// ============================================================
//  Services/  —  Tầng nghiệp vụ (Business Logic)
//  Controller chỉ nhận request rồi gọi Service
//  Service mới thực sự xử lý logic và gọi database
// ============================================================
using Microsoft.EntityFrameworkCore;
using RestaurantPOS.API.Data;
using RestaurantPOS.API.DTOs;
using RestaurantPOS.API.Models;

namespace RestaurantPOS.API.Services;


// ════════════════════════════════════════════════════════════
//  TABLE SERVICE  —  Quản lý sơ đồ bàn
// ════════════════════════════════════════════════════════════
public interface ITableService
{
    Task<List<AreaWithTablesDto>> GetFloorMapAsync();
    Task<OrderDetailResponseDto> CreateOrderAsync(int tableId, int userId);
    Task<List<AreaDto>> GetAreasAsync();
    Task<AreaDto> AddAreaAsync(UpsertAreaDto dto);
    Task<AreaDto> UpdateAreaAsync(int areaId, UpsertAreaDto dto);
    Task<bool> DeleteAreaAsync(int areaId);
    
    // Table CRUD
    Task<List<TableDto>> GetTablesByAreaAsync(int areaId);
    Task<TableDto> AddTableAsync(UpsertTableDto dto);
    Task<TableDto> UpdateTableAsync(int tableId, UpsertTableDto dto);
    Task<bool> DeleteTableAsync(int tableId);
}

public class TableService : ITableService
{
    private readonly AppDbContext _db;

    // Constructor Injection: ASP.NET tự inject AppDbContext
    public TableService(AppDbContext db) => _db = db;

    // Lấy toàn bộ sơ đồ bàn kèm trạng thái
    public async Task<List<AreaWithTablesDto>> GetFloorMapAsync()
    {
        var areas = await _db.Areas
            .Where(a => a.IsActive)
            .OrderBy(a => a.SortOrder)
            .Include(a => a.DiningTables.Where(t => t.IsActive))
            .ToListAsync();

        var activeOrderList = await _db.Orders
            .Where(o => o.Status == 0 || o.Status == 1)
            .Include(o => o.OrderDetails)
            .Include(o => o.User)
            .ToListAsync();

        // Map theo TableID
        var activeOrders = activeOrderList.ToDictionary(o => o.TableID);

        var now = DateTime.Now;

        return areas.Select(a => new AreaWithTablesDto
        {
            AreaID   = a.AreaID,
            AreaName = a.AreaName,
            Tables   = a.DiningTables.OrderBy(t => t.TableName).Select(t =>
            {
                activeOrders.TryGetValue(t.TableID, out var order);
                var durationMin = order != null ? (int)(now - order.CreatedAt).TotalMinutes : 0;
                var itemCount     = order?.OrderDetails?.Sum(d => d.Quantity) ?? 0;
                var currentAmount  = order?.OrderDetails?.Sum(d => d.Quantity * d.UnitPrice) ?? 0m;
                return new TableStatusDto
                {
                    TableID       = t.TableID,
                    TableName     = t.TableName,
                    Capacity      = t.Capacity,
                    Status        = t.Status,
                    ActiveOrderID = order?.OrderID,
                    ItemCount     = itemCount,
                    CurrentAmount = currentAmount,
                    OpenDuration  = order != null
                        ? (durationMin >= 60 ? $"{durationMin / 60}h{durationMin % 60}ph" : $"{durationMin}ph")
                        : null,
                    StaffName     = order?.User?.FullName
                };
            }).ToList()
        }).ToList();
    }


    // Tạo order mới khi khách ngồi vào bàn
    public async Task<OrderDetailResponseDto> CreateOrderAsync(int tableId, int userId)
    {
        // Kiểm tra bàn đã có order chưa
        var existingOrder = await _db.Orders
            .FirstOrDefaultAsync(o => o.TableID == tableId && (o.Status == 0 || o.Status == 1));

        if (existingOrder != null)
            throw new InvalidOperationException("Bàn này đang có khách. Không thể tạo order mới.");

        var table = await _db.DiningTables.FindAsync(tableId)
            ?? throw new KeyNotFoundException($"Không tìm thấy bàn ID={tableId}");

        // Tạo order mới
        var order = new Order { TableID = tableId, UserID = userId, Status = 0 };
        _db.Orders.Add(order);

        // Cập nhật trạng thái bàn → Có khách
        table.Status = 1;

        await _db.SaveChangesAsync();

        return new OrderDetailResponseDto
        {
            OrderID = order.OrderID,
            TableID = tableId,
            TableName = table.TableName,
            Status = order.Status,
            TotalAmount = 0,
            FinalAmount = 0,
            StaffID = userId,
            StaffName = "", // Trống khi mới tạo (frontend sẽ tự load lại hoặc bỏ qua)
            CreatedAt = order.CreatedAt,
            Items = new List<OrderItemDto>()
        };
    }

