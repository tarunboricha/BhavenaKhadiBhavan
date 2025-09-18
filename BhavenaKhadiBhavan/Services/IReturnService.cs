using BhavenaKhadiBhavan.Models;

namespace BhavenaKhadiBhavan.Services
{
    public interface IReturnService
    {
        Task<Return> CreateReturnAsync(Return returnEntity, List<ReturnItem> items);
        Task<Return?> GetReturnByIdAsync(int id);
        Task<List<Return>> GetReturnsAsync(DateTime? fromDate = null, DateTime? toDate = null);
        Task<bool> ProcessReturnAsync(int returnId);
    }
}
