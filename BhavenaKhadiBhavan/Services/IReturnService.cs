using BhavenaKhadiBhavan.Models;

namespace BhavenaKhadiBhavan.Services
{
    public interface IReturnService
    {
        Task<List<Return>> GetReturnsAsync(DateTime? fromDate = null, DateTime? toDate = null, string? search = null, string? status = null);
        Task<Return?> GetReturnByIdAsync(int id);
        Task<Return> CreateReturnAsync(Return returnTransaction, List<ReturnItemViewModel> returnItems);
        Task<ReturnProcessResult> ProcessReturnAsync(int returnId, string refundMethod, string? refundReference, string? notes, string? processedBy);
        Task<ReturnProcessResult> CancelReturnAsync(int returnId, string reason, string? cancelledBy);
        Task<List<ReturnableItemViewModel>> GetReturnableItemsAsync(int saleId);
        Task<Dictionary<int, decimal>> GetAvailableQuantitiesForReturnAsync(int saleId);
    }

    public class ReturnProcessResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public decimal RefundAmount { get; set; }
        public string? ReturnNumber { get; set; }
    }
}
