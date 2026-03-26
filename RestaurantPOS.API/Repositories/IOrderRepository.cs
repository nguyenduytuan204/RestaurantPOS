using RestaurantPOS.API.Models;

namespace RestaurantPOS.API.Repositories;

/// <summary>
/// Repository Pattern: tách biệt logic truy vấn DB ra khỏi Service.
/// Service chỉ gọi interface — không biết đến EF Core hay SQL trực tiếp.
/// </summary>
public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(int orderId);
    Task<Order?> GetActiveByTableAsync(int tableId);
    Task<Order>  CreateAsync(Order order);
    Task         UpdateAsync(Order order);
    Task<List<Order>> GetByDateRangeAsync(DateTime from, DateTime to);
}