    public async Task<List<AreaDto>> GetAreasAsync()
    {
        return await _db.Areas
            .Where(a => a.IsActive)
            .OrderBy(a => a.SortOrder)
            .Select(a => new AreaDto
            {
                AreaID = a.AreaID,
                AreaName = a.AreaName,
                Description = a.Description,
                SortOrder = a.SortOrder
            }).ToListAsync();
    }

    public async Task<AreaDto> AddAreaAsync(UpsertAreaDto dto)
    {
        var area = new Area
        {
            AreaName = dto.AreaName,
            SortOrder = dto.SortOrder,
            Description = dto.Description,
            IsActive = true
        };
        _db.Areas.Add(area);
        await _db.SaveChangesAsync();
        return new AreaDto
        {
            AreaID = area.AreaID,
            AreaName = area.AreaName,
            Description = area.Description,
            SortOrder = area.SortOrder
        };
    }

    public async Task<AreaDto> UpdateAreaAsync(int areaId, UpsertAreaDto dto)
    {
        var area = await _db.Areas.FindAsync(areaId)
            ?? throw new KeyNotFoundException($"Không tìm thấy khu vực ID={areaId}");
        
        area.AreaName = dto.AreaName;
        area.SortOrder = dto.SortOrder;
        area.Description = dto.Description;

        await _db.SaveChangesAsync();
        return new AreaDto
        {
            AreaID = area.AreaID,
            AreaName = area.AreaName,
            Description = area.Description,
            SortOrder = area.SortOrder
        };
    }

    public async Task<bool> DeleteAreaAsync(int areaId)
    {
        var area = await _db.Areas.FirstOrDefaultAsync(a => a.AreaID == areaId && a.IsActive);
        if (area == null) return false;

        area.IsActive = false; // Soft delete
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<List<TableDto>> GetTablesByAreaAsync(int areaId)
    {
        return await _db.DiningTables
            .Where(t => t.AreaID == areaId && t.IsActive)
            .Select(t => new TableDto
            {
                TableID = t.TableID,
                AreaID = t.AreaID,
                TableName = t.TableName,
                Capacity = t.Capacity,
                Status = t.Status
            }).ToListAsync();
    }

    public async Task<TableDto> AddTableAsync(UpsertTableDto dto)
    {
        var table = new DiningTable
        {
            AreaID = dto.AreaID,
            TableName = dto.TableName,
            Capacity = dto.Capacity,
            Status = 0,
            IsActive = true
        };
        _db.DiningTables.Add(table);
        await _db.SaveChangesAsync();
        return new TableDto
        {
            TableID = table.TableID,
            AreaID = table.AreaID,
            TableName = table.TableName,
            Capacity = table.Capacity,
            Status = table.Status
        };
    }

    public async Task<TableDto> UpdateTableAsync(int tableId, UpsertTableDto dto)
    {
        var table = await _db.DiningTables.FindAsync(tableId)
            ?? throw new KeyNotFoundException($"Không tìm thấy bàn ID={tableId}");

        table.AreaID = dto.AreaID;
        table.TableName = dto.TableName;
        table.Capacity = dto.Capacity;

        await _db.SaveChangesAsync();
        return new TableDto
        {
            TableID = table.TableID,
            AreaID = table.AreaID,
            TableName = table.TableName,
            Capacity = table.Capacity,
            Status = table.Status
        };
    }

    public async Task<bool> DeleteTableAsync(int tableId)
    {
        var table = await _db.DiningTables.FirstOrDefaultAsync(t => t.TableID == tableId && t.IsActive);
        if (table == null) return false;

        table.IsActive = false; // Soft delete
        await _db.SaveChangesAsync();
        return true;
    }
}


// ════════════════════════════════════════════════════════════
//  ORDER SERVICE  —  Xử lý đặt món và thanh toán
// ════════════════════════════════════════════════════════════
public interface IOrderService
{
    Task<OrderDetailResponseDto> GetOrderAsync(int orderId);
    Task<OrderDetailResponseDto> AddItemAsync(int orderId, AddItemDto dto);
    Task<bool> RemoveItemAsync(int orderId, int orderDetailId);
    Task<CheckoutResponseDto> CheckoutAsync(int orderId, CheckoutRequestDto dto);
    Task<OrderDetailResponseDto> UpdateOrderStaffAsync(int orderId, int staffId);
    Task<List<PaymentMethodDto>> GetPaymentMethodsAsync();
}

public class OrderService : IOrderService
{
    private readonly AppDbContext _db;
    public OrderService(AppDbContext db) => _db = db;

