using Microsoft.EntityFrameworkCore;
using RestaurantPOS.API.Data;
using RestaurantPOS.API.Models;

namespace RestaurantPOS.API.Repositories;

public class OrderRepository : IOrderRepository
{
    private readonly AppDbContext _db;
    public OrderRepository(AppDbContext db) => _db = db;

    public async Task<Order?> GetByIdAsync(int orderId) =>
        await _db.Orders
            .Include(o => o.DiningTable)
            .Include(o => o.OrderDetails).ThenInclude(od => od.Product)
            .Include(o => o.PaymentMethod)
            .FirstOrDefaultAsync(o => o.OrderID == orderId);

    public async Task<Order?> GetActiveByTableAsync(int tableId) =>
        await _db.Orders
            .FirstOrDefaultAsync(o => o.TableID == tableId && (o.Status == 0 || o.Status == 1));

    public async Task<Order> CreateAsync(Order order)
    {
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();
        return order;
    }

    public async Task UpdateAsync(Order order)
    {
        _db.Orders.Update(order);
        await _db.SaveChangesAsync();
    }

    public async Task<List<Order>> GetByDateRangeAsync(DateTime from, DateTime to) =>
        await _db.Orders
            .Where(o => o.Status == 2 && o.CheckoutAt >= from && o.CheckoutAt < to)
            .ToListAsync();
}
