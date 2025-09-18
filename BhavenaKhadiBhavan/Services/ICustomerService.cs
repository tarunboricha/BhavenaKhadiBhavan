using BhavenaKhadiBhavan.Models;

namespace BhavenaKhadiBhavan.Services
{
    public interface ICustomerService
    {
        Task<List<Customer>> GetAllCustomersAsync();
        Task<Customer?> GetCustomerByIdAsync(int id);
        Task<Customer?> GetCustomerByPhoneAsync(string phone);
        Task<Customer> CreateCustomerAsync(Customer customer);
        Task<Customer> UpdateCustomerAsync(Customer customer);
        Task<List<Customer>> SearchCustomersAsync(string searchTerm);
        Task<List<Sale>> GetCustomerSalesAsync(int customerId);
        Task<bool> UpdateCustomerTotalsAsync(int customerId);
    }
}
