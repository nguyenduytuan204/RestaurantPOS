using Microsoft.EntityFrameworkCore;
using RestaurantPOS.API.Data;
using RestaurantPOS.API.DTOs;

namespace RestaurantPOS.API.Services;

public interface IReportService
{
    Task<ReportResponseDto> GetDailyReportAsync(DateTime date);
}

public class ReportService : IReportService
{
    private readonly AppDbContext _db;
    public ReportService(AppDbContext db) => _db = db;

    public async Task<ReportResponseDto> GetDailyReportAsync(DateTime date)
    {
        var dayStart = date.Date;
        var dayEnd   = dayStart.AddDays(1);

        var orders = await _db.Orders
            .Include(o => o.OrderDetails).ThenInclude(od => od.Product).ThenInclude(p => p.Category)
            .Include(o => o.PaymentMethod)
            .Where(o => o.Status == 2 && o.CheckoutAt >= dayStart && o.CheckoutAt < dayEnd)
            .ToListAsync();

        var yesterday = await _db.Orders
            .Where(o => o.Status == 2 && o.CheckoutAt >= dayStart.AddDays(-1) && o.CheckoutAt < dayStart)
            .ToListAsync();

        decimal todayRev   = orders.Sum(o => o.FinalAmount);
        decimal yestRev    = yesterday.Sum(o => o.FinalAmount);
        int     todayCount = orders.Count;
        int     yestCount  = yesterday.Count;

        var summary = new DailySummaryDto
        {
            Date          = date.Date,
            TotalRevenue  = todayRev,
            TotalDiscount = orders.Sum(o => o.Discount),
            TotalOrders   = todayCount,
            AvgOrderValue = todayCount > 0 ? todayRev / todayCount : 0,
            TotalGuests   = todayCount * 2,
            RevenueDeltaPct = yestRev > 0 ? Math.Round((todayRev - yestRev) / yestRev * 100, 1) : 0,
            OrdersDeltaPct  = yestCount > 0 ? Math.Round((decimal)(todayCount - yestCount) / yestCount * 100, 1) : 0,
        };

        var hourly = Enumerable.Range(0, 24).Select(h => new HourlyRevenueDto
        {
            Hour    = h,
            Revenue = orders.Where(o => o.CheckoutAt!.Value.Hour == h).Sum(o => o.FinalAmount),
            Orders  = orders.Count(o => o.CheckoutAt!.Value.Hour == h),
        }).ToList();

        var allOrders14 = await _db.Orders
            .Where(o => o.Status == 2 && o.CheckoutAt >= dayStart.AddDays(-13) && o.CheckoutAt < dayEnd)
            .ToListAsync();

        var daily = Enumerable.Range(-13, 14).Select(offset =>
        {
            var d = dayStart.AddDays(offset);
            var dayOrders = allOrders14.Where(o => o.CheckoutAt!.Value.Date == d).ToList();
            return new DailyRevenueDto
            {
                Date    = d,
                Label   = offset == 0 ? "Hôm nay" : offset == -1 ? "Hôm qua" : $"{d.Day}/{d.Month}",
                Revenue = dayOrders.Sum(o => o.FinalAmount),
                Orders  = dayOrders.Count,
            };
        }).ToList();

        var allDetails = orders.SelectMany(o => o.OrderDetails).ToList();
        var totalRev   = allDetails.Sum(od => od.Quantity * od.UnitPrice);

        var catRevenue = allDetails
            .GroupBy(od => od.Product?.Category?.CategoryName ?? "Khác")
            .Select(g => new CategoryRevenueDto
            {
                CategoryName = g.Key,
                Revenue      = g.Sum(od => od.Quantity * od.UnitPrice),
                Quantity     = g.Sum(od => od.Quantity),
                Percentage   = totalRev > 0 ? Math.Round(g.Sum(od => od.Quantity * od.UnitPrice) / totalRev * 100, 1) : 0,
            })
            .OrderByDescending(c => c.Revenue).ToList();

        var topProducts = allDetails
            .GroupBy(od => new { od.ProductID, od.Product?.ProductName, od.Product?.Category?.CategoryName })
            .Select(g => new TopProductDto
            {
                ProductID    = g.Key.ProductID,
                ProductName  = g.Key.ProductName ?? "",
                CategoryName = g.Key.CategoryName ?? "",
                QuantitySold = g.Sum(od => od.Quantity),
                Revenue      = g.Sum(od => od.Quantity * od.UnitPrice),
            })
            .OrderByDescending(p => p.QuantitySold).Take(5)
            .Select((p, i) => { p.Rank = i + 1; return p; }).ToList();

        var pmRevenue = orders
            .GroupBy(o => o.PaymentMethod?.MethodName ?? "Không xác định")
            .Select(g => new PaymentMethodRevenueDto
            {
                MethodName  = g.Key,
                Revenue     = g.Sum(o => o.FinalAmount),
                OrderCount  = g.Count(),
                Percentage  = todayRev > 0 ? Math.Round(g.Sum(o => o.FinalAmount) / todayRev * 100, 1) : 0,
            })
            .OrderByDescending(p => p.Revenue).ToList();

        return new ReportResponseDto
        {
            Summary = summary, HourlyRevenue = hourly, DailyTrend = daily,
            CategoryRevenue = catRevenue, TopProducts = topProducts, PaymentMethods = pmRevenue,
        };
    }
}
