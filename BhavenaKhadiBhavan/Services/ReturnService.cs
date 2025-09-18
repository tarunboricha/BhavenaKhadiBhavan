using BhavenaKhadiBhavan.Data;
using BhavenaKhadiBhavan.Models;
using Microsoft.EntityFrameworkCore;

namespace BhavenaKhadiBhavan.Services
{
    public class ReturnService : IReturnService
    {
        private readonly ApplicationDbContext _context;
        private readonly IProductService _productService;
        private readonly ICustomerService _customerService;

        public ReturnService(ApplicationDbContext context, IProductService productService, ICustomerService customerService)
        {
            _context = context;
            _productService = productService;
            _customerService = customerService;
        }

        public async Task<Return> CreateReturnAsync(Return returnRecord, List<ReturnItem> returnItems)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Generate return number
                returnRecord.ReturnNumber = await GenerateReturnNumberAsync();
                returnRecord.ReturnDate = DateTime.Now;

                // Calculate totals
                decimal subtotal = 0;
                decimal totalGST = 0;

                foreach (var item in returnItems)
                {
                    var itemSubtotal = item.UnitPrice * item.ReturnQuantity;
                    var itemGST = itemSubtotal * item.GSTRate / 100;
                    var itemTotal = itemSubtotal + itemGST;

                    item.GSTAmount = itemGST;
                    item.LineTotal = itemTotal;

                    subtotal += itemSubtotal;
                    totalGST += itemGST;
                }

                returnRecord.SubTotal = subtotal;
                returnRecord.GSTAmount = totalGST;
                returnRecord.TotalAmount = subtotal + totalGST;

                _context.Returns.Add(returnRecord);
                await _context.SaveChangesAsync();

                // Add return items and update stock
                foreach (var item in returnItems)
                {
                    item.ReturnId = returnRecord.Id;

                    // Update product stock
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product != null)
                    {
                        product.StockQuantity += item.ReturnQuantity; // Add back to stock
                        product.UpdatedAt = DateTime.Now;
                    }

                    // Update sale item returned quantity
                    var saleItem = await _context.SaleItems.FindAsync(item.SaleItemId);
                    if (saleItem != null)
                    {
                        saleItem.ReturnedQuantity += item.ReturnQuantity;
                    }

                    _context.ReturnItems.Add(item);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return returnRecord;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<Return?> GetReturnByIdAsync(int id)
        {
            return await _context.Returns
                .Include(r => r.Sale)
                .Include(r => r.ReturnItems)
                .ThenInclude(ri => ri.Product)
                .FirstOrDefaultAsync(r => r.Id == id);
        }

        public async Task<List<Return>> GetReturnsAsync(DateTime? fromDate = null, DateTime? toDate = null)
        {
            var query = _context.Returns
                .Include(r => r.Sale)
                .Include(r => r.ReturnItems)
                .AsQueryable();

            if (fromDate.HasValue)
            {
                query = query.Where(r => r.ReturnDate.Date >= fromDate.Value.Date);
            }

            if (toDate.HasValue)
            {
                query = query.Where(r => r.ReturnDate.Date <= toDate.Value.Date);
            }

            return await query
                .OrderByDescending(r => r.ReturnDate)
                .ToListAsync();
        }

        private async Task<string> GenerateReturnNumberAsync()
        {
            var today = DateTime.Today;
            var prefix = $"RET{today:yyyyMMdd}";
            
            var lastReturn = await _context.Returns
                .Where(r => r.ReturnNumber.StartsWith(prefix))
                .OrderByDescending(r => r.ReturnNumber)
                .FirstOrDefaultAsync();

            int sequence = 1;
            if (lastReturn != null && lastReturn.ReturnNumber.Length > prefix.Length)
            {
                var lastSequence = lastReturn.ReturnNumber.Substring(prefix.Length);
                if (int.TryParse(lastSequence, out int lastNum))
                {
                    sequence = lastNum + 1;
                }
            }

            return $"{prefix}{sequence:D3}";
        }

        public async Task<bool> ProcessReturnAsync(int returnId)
        {
            var returnEntity = await GetReturnByIdAsync(returnId);
            if (returnEntity == null) return false;

            returnEntity.Status = "Completed";
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
