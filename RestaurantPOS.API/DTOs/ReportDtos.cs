namespace RestaurantPOS.API.DTOs;

public class DailySummaryDto
{
    public DateTime Date           { get; set; }
    public decimal  TotalRevenue   { get; set; }
    public decimal  TotalDiscount  { get; set; }
    public int      TotalOrders    { get; set; }
    public decimal  AvgOrderValue  { get; set; }
    public int      TotalGuests    { get; set; }
    public decimal  RevenueDeltaPct  { get; set; }
    public decimal  OrdersDeltaPct   { get; set; }
}

public class HourlyRevenueDto
{
    public int     Hour    { get; set; }
    public decimal Revenue { get; set; }
    public int     Orders  { get; set; }
}

public class DailyRevenueDto
{
    public DateTime Date    { get; set; }
    public string   Label   { get; set; } = string.Empty;
    public decimal  Revenue { get; set; }
    public int      Orders  { get; set; }
}

public class CategoryRevenueDto
{
    public string  CategoryName { get; set; } = string.Empty;
    public decimal Revenue      { get; set; }
    public int     Quantity     { get; set; }
    public decimal Percentage   { get; set; }
}

public class TopProductDto
{
    public int     ProductID    { get; set; }
    public string  ProductName  { get; set; } = string.Empty;
    public string  CategoryName { get; set; } = string.Empty;
    public int     QuantitySold { get; set; }
    public decimal Revenue      { get; set; }
    public int     Rank         { get; set; }
}

public class PaymentMethodRevenueDto
{
    public string  MethodName  { get; set; } = string.Empty;
    public decimal Revenue     { get; set; }
    public int     OrderCount  { get; set; }
    public decimal Percentage  { get; set; }
}

public class ReportResponseDto
{
    public DailySummaryDto          Summary          { get; set; } = new();
    public List<HourlyRevenueDto>   HourlyRevenue    { get; set; } = new();
    public List<DailyRevenueDto>    DailyTrend       { get; set; } = new();
    public List<CategoryRevenueDto> CategoryRevenue  { get; set; } = new();
    public List<TopProductDto>      TopProducts      { get; set; } = new();
    public List<PaymentMethodRevenueDto> PaymentMethods { get; set; } = new();
}

// UpsertProductDto (thêm/sửa món) - dùng cho admin_menu
public class UpsertProductDto
{
    public int CategoryID { get; set; }

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MaxLength(200)]
    public string ProductName { get; set; } = string.Empty;

    public string? Description { get; set; }

    [System.ComponentModel.DataAnnotations.Range(0, 99_000_000)]
    public decimal Price { get; set; }

    public string? ImageUrl { get; set; }
    public bool IsAvailable { get; set; } = true;
}

public class CategoryDto
{
    public int    CategoryID   { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int    SortOrder    { get; set; }
}

public class RecentOrderDto
{
    public int      OrderID     { get; set; }
    public string   TableName   { get; set; } = string.Empty;
    public decimal  FinalAmount { get; set; }
    public string   StaffName   { get; set; } = string.Empty;
    public DateTime CheckoutAt  { get; set; }
    public string   TimeAgo     { get; set; } = string.Empty;
}
