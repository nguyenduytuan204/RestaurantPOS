using Microsoft.EntityFrameworkCore;
using RestaurantPOS.API.Data;
using RestaurantPOS.API.Models;

namespace RestaurantPOS.API.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly AppDbContext _db;
    public ProductRepository(AppDbContext db) => _db = db;

    public async Task<List<Product>> GetAllAsync(int? categoryId = null)
    {
        var query = _db.Products.Include(p => p.Category)
            .Where(p => p.IsActive).AsQueryable();
        if (categoryId.HasValue) query = query.Where(p => p.CategoryID == categoryId.Value);
        return await query.OrderBy(p => p.Category.SortOrder).ThenBy(p => p.ProductName).ToListAsync();
    }

    public async Task<Product?> GetByIdAsync(int productId) =>
        await _db.Products.Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.ProductID == productId);

    public async Task<Product> CreateAsync(Product product)
    {
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        return product;
    }

    public async Task UpdateAsync(Product product)
    {
        _db.Products.Update(product);
        await _db.SaveChangesAsync();
    }
}
