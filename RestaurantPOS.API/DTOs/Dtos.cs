// ============================================================
//  DTOs/  —  Data Transfer Objects
//
//  TẠI SAO CẦN DTO thay vì trả thẳng Model?
//  - Tránh lộ thông tin nhạy cảm (PasswordHash, ...)
//  - Chỉ trả đúng dữ liệu frontend cần
//  - Dễ validate dữ liệu đầu vào
// ============================================================

// ── DTOs/FloorMap ──────────────────────────────────────────
namespace RestaurantPOS.API.DTOs;

// Trả về khi GET /api/areas (màn hình sơ đồ bàn)
public class AreaWithTablesDto
{
    public int AreaID { get; set; }
    public string AreaName { get; set; } = string.Empty;
    public List<TableStatusDto> Tables { get; set; } = new();
}

public class TableStatusDto
{
    public int TableID { get; set; }
    public string TableName { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public byte Status { get; set; }
    public string StatusLabel => Status switch
    {
        0 => "Trống",
        1 => "Có khách",
        2 => "Đã đặt",
        3 => "Đang dọn",
        _ => "Không xác định"
    };
    public int? ActiveOrderID { get; set; }   // null nếu bàn trống
    public int ItemCount { get; set; }        // Số món đã gọi
    public decimal CurrentAmount { get; set; } // Tổng tiền hiện tại
    public string? OpenDuration { get; set; }  // Thời gian mở bàn (ví dụ: "30ph")
    public string? StaffName { get; set; }     // Tên nhân viên phục vụ
}


// ── DTOs/Products ──────────────────────────────────────────
// Trả về khi GET /api/products
public class ProductDto
{
    public int ProductID { get; set; }
    public int CategoryID { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsAvailable { get; set; }
}


// ── DTOs/Orders ────────────────────────────────────────────
// Nhận vào khi thêm món: POST /api/orders/{orderId}/items
public class AddItemDto
{
    public int ProductID { get; set; }
    public int Quantity { get; set; } = 1;
    public string? Note { get; set; }
}

// Trả về chi tiết order đang mở
public class OrderDetailResponseDto
{
    public int OrderID { get; set; }
    public int TableID { get; set; }
    public string TableName { get; set; } = string.Empty;
    public byte Status { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal Discount { get; set; }
    public decimal FinalAmount { get; set; }
    public int StaffID { get; set; }
    public string StaffName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<OrderItemDto> Items { get; set; } = new();
}

public class OrderItemDto
{
    public int OrderDetailID { get; set; }
    public int ProductID { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal SubTotal { get; set; }
    public string? Note { get; set; }
}


// ── DTOs/Checkout ──────────────────────────────────────────
// Nhận vào khi thanh toán: POST /api/orders/{orderId}/checkout
public class CheckoutRequestDto
{
    public int PaymentMethodID { get; set; }
    public decimal Discount { get; set; } = 0;
    public decimal? CustomerPaid { get; set; }   // Chỉ cần khi thanh toán tiền mặt
}

// Trả về sau khi thanh toán xong
public class CheckoutResponseDto
{
    public bool Success { get; set; }
    public decimal FinalAmount { get; set; }
    public decimal ChangeAmount { get; set; }
    public string Message { get; set; } = string.Empty;
}


// ── DTOs/Dashboard ─────────────────────────────────────────
// Trả về khi GET /api/dashboard
public class DashboardDto
{
    public decimal TodayRevenue { get; set; }
    public decimal YesterdayRevenue { get; set; }
    public int TodayOrders { get; set; }
    public int ActiveOrders { get; set; }       // đơn đang phục vụ
    public int TodayCustomers { get; set; }      // số lượt khách hôm nay
    public decimal MonthRevenue { get; set; }
    public List<HourlyRevenueDto> HourlyRevenue { get; set; } = new();
    public List<RecentOrderDto> RecentOrders { get; set; } = new();
    public List<StaffRevenueDto> RevenueByStaff { get; set; } = new();
    public string Message { get; set; } = string.Empty;
}

public class StaffRevenueDto
{
    public string StaffName { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public int OrdersCount { get; set; }
}

public class AreaDto
{
    public int AreaID { get; set; }
    public string AreaName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
}

public class UpsertAreaDto
{
    public string AreaName { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public string? Description { get; set; }
}

public class UpsertCategoryDto
{
    public string CategoryName { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public class TableDto
{
    public int TableID { get; set; }
    public int AreaID { get; set; }
    public string TableName { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public byte Status { get; set; }
}

public class UpsertTableDto
{
    public int AreaID { get; set; }
    public string TableName { get; set; } = string.Empty;
    public int Capacity { get; set; }
}

public class PaymentMethodDto
{
    public int PaymentMethodID { get; set; }
    public string MethodName { get; set; } = string.Empty;
    public string? Description { get; set; }
}
