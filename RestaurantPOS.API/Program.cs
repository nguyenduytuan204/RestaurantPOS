using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RestaurantPOS.API.Data;
using RestaurantPOS.API.Models;
using RestaurantPOS.API.Repositories;
using RestaurantPOS.API.Services;

var builder = WebApplication.CreateBuilder(args);

// ── 1. CONTROLLERS + SWAGGER ──────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Restaurant POS API", Version = "v1" });
});

// ── 2. DATABASE (Entity Framework Core) ──────────────────
// Chuẩn hóa hành vi DateTime cho PostgreSQL
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

string? connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Ưu tiên các biến môi trường từ Railway (PGHOST, PGPORT, v.v.)
var pgHost = Environment.GetEnvironmentVariable("PGHOST");
var pgPort = Environment.GetEnvironmentVariable("PGPORT");
var pgUser = Environment.GetEnvironmentVariable("PGUSER");
var pgPass = Environment.GetEnvironmentVariable("PGPASSWORD");
var pgDb   = Environment.GetEnvironmentVariable("PGDATABASE");
var dbUrl  = Environment.GetEnvironmentVariable("DATABASE_URL");

if (!string.IsNullOrEmpty(pgHost))
{
    connectionString = $"Host={pgHost};Port={pgPort};Database={pgDb};Username={pgUser};Password={pgPass};SSL Mode=Require;Trust Server Certificate=true";
    Console.WriteLine($"DEBUG: Using Railway PG* variables. Host: {pgHost}");
}
else if (!string.IsNullOrEmpty(dbUrl))
{
    try 
    {
        var uri = new Uri(dbUrl);
        var userInfo = uri.UserInfo.Split(':');
        var user = Uri.UnescapeDataString(userInfo[0]);
        var pass = Uri.UnescapeDataString(userInfo[userInfo.Length > 1 ? 1 : 0]);
        connectionString = $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.Trim('/')};Username={user};Password={pass};SSL Mode=Require;Trust Server Certificate=true";
        Console.WriteLine($"DEBUG: Using DATABASE_URL. Host: {uri.Host}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ERROR parsing DATABASE_URL: {ex.Message}");
    }
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// ── 3. REPOSITORIES ───────────────────────────────────────
builder.Services.AddScoped<IOrderRepository,   OrderRepository>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();

// ── 4. SERVICES (DI) ─────────────────────────────────────
builder.Services.AddScoped<ITableService,     TableService>();
builder.Services.AddScoped<IOrderService,     OrderService>();
builder.Services.AddScoped<IProductService,   ProductService>();
builder.Services.AddScoped<IAuthService,      AuthService>();
builder.Services.AddScoped<IReportService,    ReportService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IVnPayService,     VnPayService>();

// ── 5. JWT AUTHENTICATION ─────────────────────────────────
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"],
            ValidAudience            = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });

// ── 6. AUTHORIZATION POLICIES ─────────────────────────────
builder.Services.AddAuthorization(opt =>
{
    opt.AddPolicy("AdminOnly",  p => p.RequireClaim("RoleCode", "3"));
    opt.AddPolicy("ManagerUp",  p => p.RequireClaim("RoleCode", "2", "3"));
    opt.AddPolicy("AllStaff",   p => p.RequireAuthenticatedUser());
});

// ── 7. CORS ───────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
        policy.WithOrigins(
                "http://localhost:5500",
                "http://127.0.0.1:5500",
                "http://localhost:3000")
              .SetIsOriginAllowed(origin =>
              {
                  // Cho phép file:// (origin == "null") và localhost
                  if (string.IsNullOrEmpty(origin) || origin == "null") return true;
                  return origin.StartsWith("http://localhost") || 
                         origin.StartsWith("https://localhost") || 
                         origin.StartsWith("http://127.0.0.1");
              })
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

