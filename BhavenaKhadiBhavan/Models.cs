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
    /// Enhanced SaleItem model with individual item-level discounts
    /// </summary>
    public partial class SaleItem
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

        [Range(0, 999999.99)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal LineTotal { get; set; }

        [Column(TypeName = "decimal(10,3)")]
        public decimal ReturnedQuantity { get; set; } = 0;

        [StringLength(20)]
        public string? UnitOfMeasure { get; set; } = "Piece";

        // CRITICAL: Enhanced item-level discount fields
        [Range(0, 100)]
        [Column(TypeName = "decimal(5,2)")]
        public decimal ItemDiscountPercentage { get; set; } = 0;

        [Range(0, 999999.99)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal ItemDiscountAmount { get; set; } = 0;

        // Navigation Properties
        public virtual Sale? Sale { get; set; }
        public virtual Product? Product { get; set; }
        public virtual ICollection<ReturnItem> ReturnItems { get; set; } = new List<ReturnItem>();

        // Enhanced Computed Properties for Item-Level Discounts
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

        [NotMapped]
        public decimal ReturnableQuantity => Quantity - ReturnedQuantity;

        [NotMapped]
        public bool CanBeReturned => ReturnableQuantity > 0;

        [NotMapped]
        public string DisplayQuantity => $"{Quantity:0.###} {UnitOfMeasure}";

        // CRITICAL: Methods for discount calculations
        public void ApplyDiscountPercentage(decimal discountPercentage)
        {
            ItemDiscountPercentage = discountPercentage;
            ItemDiscountAmount = LineSubtotal * discountPercentage / 100;
        }

        public void ApplyDiscountAmount(decimal discountAmount)
        {
            ItemDiscountAmount = discountAmount;
            ItemDiscountPercentage = LineSubtotal > 0 ? (discountAmount / LineSubtotal) * 100 : 0;
        }

        public void ClearDiscount()
        {
            ItemDiscountPercentage = 0;
            ItemDiscountAmount = 0;
        }
    }

    /// <summary>
    /// Enhanced Sale model with computed discount properties (hiding overall discount percentage)
    /// </summary>
    public partial class Sale
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "Invoice Number")]
        public string InvoiceNumber { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Sale Date")]
        public DateTime SaleDate { get; set; } = DateTime.Now;

        [Display(Name = "Customer")]
        public int? CustomerId { get; set; }

        [StringLength(100, ErrorMessage = "Customer name cannot exceed 100 characters")]
        [Display(Name = "Customer Name")]
        public string? CustomerName { get; set; }

        [StringLength(15, ErrorMessage = "Phone number cannot exceed 15 characters")]
        [Display(Name = "Customer Phone")]
        public string? CustomerPhone { get; set; }

        [Required]
        [Display(Name = "Payment Method")]
        public string PaymentMethod { get; set; } = "Cash";

        [Display(Name = "Payment Reference")]
        public string? PaymentReference { get; set; }

        [Display(Name = "Subtotal (₹)")]
        public decimal SubTotal { get; set; }

        [Display(Name = "GST Amount (₹)")]
        public decimal GSTAmount { get; set; }

        // CRITICAL: Keep for backward compatibility but hide in UI
        [Range(0, 100, ErrorMessage = "Discount percentage must be between 0 and 100")]
        [Display(Name = "Overall Discount (%)")]
        public decimal DiscountPercentage { get; set; } = 0;

        [Display(Name = "Discount Amount (₹)")]
        public decimal DiscountAmount { get; set; }

        [Display(Name = "Total Amount (₹)")]
        public decimal TotalAmount { get; set; }

        [StringLength(20)]
        public string Status { get; set; } = "Completed";

        [StringLength(300)]
        public string? Notes { get; set; }

        // Navigation Properties
        public virtual Customer? Customer { get; set; }
        public virtual ICollection<SaleItem> SaleItems { get; set; } = new List<SaleItem>();
        public virtual ICollection<Return> Returns { get; set; } = new List<Return>();

        // ENHANCED: Computed Properties for Item-Level Discounts
        [NotMapped]
        public string CustomerDisplayName => CustomerName ?? Customer?.Name ?? "Walk-in Customer";

        [NotMapped]
        public decimal ItemCount => SaleItems?.Sum(si => si.Quantity) ?? 0;

        [NotMapped]
        public bool HasCustomer => CustomerId.HasValue || !string.IsNullOrEmpty(CustomerName);

        [NotMapped]
        public decimal ReturnedAmount => Returns?.Where(r => r.Status == "Completed").Sum(r => r.TotalAmount) ?? 0;

        [NotMapped]
        public decimal NetAmount => TotalAmount - ReturnedAmount;

        // CRITICAL: New item-level discount computed properties
        [NotMapped]
        public decimal TotalItemDiscounts => SaleItems?.Sum(i => i.ItemDiscountAmount) ?? 0;

        [NotMapped]
        public bool HasItemLevelDiscounts => SaleItems?.Any(i => i.HasItemDiscount) ?? false;

        [NotMapped]
        public decimal EffectiveDiscountPercentage =>
            SubTotal > 0 ? (TotalItemDiscounts / SubTotal) * 100 : 0;

        [NotMapped]
        public int ItemsWithDiscountCount => SaleItems?.Count(i => i.HasItemDiscount) ?? 0;

        [NotMapped]
        public decimal AverageDiscountPercentage =>
            ItemsWithDiscountCount > 0 ?
            SaleItems?.Where(i => i.HasItemDiscount).Average(i => i.ItemDiscountPercentage) ?? 0 : 0;

        // CRITICAL: Methods for applying overall discounts to individual items
        public void ApplyOverallDiscountToItems(decimal overallDiscountPercentage)
        {
            if (SaleItems == null) return;

            foreach (var item in SaleItems)
            {
                item.ApplyDiscountPercentage(overallDiscountPercentage);
            }

            // Update overall discount fields for compatibility
            DiscountPercentage = overallDiscountPercentage;
            RecalculateTotals();
        }

        public void RecalculateTotals()
        {
            if (SaleItems == null) return;

            SubTotal = SaleItems.Sum(i => i.LineSubtotal);
            DiscountAmount = SaleItems.Sum(i => i.ItemDiscountAmount);
            GSTAmount = SaleItems.Sum(i => i.LineGSTAmount);
            TotalAmount = SaleItems.Sum(i => i.LineTotalWithDiscount);
        }

        public void ClearAllItemDiscounts()
        {
            if (SaleItems == null) return;

            foreach (var item in SaleItems)
            {
                item.ClearDiscount();
            }

            DiscountPercentage = 0;
            RecalculateTotals();
        }
    }

    /// <summary>
    /// Enhanced view model for cart items with discount support
    /// </summary>
    public class CartItemViewModel
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal GSTRate { get; set; }
        public string? UnitOfMeasure { get; set; } = "Piece";

        // Item-level discount fields
        public decimal ItemDiscountPercentage { get; set; } = 0;
        public decimal ItemDiscountAmount { get; set; } = 0;

        // Computed properties
        public decimal LineSubtotal => UnitPrice * Quantity;
        public decimal LineSubtotalAfterDiscount => LineSubtotal - ItemDiscountAmount;
        public decimal LineGSTAmount => LineSubtotalAfterDiscount * GSTRate / 100;
        public decimal LineTotalWithDiscount => LineSubtotalAfterDiscount + LineGSTAmount;
        public bool HasDiscount => ItemDiscountPercentage > 0 || ItemDiscountAmount > 0;

        // Methods for discount manipulation
        public void ApplyDiscountPercentage(decimal discountPercentage)
        {
            ItemDiscountPercentage = discountPercentage;
            ItemDiscountAmount = LineSubtotal * discountPercentage / 100;
        }

        public void ApplyDiscountAmount(decimal discountAmount)
        {
            ItemDiscountAmount = discountAmount;
            ItemDiscountPercentage = LineSubtotal > 0 ? (discountAmount / LineSubtotal) * 100 : 0;
        }
    }

    /// <summary>
    /// Enhanced sales view model with item-level discount support
    /// </summary>
    public class SalesViewModel
    {
        public Sale Sale { get; set; } = new Sale();
        public List<Category> Categories { get; set; } = new List<Category>();
        public List<Product> Products { get; set; } = new List<Product>();
        public List<Customer> Customers { get; set; } = new List<Customer>();
        public List<CartItemViewModel> CartItems { get; set; } = new List<CartItemViewModel>();

        // ENHANCED: Cart totals with item-level discounts
        [Column(TypeName = "decimal(18,2)")]
        public decimal CartSubtotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CartDiscountAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CartGST { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CartTotal { get; set; }

        // Computed properties for discounts
        public bool HasItemDiscounts => CartItems.Any(i => i.HasDiscount);
        public int ItemsWithDiscountCount => CartItems.Count(i => i.HasDiscount);
        public decimal EffectiveDiscountPercentage =>
            CartSubtotal > 0 ? (CartDiscountAmount / CartSubtotal) * 100 : 0;
    }

    /// <summary>
    /// Discount operation result
    /// </summary>
    public class DiscountOperationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public decimal NewDiscountAmount { get; set; }
        public decimal NewDiscountPercentage { get; set; }
        public decimal NewLineTotal { get; set; }
        public decimal NewCartTotal { get; set; }
    }

    /// <summary>
    /// Simple Return model
    /// </summary>
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

        [Required]
        [StringLength(200)]
        public string Reason { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Notes { get; set; }

        [Required]
        [Range(0, 999999.99)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal SubTotal { get; set; }

        [Range(0, 999999.99)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal GSTAmount { get; set; }

        [Range(0, 999999.99)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountAmount { get; set; }

        [Required]
        [Range(0, 999999.99)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [StringLength(20)]
        public string Status { get; set; } = "Completed";

        // Navigation Properties
        public virtual Sale? Sale { get; set; }
        public virtual ICollection<ReturnItem> ReturnItems { get; set; } = new List<ReturnItem>();

        // Computed Properties
        public string CustomerName => Sale?.CustomerDisplayName ?? "Unknown";
    }

    /// <summary>
    /// Return Item model
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

        // CRITICAL: Change from int to decimal
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

        [Range(0, 999999.99)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountAmount { get; set; }

        [Range(0, 999999.99)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal LineTotal { get; set; }

        [StringLength(20)]
        public string? UnitOfMeasure { get; set; } = "Piece";

        // Navigation Properties
        public virtual Return? Return { get; set; }
        public virtual SaleItem? SaleItem { get; set; }
        public virtual Product? Product { get; set; }

        public string DisplayQuantity => $"{ReturnQuantity:0.###} {UnitOfMeasure}";
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