using RestaurantPOS.API.Models;

namespace RestaurantPOS.API.Repositories;

public interface IProductRepository
{
    Task<List<Product>> GetAllAsync(int? categoryId = null);
    Task<Product?>      GetByIdAsync(int productId);
    Task<Product>       CreateAsync(Product product);
    Task                UpdateAsync(Product product);
}
