using BhavenaKhadiBhavan.Models;

namespace BhavenaKhadiBhavan.Services
{
    public interface IReturnService
    {
        // Core return operations
        Task<Return> CreateReturnAsync(Return returnEntity, List<ReturnItem> items);
        Task<Return?> GetReturnByIdAsync(int id);
        Task<List<Return>> GetReturnsAsync(DateTime? fromDate = null, DateTime? toDate = null);
        Task<bool> ProcessReturnAsync(int returnId);

        // CRITICAL: Methods for proper partial quantity handling
        Task<Dictionary<int, decimal>> GetReturnableQuantitiesAsync(int saleId);
        Task<List<ReturnableItemInfo>> GetReturnableItemsAsync(int saleId);
        Task<ReturnCalculationResult> CalculateReturnTotalsAsync(int saleId, Dictionary<int, decimal> returnQuantities);

        // Validation methods
        Task<bool> ValidateReturnQuantitiesAsync(int saleId, Dictionary<int, decimal> returnQuantities);
        Task<string> GetNextReturnNumberAsync();
    }

    public class ReturnableItemInfo
    {
        public int SaleItemId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal OriginalQuantity { get; set; }
        public decimal AlreadyReturnedQuantity { get; set; }
        public decimal ReturnableQuantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal GSTRate { get; set; }
        public string UnitOfMeasure { get; set; } = "Piece";

        // CRITICAL: Individual item discount information
        public decimal ItemDiscountPercentage { get; set; }
        public decimal ItemDiscountAmount { get; set; }

        public bool CanBeReturned => ReturnableQuantity > 0;
        public string DisplayText => $"{ProductName} ({ReturnableQuantity:0.###} {UnitOfMeasure} available)";
    }

    /// <summary>
    /// ENHANCED: Return calculation result with individual discount tracking
    /// </summary>
    public class ReturnCalculationResult
    {
        public decimal SubTotal { get; set; }
        public decimal GSTAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public List<ReturnItemCalculation> Items { get; set; } = new List<ReturnItemCalculation>();
    }

    /// <summary>
    /// ENHANCED: Individual return item calculation with discount details
    /// </summary>
    public class ReturnItemCalculation
    {
        public int SaleItemId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal ReturnQuantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineSubtotal { get; set; }
        public decimal LineGST { get; set; }
        public decimal LineDiscount { get; set; }
        public decimal LineTotal { get; set; }
        public string UnitOfMeasure { get; set; } = "Piece";

        // CRITICAL: Individual discount tracking
        public decimal ItemDiscountPercentage { get; set; }
        public decimal ItemDiscountAmount { get; set; }
    }
}