    // Lấy chi tiết 1 order
    public async Task<OrderDetailResponseDto> GetOrderAsync(int orderId)
    {
        var order = await _db.Orders
            .Include(o => o.DiningTable)
            .Include(o => o.User)
            .Include(o => o.OrderDetails)
                .ThenInclude(od => od.Product)
            .FirstOrDefaultAsync(o => o.OrderID == orderId)
            ?? throw new KeyNotFoundException($"Không tìm thấy order ID={orderId}");

        return MapToDto(order);
    }

    // Thêm món vào order
    public async Task<OrderDetailResponseDto> AddItemAsync(int orderId, AddItemDto dto)
    {
        var order = await _db.Orders
            .Include(o => o.OrderDetails)
            .FirstOrDefaultAsync(o => o.OrderID == orderId && o.Status == 0)
            ?? throw new KeyNotFoundException("Order không tồn tại hoặc đã đóng.");

        var product = await _db.Products.FindAsync(dto.ProductID)
            ?? throw new KeyNotFoundException($"Không tìm thấy món ID={dto.ProductID}");

        // Nếu món đã có thì cộng thêm, không thì thêm mới
        var existing = order.OrderDetails.FirstOrDefault(od => od.ProductID == dto.ProductID);
        if (existing != null)
        {
            existing.Quantity += dto.Quantity;
        }
        else
        {
            order.OrderDetails.Add(new OrderDetail
            {
                OrderID = orderId,
                ProductID = dto.ProductID,
                Quantity = dto.Quantity,
                UnitPrice = product.Price,   // Snapshot giá tại thời điểm đặt
                Note = dto.Note
            });
        }

        // Tính lại tổng tiền
        RecalculateTotal(order);
        await _db.SaveChangesAsync();

        return await GetOrderAsync(orderId);
    }

    // Xóa 1 dòng món khỏi order
    public async Task<bool> RemoveItemAsync(int orderId, int orderDetailId)
    {
        var item = await _db.OrderDetails
            .FirstOrDefaultAsync(od => od.OrderDetailID == orderDetailId && od.OrderID == orderId);

        if (item == null) return false;

        _db.OrderDetails.Remove(item);

        var order = await _db.Orders.Include(o => o.OrderDetails)
            .FirstAsync(o => o.OrderID == orderId);
        RecalculateTotal(order);

        await _db.SaveChangesAsync();
        return true;
    }

    // Thanh toán
    public async Task<CheckoutResponseDto> CheckoutAsync(int orderId, CheckoutRequestDto dto)
    {
        var order = await _db.Orders
            .Include(o => o.DiningTable)
            .FirstOrDefaultAsync(o => o.OrderID == orderId && o.Status <= 1)
            ?? throw new KeyNotFoundException("Order không tồn tại hoặc đã thanh toán.");

        order.Discount = dto.Discount;
        order.FinalAmount = order.TotalAmount - dto.Discount;
        order.PaymentMethodID = dto.PaymentMethodID;
        order.CustomerPaid = dto.CustomerPaid;
        order.ChangeAmount = dto.CustomerPaid.HasValue
            ? dto.CustomerPaid.Value - order.FinalAmount : null;
        order.Status = 2;          // Đã thanh toán
        order.CheckoutAt = DateTime.Now;

        // Giải phóng bàn → Trống
        order.DiningTable.Status = 0;

        await _db.SaveChangesAsync();

        return new CheckoutResponseDto
        {
            Success = true,
            FinalAmount = order.FinalAmount,
            ChangeAmount = order.ChangeAmount ?? 0,
            Message = $"Thanh toán thành công. Tổng tiền: {order.FinalAmount:N0} đ"
        };
    }

