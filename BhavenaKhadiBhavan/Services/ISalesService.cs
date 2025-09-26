using BhavenaKhadiBhavan.Models;

namespace BhavenaKhadiBhavan.Services
{
    public interface ISalesService
    {
        // CRITICAL: Updated method signatures for item-level discount support
        Task<Sale> CreateSaleAsync(Sale sale, List<SaleItem> saleItems);
        Task<Sale> CreateSaleFromCartAsync(Sale sale, List<CartItemViewModel> cartItems);

        Task<Sale?> GetSaleByIdAsync(int id);
        Task<Sale?> GetSaleByInvoiceNumberAsync(string invoiceNumber);
        Task<List<Sale>> GetSalesByDateAsync(DateTime date);
        Task<List<Sale>> GetSalesAsync(DateTime? fromDate = null, DateTime? toDate = null);

        Task<string> GenerateInvoiceNumberAsync();
        Task<decimal> CalculateGSTAmountAsync(List<SaleItem> items);
        Task<bool> ProcessSaleCompletionAsync(int saleId);
        Task<Dictionary<int, decimal>> GetReturnableQuantitiesAsync(int saleId);

        // NEW: Item-Level Discount Methods
        Task<bool> ApplyOverallDiscountAsync(int saleId, decimal discountPercentage);
        Task<DiscountOperationResult> ApplyItemDiscountAsync(int saleItemId, decimal discountPercentage);
        Task<DiscountOperationResult> RemoveItemDiscountAsync(int saleItemId);
        Task<object> GetSaleDiscountSummaryAsync(int saleId);

        // NEW: Cart calculation with discounts
        CartTotals CalculateCartTotalsWithDiscounts(List<CartItemViewModel> cartItems);
        Task<bool> UpdateSaleStatusAsync(int saleId, string status);
    }
}
