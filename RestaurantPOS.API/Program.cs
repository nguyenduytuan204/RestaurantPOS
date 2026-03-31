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

string? connectionString = null;
var pgHost = Environment.GetEnvironmentVariable("PGHOST");
var pgPort = Environment.GetEnvironmentVariable("PGPORT");
var pgUser = Environment.GetEnvironmentVariable("PGUSER");
var pgPass = Environment.GetEnvironmentVariable("PGPASSWORD");
var pgDb   = Environment.GetEnvironmentVariable("PGDATABASE");
var dbUrl  = Environment.GetEnvironmentVariable("DATABASE_URL");

if (!string.IsNullOrEmpty(pgHost))
{
    connectionString = $"Host={pgHost};Port={pgPort};Database={pgDb};Username={pgUser};Password={pgPass};SSL Mode=Require;Trust Server Certificate=true";
    builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
    Console.WriteLine("DEBUG: Using Railway PG* environment variables.");
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
        builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
        Console.WriteLine("DEBUG: Using DATABASE_URL environment variable.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ERROR parsing DATABASE_URL: {ex.Message}");
    }
}
else 
{
    // Local development — Use SQLite to avoid connection issues
    var sqliteConn = builder.Configuration.GetConnectionString("SqliteConnection") ?? "Data Source=RestaurantPOS.db";
    builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(sqliteConn));
    Console.WriteLine("DEBUG: Using Local SQLite database.");
}

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
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ── 9. SEED DATA ──────────────────────────────────────────
try 
{
    using (var scope = app.Services.CreateScope())
    {
        var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        db.Database.EnsureCreated(); // Creates SQLite file if missing
        await auth.SeedAdminAsync();

        if (!db.Areas.Any())
        {
            Console.WriteLine("DEBUG: SEEDING EXTENSIVE INITIAL DATA...");
            
            // 1. Payment Methods
            db.PaymentMethods.AddRange(
                new PaymentMethod { MethodName = "Tiền mặt", Description = "Thanh toán bằng tiền mặt", IsActive = true },
                new PaymentMethod { MethodName = "Chuyển khoản", Description = "Chuyển khoản ngân hàng / QR", IsActive = true },
                new PaymentMethod { MethodName = "VNPay", Description = "Thanh toán điện tử VNPay", IsActive = true }
            );

            // 2. Categories
            var cat1 = new Category { CategoryName = "ẤM CÁI BỤNG", SortOrder = 1 };
            var cat2 = new Category { CategoryName = "GIẢI KHÁT", SortOrder = 2 };
            var cat3 = new Category { CategoryName = "TRÁNG MIỆNG", SortOrder = 3 };
            db.Categories.AddRange(cat1, cat2, cat3);

            // 3. Areas
            var area1 = new Area { AreaName = "Tầng 1", SortOrder = 1 };
            var area2 = new Area { AreaName = "Sân vườn", SortOrder = 2 };
            db.Areas.AddRange(area1, area2);
            await db.SaveChangesAsync();

            // 4. Tables
            for (int i = 1; i <= 8; i++)
            {
                db.DiningTables.Add(new DiningTable { Area = area1, TableName = $"Bàn {i:D2}", Capacity = 4 });
            }
            for (int i = 9; i <= 12; i++)
            {
                db.DiningTables.Add(new DiningTable { Area = area2, TableName = $"Bàn {i:D2}", Capacity = 2 });
            }

            // 5. Products
            db.Products.AddRange(
                new Product { Category = cat1, ProductName = "Bún bò Huế", Price = 45000, IsAvailable = true },
                new Product { Category = cat1, ProductName = "Phở Thìn đặc biệt", Price = 55000, IsAvailable = true },
                new Product { Category = cat1, ProductName = "Cơm tấm sườn bì", Price = 40000, IsAvailable = true },
                new Product { Category = cat2, ProductName = "Cà phê sữa đá", Price = 25000, IsAvailable = true },
                new Product { Category = cat2, ProductName = "Trà đào cam sả", Price = 35000, IsAvailable = true },
                new Product { Category = cat2, ProductName = "Nước cam ép", Price = 30000, IsAvailable = true },
                new Product { Category = cat3, ProductName = "Chè khúc bạch", Price = 20000, IsAvailable = true },
                new Product { Category = cat3, ProductName = "Kem bơ Đà Lạt", Price = 30000, IsAvailable = true }
            );
            
            await db.SaveChangesAsync();
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error during initialization: {ex.Message}");
}

app.Run();