    public async Task<OrderDetailResponseDto> UpdateOrderStaffAsync(int orderId, int staffId)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderID == orderId && o.Status <= 1)
            ?? throw new KeyNotFoundException("Order không tồn tại hoặc đã thanh toán.");

        var user = await _db.Users.FindAsync(staffId)
            ?? throw new KeyNotFoundException("Nhân viên không tồn tại.");

        order.UserID = staffId;
        await _db.SaveChangesAsync();

        return await GetOrderAsync(orderId);
    }

    public async Task<List<PaymentMethodDto>> GetPaymentMethodsAsync()
    {
        return await _db.PaymentMethods
            .Where(pm => pm.IsActive)
            .Select(pm => new PaymentMethodDto
            {
                PaymentMethodID = pm.PaymentMethodID,
                MethodName = pm.MethodName,
                Description = pm.Description
            }).ToListAsync();
    }

    // ── HELPER ─────────────────────────────────────────────
    private static void RecalculateTotal(Order order)
    {
        order.TotalAmount = order.OrderDetails.Sum(od => od.Quantity * od.UnitPrice);
        order.FinalAmount = order.TotalAmount - order.Discount;
    }

    private static OrderDetailResponseDto MapToDto(Order order) => new()
    {
        OrderID = order.OrderID,
        TableID = order.TableID,
        TableName = order.DiningTable?.TableName ?? "",
        Status = order.Status,
        TotalAmount = order.TotalAmount,
        Discount = order.Discount,
        FinalAmount = order.FinalAmount,
        StaffID = order.UserID,
        StaffName = order.User?.FullName ?? "",
        CreatedAt = order.CreatedAt,
        Items = order.OrderDetails.Select(od => new OrderItemDto
        {
            OrderDetailID = od.OrderDetailID,
            ProductID = od.ProductID,
            ProductName = od.Product?.ProductName ?? "",
            Quantity = od.Quantity,
            UnitPrice = od.UnitPrice,
            SubTotal = od.Quantity * od.UnitPrice,
            Note = od.Note
        }).ToList()
    };
}


// ════════════════════════════════════════════════════════════
//  PRODUCT SERVICE  —  Quản lý thực đơn
// ════════════════════════════════════════════════════════════
public interface IProductService
{
    Task<List<CategoryDto>> GetCategoriesAsync();
    Task<List<ProductDto>> GetProductsAsync(int? categoryId = null);
    Task<ProductDto> AddProductAsync(UpsertProductDto dto);
    Task<ProductDto> UpdateProductAsync(int productId, UpsertProductDto dto);
    Task<bool> ToggleProductAsync(int productId);
    Task<bool> DeleteProductAsync(int productId);

    // Category CRUD
    Task<CategoryDto> AddCategoryAsync(UpsertCategoryDto dto);
    Task<CategoryDto> UpdateCategoryAsync(int categoryId, UpsertCategoryDto dto);
    Task<bool> DeleteCategoryAsync(int categoryId);
}

public class ProductService : IProductService
{
    private readonly AppDbContext _db;
    public ProductService(AppDbContext db) => _db = db;

    public async Task<List<ProductDto>> GetProductsAsync(int? categoryId = null)
    {
        var query = _db.Products
            .Include(p => p.Category)
            .AsQueryable();

        if (categoryId.HasValue)
            query = query.Where(p => p.CategoryID == categoryId.Value);

        return await query
            .OrderBy(p => p.Category.SortOrder)
            .ThenBy(p => p.ProductName)
            .Select(p => new ProductDto
            {
                ProductID = p.ProductID,
                CategoryID = p.CategoryID,
                CategoryName = p.Category.CategoryName,
                ProductName = p.ProductName,
                Description = p.Description,
                Price = p.Price,
                ImageUrl = p.ImageUrl,
                IsAvailable = p.IsAvailable
            })
            .ToListAsync();
    }

    public async Task<List<CategoryDto>> GetCategoriesAsync()
    {
        return await _db.Categories
            .OrderBy(c => c.SortOrder)
            .Select(c => new CategoryDto
            {
                CategoryID = c.CategoryID,
                CategoryName = c.CategoryName,
                SortOrder = c.SortOrder
            })
            .ToListAsync();
    }