// ── 8. MIDDLEWARE PIPELINE ────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowFrontend");
app.UseHttpsRedirection();
// ── 7.8 CLEAN URLS (No .html extension) ────────────────────
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value;
    if (!string.IsNullOrEmpty(path) && !path.EndsWith("/") && !Path.HasExtension(path))
    {
        var webRoot = app.Environment.WebRootPath;
        var filePath = Path.Combine(webRoot, path.TrimStart('/') + ".html");
        if (File.Exists(filePath))
        {
            context.Request.Path = path + ".html";
        }
    }
    await next();
});

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();   // phải trước UseAuthorization
app.UseAuthorization();
app.MapControllers();

    // ── 9. SEED DỮ LIỆU MẶC ĐỊNH & FIX FONT ──────────────────
    try 
    {
        using (var scope = app.Services.CreateScope())
        {
            var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();
            await auth.SeedAdminAsync();

        // Kiểm tra xem đã có dữ liệu chưa. Nếu chưa có thì mới nạp (Tránh mất bàn đã có)
        bool dbIsEmpty = !db.Areas.Any();
        bool needForceFix = false; 

        if (needForceFix || dbIsEmpty)
        {
            if (needForceFix) {
                Console.WriteLine("DEBUG: FORCE DELETING OLD DATA...");
                db.Database.ExecuteSqlRaw("DELETE FROM OrderDetails");
                db.Database.ExecuteSqlRaw("DELETE FROM Orders");
                db.Database.ExecuteSqlRaw("DELETE FROM Products");
                db.Database.ExecuteSqlRaw("DELETE FROM DiningTables");
                db.Database.ExecuteSqlRaw("DELETE FROM Areas");
                db.Database.ExecuteSqlRaw("DELETE FROM Categories");
                db.Database.ExecuteSqlRaw("DELETE FROM PaymentMethods");
                try {
                    db.Database.ExecuteSqlRaw("DELETE FROM sqlite_sequence WHERE name IN ('OrderDetails', 'Orders', 'Products', 'DiningTables', 'Areas', 'Categories', 'PaymentMethods')");
                } catch {}
            }
            
            Console.WriteLine("DEBUG: SEEDING INITIAL DATA...");
            
            // 1. Payment Methods
            db.PaymentMethods.AddRange(
                new PaymentMethod { MethodName = "Tiền mặt", Description = "Thanh toán bằng tiền mặt", IsActive = true },
                new PaymentMethod { MethodName = "Chuyển khoản", Description = "Chuyển khoản ngân hàng / QR", IsActive = true }
            );

            // 2. Categories
            var categories = new List<Category>
            {
                new() { CategoryName = "BẾP THAN", SortOrder = 1 },
                new() { CategoryName = "COMBO", SortOrder = 2 },
                new() { CategoryName = "Marketing", SortOrder = 3 },
                new() { CategoryName = "MÓN CHAY", SortOrder = 4 },
                new() { CategoryName = "ỐC NHÀ NỌ", SortOrder = 5 },
                new() { CategoryName = "PHỤ THU", SortOrder = 6 },
                new() { CategoryName = "ĐẶC SẢN", SortOrder = 7 },
                new() { CategoryName = "ĂN NÀY", SortOrder = 8 },
                new() { CategoryName = "ĂN NỌ", SortOrder = 9 },
                new() { CategoryName = "CHẤM & CUỐN", SortOrder = 10 },
                new() { CategoryName = "RAU XANH", SortOrder = 11 },
                new() { CategoryName = "NO CÁI BỤNG", SortOrder = 12 },
                new() { CategoryName = "ẤM CÁI BỤNG", SortOrder = 13 },
                new() { CategoryName = "MÓN NƯỚNG", SortOrder = 14 },
                new() { CategoryName = "NƯỚNG SẴN", SortOrder = 15 },
                new() { CategoryName = "MÓN THÊM", SortOrder = 16 },
                new() { CategoryName = "NƯỚC GIẢI KHÁT", SortOrder = 17 },
                new() { CategoryName = "BIA", SortOrder = 18 },
                new() { CategoryName = "RƯỢU", SortOrder = 19 }
            };
            db.Categories.AddRange(categories);

            // 3. Areas
            var area1 = new Area { AreaName = "Khu 1", SortOrder = 1 };
            var area2 = new Area { AreaName = "Khu 2", SortOrder = 2 };
            var areaVip = new Area { AreaName = "Khu VIP", SortOrder = 3 };
            db.Areas.AddRange(area1, area2, areaVip);
            await db.SaveChangesAsync();

            // 4. Dining Tables
            for (int i = 1; i <= 8; i++) db.DiningTables.Add(new DiningTable { Area = area1, TableName = $"Bàn 1-{i:D2}", Capacity = 4 });
            for (int i = 1; i <= 8; i++) db.DiningTables.Add(new DiningTable { Area = area2, TableName = $"Bàn 2-{i:D2}", Capacity = 4 });
            db.DiningTables.Add(new DiningTable { Area = area2, TableName = "B2.3", Capacity = 4 });
            for (int i = 1; i <= 4; i++) db.DiningTables.Add(new DiningTable { Area = areaVip, TableName = $"Vip-{i:D2}", Capacity = 4 });

            // 5. Products (Mẫu một số món)
            var catDoAn = categories.First(c => c.CategoryName == "ẤM CÁI BỤNG");
            var catNuoc = categories.First(c => c.CategoryName == "NƯỚC GIẢI KHÁT");
            var catMonNay = categories.First(c => c.CategoryName == "ĂN NÀY");
            var catBia = categories.First(c => c.CategoryName == "BIA");
            var catCombo = categories.First(c => c.CategoryName == "COMBO");
            var catOc = categories.First(c => c.CategoryName == "ỐC NHÀ NỌ");
            var catNoCaiBung = categories.First(c => c.CategoryName == "NO CÁI BỤNG");
            var catChamCuon = categories.First(c => c.CategoryName == "CHẤM & CUỐN");
            var catRauXanh = categories.First(c => c.CategoryName == "RAU XANH");
            var catAnNo = categories.First(c => c.CategoryName == "ĂN NỌ");
            var catPhuThu = categories.First(c => c.CategoryName == "PHỤ THU");
            var catMarketing = categories.First(c => c.CategoryName == "Marketing");

            db.Products.AddRange(
                // BILL ITEMS
                new Product { Category = catBia, ProductName = "Tiger bạc", Price = 29000, IsAvailable = true },
                new Product { Category = catNuoc, ProductName = "Soda tắc xí muội", Price = 39000, IsAvailable = true },
                new Product { Category = catNuoc, ProductName = "Pepsi", Price = 19000, IsAvailable = true },
                new Product { Category = catNuoc, ProductName = "7 up", Price = 19000, IsAvailable = true },
                new Product { Category = catNuoc, ProductName = "Nước suối", Price = 15000, IsAvailable = true },
                new Product { Category = catNuoc, ProductName = "NƯỚC ÉP DƯA HẤU", Price = 0, IsAvailable = true },
                new Product { Category = catCombo, ProductName = "COMBO ĐẬM VỊ", Price = 719000, IsAvailable = true },
                new Product { Category = catOc, ProductName = "Ốc hương trứng muối", Price = 185000, IsAvailable = true },
                new Product { Category = catNoCaiBung, ProductName = "Ếch núp rơm", Price = 125000, IsAvailable = true },
                new Product { Category = catChamCuon, ProductName = "Gỏi xoài tôm dẻo", Price = 129000, IsAvailable = true },
                new Product { Category = catRauXanh, ProductName = "Rau càng cua bò trứng", Price = 169000, IsAvailable = true },
                new Product { Category = catMonNay, ProductName = "Sụn gà cháy tỏi", Price = 125000, IsAvailable = true },
                new Product { Category = catAnNo, ProductName = "Khoai tây chiên", Price = 65000, IsAvailable = true },
                new Product { Category = catPhuThu, ProductName = "KHĂN", Price = 2000, IsAvailable = true },
                new Product { Category = catRauXanh, ProductName = "Rau thêm (tặng)", Price = 0, IsAvailable = true },
                
                // STATUS MESSAGES
                new Product { Category = catMarketing, ProductName = "BÁO LÊN THAN", Price = 0, IsAvailable = true },
                new Product { Category = catMarketing, ProductName = "BÁO XUỐNG LÒ BẾP", Price = 0, IsAvailable = true },
                new Product { Category = catMarketing, ProductName = "BÁO LÊN LẨU", Price = 0, IsAvailable = true },
                new Product { Category = catMarketing, ProductName = "BÁO LÊN BẾP", Price = 0, IsAvailable = true },

                // SAMPLES
                new Product { Category = catDoAn, ProductName = "Bún bò Huế", Price = 45000, IsAvailable = true, Description = "Bún bò Huế truyền thống" },
                new Product { Category = catDoAn, ProductName = "Cơm sườn nướng", Price = 35000, IsAvailable = true, Description = "Cơm tấm sườn bì chả" },
                new Product { Category = catNuoc, ProductName = "Cà phê sữa đá", Price = 25000, IsAvailable = true },
                new Product { Category = catNuoc, ProductName = "Trà đào cam sả", Price = 35000, IsAvailable = true }
            );

            await db.SaveChangesAsync();
        }
        
        // --- 10. ADMIN ACCOUNT SEEDING ---
        var adminUser = await db.Users.FirstOrDefaultAsync(u => u.Username == "admin");
        if (adminUser == null)
        {
            db.Users.Add(new User 
            { 
                Username = "admin", 
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123", 11), 
                FullName = "Quản trị viên", 
                Role = 3 
            });
            await db.SaveChangesAsync();
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error during production initialization: {ex.Message}");
}

app.Run();
