using BhavenaKhadiBhavan.Models;

namespace BhavenaKhadiBhavan.Services
{
    public interface ISalesService
    {
        Task<Sale> CreateSaleAsync(Sale sale, List<SaleItem> items);
        Task<Sale?> GetSaleByIdAsync(int id);
        Task<Sale?> GetSaleByInvoiceNumberAsync(string invoiceNumber);
        Task<List<Sale>> GetSalesByDateAsync(DateTime date);
        Task<List<Sale>> GetSalesAsync(DateTime? fromDate = null, DateTime? toDate = null);
        Task<string> GenerateInvoiceNumberAsync();
        Task<decimal> CalculateGSTAmountAsync(List<SaleItem> items);
        Task<bool> ProcessSaleCompletionAsync(int saleId);
        Task<Dictionary<int, decimal>> GetReturnableQuantitiesAsync(int saleId);
    }
}