    public async Task<ProductDto> AddProductAsync(UpsertProductDto dto)
    {
        var category = await _db.Categories.FindAsync(dto.CategoryID)
            ?? throw new KeyNotFoundException("Category not found");

        var product = new Product
        {
            CategoryID = dto.CategoryID,
            ProductName = dto.ProductName,
            Description = dto.Description,
            Price = dto.Price,
            ImageUrl = dto.ImageUrl,
            IsAvailable = dto.IsAvailable,
            IsActive = true
        };

        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        return new ProductDto
        {
            ProductID = product.ProductID,
            CategoryID = product.CategoryID,
            CategoryName = category.CategoryName,
            ProductName = product.ProductName,
            Description = product.Description,
            Price = product.Price,
            ImageUrl = product.ImageUrl,
            IsAvailable = product.IsAvailable
        };
    }

    public async Task<ProductDto> UpdateProductAsync(int productId, UpsertProductDto dto)
    {
        var product = await _db.Products.Include(p => p.Category).FirstOrDefaultAsync(p => p.ProductID == productId && p.IsActive)
            ?? throw new KeyNotFoundException("Product not found");

        var category = await _db.Categories.FindAsync(dto.CategoryID)
            ?? throw new KeyNotFoundException("Category not found");

        product.CategoryID = dto.CategoryID;
        product.ProductName = dto.ProductName;
        product.Description = dto.Description;
        product.Price = dto.Price;
        product.ImageUrl = dto.ImageUrl;
        product.IsAvailable = dto.IsAvailable;

        await _db.SaveChangesAsync();

        return new ProductDto
        {
            ProductID = product.ProductID,
            CategoryID = product.CategoryID,
            CategoryName = category.CategoryName,
            ProductName = product.ProductName,
            Description = product.Description,
            Price = product.Price,
            ImageUrl = product.ImageUrl,
            IsAvailable = product.IsAvailable
        };
    }

