using BhavenaKhadiBhavan.Data;
using BhavenaKhadiBhavan.Models;
using Microsoft.EntityFrameworkCore;

namespace BhavenaKhadiBhavan.Services
{
    public class CustomerService : ICustomerService
    {
        private readonly ApplicationDbContext _context;

        public CustomerService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Customer>> GetAllCustomersAsync()
        {
            return await _context.Customers
                .OrderBy(c => c.Name)
                .ToListAsync();
        }

        public async Task<Customer?> GetCustomerByIdAsync(int id)
        {
            return await _context.Customers.FindAsync(id);
        }

        public async Task<Customer?> GetCustomerByPhoneAsync(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return null;

            return await _context.Customers
                .FirstOrDefaultAsync(c => c.Phone == phone.Trim());
        }

        public async Task<Customer> CreateCustomerAsync(Customer customer)
        {
            customer.CreatedAt = DateTime.Now;
            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();
            return customer;
        }

        public async Task<Customer> UpdateCustomerAsync(Customer customer)
        {
            _context.Customers.Update(customer);
            await _context.SaveChangesAsync();
            return customer;
        }

        public async Task<List<Customer>> SearchCustomersAsync(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return await GetAllCustomersAsync();

            searchTerm = searchTerm.ToLower();
            return await _context.Customers
                .Where(c => c.Name.ToLower().Contains(searchTerm) ||
                           c.Phone!.Contains(searchTerm) ||
                           c.Email!.ToLower().Contains(searchTerm))
                .OrderBy(c => c.Name)
                .ToListAsync();
        }

        public async Task<List<Sale>> GetCustomerSalesAsync(int customerId)
        {
            return await _context.Sales
                .Include(s => s.SaleItems)
                .Where(s => s.CustomerId == customerId)
                .OrderByDescending(s => s.SaleDate)
                .ToListAsync();
        }

        public async Task<bool> UpdateCustomerTotalsAsync(int customerId)
        {
            var customer = await _context.Customers.FindAsync(customerId);
            if (customer == null) return false;

            var sales = await _context.Sales
                .Where(s => s.CustomerId == customerId && s.Status == "Completed")
                .ToListAsync();

            customer.TotalOrders = sales.Count;
            customer.TotalPurchases = sales.Sum(s => s.TotalAmount);
            customer.LastPurchaseDate = sales.Any() ? sales.Max(s => s.SaleDate) : null;

            await _context.SaveChangesAsync();
            return true;
        }
    }
}
