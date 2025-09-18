using BhavenaKhadiBhavan.Models;

namespace BhavenaKhadiBhavan.Services
{
    public interface IProductService
    {
        Task<List<Product>> GetAllProductsAsync();
        Task<List<Product>> GetActiveProductsAsync();
        Task<List<Product>> GetProductsByCategoryAsync(int categoryId);
        Task<List<Product>> SearchProductsAsync(string searchTerm);
        Task<Product?> GetProductByIdAsync(int id);
        Task<Product> CreateProductAsync(Product product);
        Task<Product> UpdateProductAsync(Product product);
        Task<bool> DeleteProductAsync(int id);
        Task<List<Product>> GetLowStockProductsAsync();
        Task<bool> UpdateStockAsync(int productId, int newStock);
        Task<bool> IncrementStockAsync(int productId, int quantity);
        Task<bool> UpdateStockAsync(int productId, decimal newQuantity);
    }
}