    public async Task<bool> ToggleProductAsync(int productId)
    {
        var product = await _db.Products.FindAsync(productId);
        if (product == null || !product.IsActive) return false;

        product.IsAvailable = !product.IsAvailable;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteProductAsync(int productId)
    {
        var product = await _db.Products.FindAsync(productId);
        if (product == null || !product.IsActive) return false;

        // Soft delete
        product.IsActive = false;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<CategoryDto> AddCategoryAsync(UpsertCategoryDto dto)
    {
        var category = new Category
        {
            CategoryName = dto.CategoryName,
            SortOrder = dto.SortOrder,
            IsActive = true
        };
        _db.Categories.Add(category);
        await _db.SaveChangesAsync();
        return new CategoryDto
        {
            CategoryID = category.CategoryID,
            CategoryName = category.CategoryName,
            SortOrder = category.SortOrder
        };
    }

    public async Task<CategoryDto> UpdateCategoryAsync(int categoryId, UpsertCategoryDto dto)
    {
        var category = await _db.Categories.FindAsync(categoryId)
            ?? throw new KeyNotFoundException($"Không tìm thấy danh mục ID={categoryId}");

        category.CategoryName = dto.CategoryName;
        category.SortOrder = dto.SortOrder;

        await _db.SaveChangesAsync();
        return new CategoryDto
        {
            CategoryID = category.CategoryID,
            CategoryName = category.CategoryName,
            SortOrder = category.SortOrder
        };
    }

    public async Task<bool> DeleteCategoryAsync(int categoryId)
    {
        var category = await _db.Categories.FirstOrDefaultAsync(c => c.CategoryID == categoryId && c.IsActive);
        if (category == null) return false;

        // Kiểm tra nếu có sản phẩm thì không cho xóa (hoặc soft delete cả sản phẩm)
        // Ở đây ta chọn soft delete category nhưng giữ sản phẩm? 
        // Thường thì nên ngăn chặn nếu có sản phẩm.
        if (await _db.Products.AnyAsync(p => p.CategoryID == categoryId && p.IsActive))
            throw new InvalidOperationException("Không thể xóa danh mục đang có sản phẩm.");

        category.IsActive = false;
        await _db.SaveChangesAsync();
        return true;
    }
}


// ════════════════════════════════════════════════════════════
//  DASHBOARD SERVICE  —  Thống kê tổng quan
// ════════════════════════════════════════════════════════════
public interface IDashboardService
{
    Task<DashboardDto> GetDashboardAsync();
}

public class DashboardService : IDashboardService
{
    private readonly AppDbContext _db;
    public DashboardService(AppDbContext db) => _db = db;

    public async Task<DashboardDto> GetDashboardAsync()
    {
        try
        {
            var now = DateTime.Now;
            var todayStart = now.Date;
            var todayEnd   = todayStart.AddDays(1);
            var yesterdayStart = todayStart.AddDays(-1);
            var monthStart = new DateTime(now.Year, now.Month, 1);

            // 1. Lấy tất cả order đã thanh toán hôm nay
            // Dùng AsNoTracking để tối ưu hiệu năng cho dashboard
            var todayPaid = await _db.Orders
                .Where(o => o.Status == 2 && o.CheckoutAt.HasValue && o.CheckoutAt >= todayStart && o.CheckoutAt < todayEnd)
                .Include(o => o.DiningTable)
                .Include(o => o.User)
                .AsNoTracking()
                .ToListAsync();

            // 2. Doanh thu hôm qua
            var yesterdayRevenue = await _db.Orders
                .Where(o => o.Status == 2 && o.CheckoutAt.HasValue && o.CheckoutAt >= yesterdayStart && o.CheckoutAt < todayStart)
                .AsNoTracking()
                .SumAsync(o => (decimal?)o.FinalAmount) ?? 0m;

            // 3. Doanh thu tháng này
            var monthRevenue = await _db.Orders
                .Where(o => o.Status == 2 && o.CheckoutAt.HasValue && o.CheckoutAt >= monthStart && o.CheckoutAt < todayEnd)
                .AsNoTracking()
                .SumAsync(o => (decimal?)o.FinalAmount) ?? 0m;

            // 4. Đơn đang phục vụ (Status 0=Chưa thanh toán, 1=Đã in bill nhưng chưa thu tiền)
            var activeOrdersCount = await _db.Orders.CountAsync(o => o.Status == 0 || o.Status == 1);

            // 5. Doanh thu theo giờ
            var hourlyRevenue = todayPaid
                .GroupBy(o => o.CheckoutAt!.Value.Hour)
                .Select(g => new HourlyRevenueDto
                {
                    Hour    = g.Key,
                    Revenue = g.Sum(o => o.FinalAmount),
                    Orders  = g.Count()
                })
                .OrderBy(x => x.Hour)
                .ToList();

            // 6. Doanh thu theo nhân viên
            var revenueByStaff = todayPaid
                .Where(o => o.User != null)
                .GroupBy(o => o.User!.FullName)
                .Select(g => new StaffRevenueDto
                {
                    StaffName   = g.Key,
                    Revenue     = g.Sum(o => o.FinalAmount),
                    OrdersCount = g.Count()
                })
                .OrderByDescending(x => x.Revenue)
                .ToList();

            // 7. Hoạt động gần đây (20 đơn cuối)
            var recentOrders = todayPaid
                .OrderByDescending(o => o.CheckoutAt)
                .Take(20)
                .Select(o => {
                    var diff = now - (o.CheckoutAt ?? now);
                    string timeAgo = diff.TotalMinutes < 1 ? "vừa xong"
                        : diff.TotalMinutes < 60 ? $"{(int)Math.Max(0, diff.TotalMinutes)} phút trước"
                        : $"{(int)Math.Max(0, diff.TotalHours)} giờ trước";
                    return new RecentOrderDto
                    {
                        OrderID     = o.OrderID,
                        TableName   = o.DiningTable?.TableName ?? "?",
                        FinalAmount = o.FinalAmount,
                        StaffName   = o.User?.FullName ?? "—",
                        CheckoutAt  = o.CheckoutAt ?? now,
                        TimeAgo     = timeAgo
                    };
                })
                .ToList();

            return new DashboardDto
            {
                TodayRevenue     = todayPaid.Sum(o => o.FinalAmount),
                YesterdayRevenue = yesterdayRevenue,
                TodayOrders      = todayPaid.Count,
                ActiveOrders     = activeOrdersCount,
                TodayCustomers   = todayPaid.Count,
                MonthRevenue     = monthRevenue,
                HourlyRevenue    = hourlyRevenue,
                RecentOrders     = recentOrders,
                RevenueByStaff   = revenueByStaff
            };
        }
        catch (Exception)
        {
            // Trả về DTO trống thay vì crash 500
            return new DashboardDto();
        }
    }
}
