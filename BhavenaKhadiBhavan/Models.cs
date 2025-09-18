using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BhavenaKhadiBhavan.Models
{
    /// <summary>
    /// Simple Product model for Khadi stores
    /// </summary>
    public class Product
    {
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(50)]
        public string? SKU { get; set; }

        [StringLength(100)]
        public string? FabricType { get; set; }

        [StringLength(50)]
        public string? Color { get; set; }

        [StringLength(20)]
        public string? Size { get; set; }

        [StringLength(50)]
        public string? Pattern { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        [Required]
        [Range(0.01, 999999.99)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal PurchasePrice { get; set; }

        [Required]
        [Range(0.01, 999999.99)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal SalePrice { get; set; }

        [Range(0, 100)]
        [Column(TypeName = "decimal(5,2)")]
        public decimal GSTRate { get; set; } = 5;

        // CRITICAL: Change from int to decimal for fractional quantities
        [Required]
        [Range(0, 999999.999)]
        [Column(TypeName = "decimal(10,3)")]  // Support up to 3 decimal places
        public decimal StockQuantity { get; set; }

        [Range(0, 999999.999)]
        [Column(TypeName = "decimal(10,3)")]
        public decimal MinimumStock { get; set; } = 5;

        // Unit of measurement for the product
        [StringLength(20)]
        public string? UnitOfMeasure { get; set; } = "Piece"; // Piece, Meter, Kg, etc.

        public bool IsActive { get; set; } = true;

        // Foreign Keys
        [Required]
        public int CategoryId { get; set; }

        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // Navigation Properties
        public virtual Category? Category { get; set; }
        public virtual ICollection<ReturnItem> ReturnItems { get; set; } = new List<ReturnItem>();

        // Computed Properties
        public string DisplayName => $"{Name}" +
            (!string.IsNullOrEmpty(Color) ? $" - {Color}" : "") +
            (!string.IsNullOrEmpty(Size) ? $" ({Size})" : "");

        [Column(TypeName = "decimal(18,2)")]
        public decimal PriceWithGST => SalePrice + (SalePrice * GSTRate / 100);

        public bool IsLowStock => StockQuantity <= MinimumStock;

        public string StockStatus => StockQuantity == 0 ? "Out of Stock" :
                                   IsLowStock ? "Low Stock" : "In Stock";

        [Column(TypeName = "decimal(5,2)")]
        public decimal ProfitMargin => SalePrice > 0 ? ((SalePrice - PurchasePrice) / SalePrice) * 100 : 0;

        // Display stock with proper formatting
        public string DisplayStock => $"{StockQuantity:0.###} {UnitOfMeasure}";
    }

    /// <summary>
    /// Simple Category model
    /// </summary>
    public class Category
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation Properties
        public virtual ICollection<Product> Products { get; set; } = new List<Product>();
    }

    /// <summary>
    /// Simple Customer model
    /// </summary>
    public class Customer
    {
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [Phone]
        [StringLength(15)]
        public string? Phone { get; set; }

        [EmailAddress]
        [StringLength(200)]
        public string? Email { get; set; }

        [StringLength(500)]
        public string? Address { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Calculated Fields
        public int TotalOrders { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalPurchases { get; set; }

        public DateTime? LastPurchaseDate { get; set; }

        // Computed Properties
        public string CustomerType
        {
            get
            {
                return TotalOrders switch
                {
                    0 => "New",
                    1 => "Second-time",
                    >= 2 and < 5 => "Regular",
                    >= 5 => "Loyal",
                    _ => "Unknown"
                };
            }
        }

        public int DaysSinceLastPurchase => LastPurchaseDate.HasValue ?
            (int)(DateTime.Now - LastPurchaseDate.Value).TotalDays : 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal AverageOrderValue => TotalOrders > 0 ? TotalPurchases / TotalOrders : 0;
    }

    /// <summary>
    /// Simple Sale model
    /// </summary>
    public class Sale
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string InvoiceNumber { get; set; } = string.Empty;

        [Required]
        public DateTime SaleDate { get; set; } = DateTime.Now;

        // Customer Information
        public int? CustomerId { get; set; }

        [StringLength(200)]
        public string? CustomerName { get; set; }

        [StringLength(15)]
        public string? CustomerPhone { get; set; }

        // Financial Fields
        [Required]
        [Range(0, 999999.99)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal SubTotal { get; set; }

        [Range(0, 999999.99)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal GSTAmount { get; set; }

        // Overall discount (maintained for backward compatibility)
        [Range(0, 100)]
        [Column(TypeName = "decimal(5,2)")]
        public decimal DiscountPercentage { get; set; }

        // CRITICAL: Total discount amount (sum of all item discounts)
        [Range(0, 999999.99)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountAmount { get; set; }

        [Required]
        [Range(0, 999999.99)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        // Payment Information
        [Required]
        [StringLength(50)]
        public string PaymentMethod { get; set; } = "Cash";

        [StringLength(100)]
        public string? PaymentReference { get; set; }

        [StringLength(20)]
        public string Status { get; set; } = "Completed";

        // Navigation Properties
        public virtual Customer? Customer { get; set; }
        public virtual ICollection<SaleItem> SaleItems { get; set; } = new List<SaleItem>();
        public virtual ICollection<Return> Returns { get; set; } = new List<Return>(); // ADDED

        // Computed Properties
        public string CustomerDisplayName => !string.IsNullOrEmpty(CustomerName) ?
            CustomerName : "Walk-in Customer";

        public decimal ItemCount => SaleItems?.Sum(i => i.Quantity) ?? 0;

        // Discount calculations
        [NotMapped]
        public decimal TotalItemDiscounts => SaleItems?.Sum(i => i.ItemDiscountAmount) ?? 0;

        [NotMapped]
        public bool HasItemLevelDiscounts => SaleItems?.Any(i => i.HasItemDiscount) ?? false;

        [NotMapped]
        public decimal EffectiveDiscountPercentage =>
            SubTotal > 0 ? (TotalItemDiscounts / SubTotal) * 100 : 0;
    }

    /// <summary>
    /// Sale Item model
    /// </summary>
    public class SaleItem
    {
        public int Id { get; set; }

        [Required]
        public int SaleId { get; set; }

        [Required]
        public int ProductId { get; set; }

        [Required]
        [StringLength(200)]
        public string ProductName { get; set; } = string.Empty;

        [Required]
        [Range(0.001, 999999.999)]
        [Column(TypeName = "decimal(10,3)")]
        public decimal Quantity { get; set; }

        [Required]
        [Range(0.01, 999999.99)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        [Range(0, 100)]
        [Column(TypeName = "decimal(5,2)")]
        public decimal GSTRate { get; set; }

        [Range(0, 999999.99)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal GSTAmount { get; set; }

        // CRITICAL: Individual item discount fields
        [Range(0, 100)]
        [Column(TypeName = "decimal(5,2)")]
        public decimal ItemDiscountPercentage { get; set; } = 0;

        [Range(0, 999999.99)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal ItemDiscountAmount { get; set; } = 0;

        [Range(0, 999999.99)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal LineTotal { get; set; }

        [Column(TypeName = "decimal(10,3)")]
        public decimal ReturnedQuantity { get; set; } = 0;

        [StringLength(20)]
        public string? UnitOfMeasure { get; set; } = "Piece";

        // Navigation Properties
        public virtual Sale? Sale { get; set; }
        public virtual Product? Product { get; set; }
        public virtual ICollection<ReturnItem> ReturnItems { get; set; } = new List<ReturnItem>();

        // Computed Properties
        public string DisplayQuantity => $"{Quantity:0.###} {UnitOfMeasure}";

        // Line calculations with individual discount
        [NotMapped]
        public decimal LineSubtotal => UnitPrice * Quantity;

        [NotMapped]
        public decimal LineSubtotalAfterDiscount => LineSubtotal - ItemDiscountAmount;

        [NotMapped]
        public decimal LineGSTAmount => LineSubtotalAfterDiscount * GSTRate / 100;

        [NotMapped]
        public decimal LineTotalWithDiscount => LineSubtotalAfterDiscount + LineGSTAmount;

        [NotMapped]
        public bool HasItemDiscount => ItemDiscountPercentage > 0 || ItemDiscountAmount > 0;

        [NotMapped]
        public string DiscountDisplay => HasItemDiscount ?
            $"{ItemDiscountPercentage:0.##}% (-₹{ItemDiscountAmount:N2})" : "No Discount";
    }

    public class Return
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string ReturnNumber { get; set; } = string.Empty;

        [Required]
        public int SaleId { get; set; }

        [Required]
        public DateTime ReturnDate { get; set; } = DateTime.Now;

        // Return Details
        [Required]
        [StringLength(500)]
        public string Reason { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Notes { get; set; }

        // CRITICAL FIX: Financial Details with correct property names
        [Required]
        [Range(0, 999999.99)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal SubTotal { get; set; } = 0;

        [Range(0, 999999.99)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalItemDiscounts { get; set; } = 0; // NOT DiscountAmount

        [Range(0, 999999.99)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal GSTAmount { get; set; } = 0;

        [Required]
        [Range(0, 999999.99)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal RefundAmount { get; set; } = 0; // NOT TotalAmount

        // Return Processing
        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Pending";

        [StringLength(50)]
        public string RefundMethod { get; set; } = "Cash";

        [StringLength(100)]
        public string? RefundReference { get; set; }

        public DateTime? ProcessedDate { get; set; }

        [StringLength(100)]
        public string? ProcessedBy { get; set; }

        // Navigation Properties
        public virtual Sale? Sale { get; set; }
        public virtual ICollection<ReturnItem> ReturnItems { get; set; } = new List<ReturnItem>();

        // Computed Properties
        [NotMapped]
        public string CustomerName => Sale?.CustomerDisplayName ?? "Walk-in Customer";

        [NotMapped]
        public string SaleInvoiceNumber => Sale?.InvoiceNumber ?? "";

        [NotMapped]
        public decimal ItemCount => ReturnItems?.Sum(i => i.ReturnQuantity) ?? 0;

        [NotMapped]
        public bool CanBeProcessed => Status == "Pending";

        [NotMapped]
        public bool IsCompleted => Status == "Completed";

        [NotMapped]
        public decimal EffectiveDiscountPercentage =>
            SubTotal > 0 ? (TotalItemDiscounts / SubTotal) * 100 : 0;

        // BACKWARD COMPATIBILITY: Add these properties if needed elsewhere in code
        [NotMapped]
        public decimal DiscountAmount => TotalItemDiscounts; // Alias for compatibility

        [NotMapped]
        public decimal TotalAmount => RefundAmount; // Alias for compatibility
    }

    /// <summary>
    /// Return item entity with proportional discount handling
    /// </summary>
    public class ReturnItem
    {
        public int Id { get; set; }

        [Required]
        public int ReturnId { get; set; }

        [Required]
        public int SaleItemId { get; set; }

        [Required]
        public int ProductId { get; set; }

        [Required]
        [StringLength(200)]
        public string ProductName { get; set; } = string.Empty;

        [Required]
        [Range(0.001, 999999.999)]
        [Column(TypeName = "decimal(10,3)")]
        public decimal ReturnQuantity { get; set; }

        [Required]
        [Range(0.01, 999999.99)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        [Range(0, 100)]
        [Column(TypeName = "decimal(5,2)")]
        public decimal GSTRate { get; set; }

        [Range(0, 999999.99)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal GSTAmount { get; set; }

        // CRITICAL FIX: Correct property names for proportional discount
        [Range(0, 100)]
        [Column(TypeName = "decimal(5,2)")]
        public decimal OriginalItemDiscountPercentage { get; set; } = 0;

        [Range(0, 999999.99)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal ProportionalDiscountAmount { get; set; } = 0; // NOT just "disc"

        [Range(0, 999999.99)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal LineTotal { get; set; }

        [StringLength(20)]
        public string? UnitOfMeasure { get; set; } = "Piece";

        // Return Processing Details
        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Pending";

        [StringLength(500)]
        public string? Condition { get; set; }

        // Navigation Properties
        public virtual Return? Return { get; set; }
        public virtual SaleItem? SaleItem { get; set; }
        public virtual Product? Product { get; set; }

        // Computed Properties
        [NotMapped]
        public string DisplayQuantity => $"{ReturnQuantity:0.###} {UnitOfMeasure}";

        [NotMapped]
        public decimal LineSubtotal => UnitPrice * ReturnQuantity;

        [NotMapped]
        public decimal LineAfterDiscount => LineSubtotal - ProportionalDiscountAmount;

        [NotMapped]
        public decimal LineGSTAmount => LineAfterDiscount * GSTRate / 100;

        [NotMapped]
        public decimal RefundLineTotal => LineAfterDiscount + LineGSTAmount;

        [NotMapped]
        public bool HasDiscount => ProportionalDiscountAmount > 0;

        [NotMapped]
        public string DiscountDisplay => HasDiscount ?
            $"{OriginalItemDiscountPercentage:0.##}% (-₹{ProportionalDiscountAmount:N2})" : "No Discount";
    }

    // =========================================
    // Return View Models
    // =========================================

    /// <summary>
    /// View model for creating returns
    /// </summary>
    public class CreateReturnViewModel
    {
        public Return Return { get; set; } = new Return();
        public Sale? Sale { get; set; }
        public List<ReturnableItemViewModel> ReturnableItems { get; set; } = new List<ReturnableItemViewModel>();
        public List<ReturnItemViewModel> SelectedItems { get; set; } = new List<ReturnItemViewModel>();
        
        // Summary calculations
        public decimal TotalRefundAmount { get; set; }
        public decimal TotalItemDiscounts { get; set; }
        public decimal TotalGSTAmount { get; set; }
    }

    /// <summary>
    /// View model for returnable items
    /// </summary>
    public class ReturnableItemViewModel
    {
        public int SaleItemId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal OriginalQuantity { get; set; }
        public decimal ReturnedQuantity { get; set; }
        public decimal ReturnableQuantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal GSTRate { get; set; }
        public string UnitOfMeasure { get; set; } = "Piece";
        
        // Original discount information
        public decimal OriginalItemDiscountPercentage { get; set; }
        public decimal OriginalItemDiscountAmount { get; set; }
        
        // Calculated properties
        public decimal MaxRefundSubtotal => UnitPrice * ReturnableQuantity;
        public decimal MaxProportionalDiscount => 
            ReturnableQuantity > 0 ? (OriginalItemDiscountAmount * ReturnableQuantity / OriginalQuantity) : 0;
        public decimal MaxRefundAfterDiscount => MaxRefundSubtotal - MaxProportionalDiscount;
        public decimal MaxRefundGST => MaxRefundAfterDiscount * GSTRate / 100;
        public decimal MaxRefundTotal => MaxRefundAfterDiscount + MaxRefundGST;
        
        public bool CanBeReturned => ReturnableQuantity > 0;
        public bool HasOriginalDiscount => OriginalItemDiscountPercentage > 0 || OriginalItemDiscountAmount > 0;
    }

    /// <summary>
    /// View model for return items in the return process
    /// </summary>
    public class ReturnItemViewModel
    {
        public int SaleItemId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal ReturnQuantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal GSTRate { get; set; }
        public string UnitOfMeasure { get; set; } = "Piece";
        public string Condition { get; set; } = "Good";
        public string? Notes { get; set; }
        
        // Discount calculations
        public decimal OriginalItemDiscountPercentage { get; set; }
        public decimal ProportionalDiscountAmount { get; set; }
        
        // Calculated properties
        public decimal LineSubtotal => UnitPrice * ReturnQuantity;
        public decimal LineAfterDiscount => LineSubtotal - ProportionalDiscountAmount;
        public decimal LineGST => LineAfterDiscount * GSTRate / 100;
        public decimal LineTotal => LineAfterDiscount + LineGST;
        
        public bool HasDiscount => ProportionalDiscountAmount > 0;
    }

    /// <summary>
    /// Return list view model
    /// </summary>
    public class ReturnsIndexViewModel
    {
        public List<Return> Returns { get; set; } = new List<Return>();
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string? SearchTerm { get; set; }
        public string? StatusFilter { get; set; }
        
        // Summary statistics
        public decimal TotalRefunds { get; set; }
        public int TotalReturns { get; set; }
        public decimal AverageRefundAmount { get; set; }
    }

    // =========================================
    // Return Helper Classes
    // =========================================

    /// <summary>
    /// Calculator for return amounts with discount proportions
    /// </summary>
    public static class ReturnCalculator
    {
        /// <summary>
        /// Calculate proportional discount for return quantity
        /// </summary>
        public static decimal CalculateProportionalDiscount(
            decimal originalQuantity,
            decimal returnQuantity,
            decimal originalDiscountAmount)
        {
            if (originalQuantity <= 0 || returnQuantity <= 0) return 0;
            return (originalDiscountAmount * returnQuantity) / originalQuantity;
        }
        
        /// <summary>
        /// Calculate return line total with discount
        /// </summary>
        public static (decimal subtotal, decimal discount, decimal afterDiscount, decimal gst, decimal total) 
            CalculateReturnLineTotal(
                decimal unitPrice,
                decimal quantity,
                decimal gstRate,
                decimal proportionalDiscount)
        {
            var subtotal = unitPrice * quantity;
            var afterDiscount = subtotal - proportionalDiscount;
            var gst = afterDiscount * gstRate / 100;
            var total = afterDiscount + gst;
            
            return (subtotal, proportionalDiscount, afterDiscount, gst, total);
        }
        
        /// <summary>
        /// Calculate total return amounts
        /// </summary>
        public static (decimal subtotal, decimal totalDiscounts, decimal totalGST, decimal refundAmount) 
            CalculateReturnTotals(List<ReturnItemViewModel> items)
        {
            var subtotal = items.Sum(i => i.LineSubtotal);
            var totalDiscounts = items.Sum(i => i.ProportionalDiscountAmount);
            var totalGST = items.Sum(i => i.LineGST);
            var refundAmount = items.Sum(i => i.LineTotal);
            
            return (subtotal, totalDiscounts, totalGST, refundAmount);
        }
        
        /// <summary>
        /// Validate return quantities against available quantities
        /// </summary>
        public static Dictionary<int, string> ValidateReturnQuantities(
            List<ReturnItemViewModel> returnItems,
            Dictionary<int, decimal> availableQuantities)
        {
            var errors = new Dictionary<int, string>();
            
            foreach (var item in returnItems)
            {
                if (!availableQuantities.ContainsKey(item.SaleItemId))
                {
                    errors[item.SaleItemId] = "Item not found in original sale";
                    continue;
                }
                
                var available = availableQuantities[item.SaleItemId];
                if (item.ReturnQuantity > available)
                {
                    errors[item.SaleItemId] = $"Cannot return {item.ReturnQuantity:0.###}. Available: {available:0.###}";
                }
                
                if (item.ReturnQuantity <= 0)
                {
                    errors[item.SaleItemId] = "Return quantity must be greater than 0";
                }
            }
            
            return errors;
        }
    }

    /// <summary>
    /// Return reason constants
    /// </summary>
    public static class ReturnReasons
    {
        public const string Defective = "Defective/Damaged";
        public const string WrongSize = "Wrong Size";
        public const string WrongColor = "Wrong Color";
        public const string CustomerChange = "Customer Changed Mind";
        public const string QualityIssue = "Quality Issue";
        public const string NotAsExpected = "Not As Expected";
        public const string Duplicate = "Duplicate Purchase";
        public const string Other = "Other";
        
        public static List<string> GetAllReasons()
        {
            return new List<string>
            {
                Defective,
                WrongSize,
                WrongColor,
                CustomerChange,
                QualityIssue,
                NotAsExpected,
                Duplicate,
                Other
            };
        }
    }

    /// <summary>
    /// Return status constants
    /// </summary>
    public static class ReturnStatus
    {
        public const string Pending = "Pending";
        public const string Approved = "Approved";
        public const string Processing = "Processing";
        public const string Completed = "Completed";
        public const string Cancelled = "Cancelled";
        public const string Rejected = "Rejected";
        
        public static List<string> GetAllStatuses()
        {
            return new List<string>
            {
                Pending,
                Approved,
                Processing,
                Completed,
                Cancelled,
                Rejected
            };
        }
    }

    /// <summary>
    /// Item condition constants
    /// </summary>
    public static class ItemCondition
    {
        public const string Good = "Good";
        public const string Minor = "Minor Wear";
        public const string Damaged = "Damaged";
        public const string Defective = "Defective";
        public const string Unusable = "Unusable";
        
        public static List<string> GetAllConditions()
        {
            return new List<string>
            {
                Good,
                Minor,
                Damaged,
                Defective,
                Unusable
            };
        }
    }

    /// <summary>
    /// Simple User model for basic authentication
    /// </summary>
    public class User
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Username is required")]
        [StringLength(50, ErrorMessage = "Username cannot exceed 50 characters")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Full name is required")]
        [StringLength(100, ErrorMessage = "Full name cannot exceed 100 characters")]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [StringLength(100, ErrorMessage = "Email cannot exceed 100 characters")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(255)]
        public string PasswordHash { get; set; } = string.Empty;

        [StringLength(20)]
        public string Role { get; set; } = "Staff"; // Admin, Staff

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Last Login")]
        public DateTime? LastLogin { get; set; }

        [Display(Name = "Created Date")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Computed Properties
        public bool IsAdmin => Role == "Admin";
        public string RoleDisplayName => Role;
        public string StatusDisplayName => IsActive ? "Active" : "Inactive";
    }

    /// <summary>
    /// Simple settings model for store configuration
    /// </summary>
    public class Setting
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Key { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Value { get; set; }

        [StringLength(100)]
        public string Category { get; set; } = "General";

        [StringLength(200)]
        public string? Description { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// View Models for UI
    /// </summary>
    public class DashboardViewModel
    {
        [Column(TypeName = "decimal(18,2)")]
        public decimal TodaySales { get; set; }

        public int TodayOrders { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal MonthSales { get; set; }

        public int TotalCustomers { get; set; }
        public int LowStockCount { get; set; }

        public List<Product> LowStockProducts { get; set; } = new List<Product>();
        public List<Sale> RecentSales { get; set; } = new List<Sale>();
        public List<Customer> RecentCustomers { get; set; } = new List<Customer>();
    }

    public class SalesViewModel
    {
        public Sale Sale { get; set; } = new Sale();
        public List<Category> Categories { get; set; } = new List<Category>();
        public List<Product> Products { get; set; } = new List<Product>();
        public List<Customer> Customers { get; set; } = new List<Customer>();
        public List<SaleItemViewModel> CartItems { get; set; } = new List<SaleItemViewModel>();

        // Cart Totals
        [Column(TypeName = "decimal(18,2)")]
        public decimal CartSubtotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CartGST { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CartDiscount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CartTotal { get; set; }
    }

    public class SaleItemViewModel
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal GSTRate { get; set; }
        public string UnitOfMeasure { get; set; } = "Piece";

        // Individual item discount
        public decimal ItemDiscountPercentage { get; set; } = 0;
        public decimal ItemDiscountAmount { get; set; } = 0;

        // Calculated properties
        public decimal LineSubtotal => UnitPrice * Quantity;
        public decimal LineSubtotalAfterDiscount => LineSubtotal - ItemDiscountAmount;
        public decimal LineGST => LineSubtotalAfterDiscount * GSTRate / 100;
        public decimal LineTotal => LineSubtotalAfterDiscount + LineGST;

        public bool HasDiscount => ItemDiscountPercentage > 0 || ItemDiscountAmount > 0;
        public string DiscountDisplay => HasDiscount ?
            $"{ItemDiscountPercentage:0.##}% (-₹{ItemDiscountAmount:N2})" : "";

        // Convert to SaleItem entity
        public SaleItem ToSaleItem()
        {
            return new SaleItem
            {
                ProductId = ProductId,
                ProductName = ProductName,
                Quantity = Quantity,
                UnitPrice = UnitPrice,
                GSTRate = GSTRate,
                UnitOfMeasure = UnitOfMeasure,
                ItemDiscountPercentage = ItemDiscountPercentage,
                ItemDiscountAmount = ItemDiscountAmount,
                GSTAmount = LineGST,
                LineTotal = LineTotal
            };
        }

        // Create from SaleItem entity
        public static SaleItemViewModel FromSaleItem(SaleItem saleItem)
        {
            return new SaleItemViewModel
            {
                ProductId = saleItem.ProductId,
                ProductName = saleItem.ProductName,
                Quantity = saleItem.Quantity,
                UnitPrice = saleItem.UnitPrice,
                GSTRate = saleItem.GSTRate,
                UnitOfMeasure = saleItem.UnitOfMeasure ?? "Piece",
                ItemDiscountPercentage = saleItem.ItemDiscountPercentage,
                ItemDiscountAmount = saleItem.ItemDiscountAmount
            };
        }
    }

    public class ReportsViewModel
    {
        public DateTime FromDate { get; set; } = DateTime.Today.AddDays(-30);
        public DateTime ToDate { get; set; } = DateTime.Today;

        // Sales Report
        public decimal TotalSales { get; set; }
        public int TotalOrders { get; set; }
        public decimal AverageOrderValue { get; set; }
        public List<Sale> Sales { get; set; } = new();

        // Stock Report
        public List<Product> Products { get; set; } = new();
        public List<Product> LowStockProducts { get; set; } = new();
        public decimal TotalStockValue { get; set; }

        // Customer Report  
        public List<Customer> TopCustomers { get; set; } = new();
        public int NewCustomers { get; set; }

        // GST Report
        public decimal TotalGST { get; set; }
        public Dictionary<decimal, decimal> GSTBreakdown { get; set; } = new();
    }
    public class ErrorViewModel
    {
        public string? RequestId { get; set; }

        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }
}