using Microsoft.EntityFrameworkCore;
using BhavenaKhadiBhavan.Models;

namespace BhavenaKhadiBhavan.Data
{
    /// <summary>
    /// FIXED: ApplicationDbContext with proper item-level discount configuration and corrected seed data
    /// </summary>
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        // DbSets for all entities
        public DbSet<Product> Products { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Sale> Sales { get; set; }
        public DbSet<SaleItem> SaleItems { get; set; }
        public DbSet<Return> Returns { get; set; }
        public DbSet<ReturnItem> ReturnItems { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Setting> Settings { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure decimal precision for Indian Rupee amounts
            modelBuilder.Entity<Product>(entity =>
            {
                entity.Property(p => p.PurchasePrice).HasPrecision(18, 2);
                entity.Property(p => p.SalePrice).HasPrecision(18, 2);
                entity.Property(p => p.GSTRate).HasPrecision(5, 2);
                entity.Property(p => p.StockQuantity).HasPrecision(10, 3);
                entity.Property(p => p.MinimumStock).HasPrecision(10, 3);

                // Index for better performance
                entity.HasIndex(p => p.Name);
                entity.HasIndex(p => p.SKU);
                entity.HasIndex(p => p.IsActive);
                entity.HasIndex(p => p.CategoryId);
            });

            modelBuilder.Entity<Category>(entity =>
            {
                entity.HasIndex(c => c.Name).IsUnique();
                entity.HasIndex(c => c.IsActive);
            });

            modelBuilder.Entity<Customer>(entity =>
            {
                entity.Property(c => c.TotalPurchases).HasPrecision(18, 2);

                // Index for searching customers
                entity.HasIndex(c => c.Name);
                entity.HasIndex(c => c.Phone);
                entity.HasIndex(c => c.Email);
            });

            modelBuilder.Entity<Sale>(entity =>
            {
                entity.Property(s => s.SubTotal).HasPrecision(18, 2);
                entity.Property(s => s.GSTAmount).HasPrecision(18, 2);
                entity.Property(s => s.DiscountPercentage).HasPrecision(5, 2);
                entity.Property(s => s.DiscountAmount).HasPrecision(18, 2);
                entity.Property(s => s.TotalAmount).HasPrecision(18, 2);

                // Unique invoice number
                entity.HasIndex(s => s.InvoiceNumber).IsUnique();
                entity.HasIndex(s => s.SaleDate);
                entity.HasIndex(s => s.CustomerId);
                entity.HasIndex(s => s.Status);
            });

            // CRITICAL FIX: Configure SaleItem with item-level discount fields
            modelBuilder.Entity<SaleItem>(entity =>
            {
                entity.Property(si => si.Quantity).HasPrecision(10, 3);
                entity.Property(si => si.UnitPrice).HasPrecision(18, 2);
                entity.Property(si => si.GSTRate).HasPrecision(5, 2);
                entity.Property(si => si.GSTAmount).HasPrecision(18, 2);
                entity.Property(si => si.LineTotal).HasPrecision(18, 2);
                entity.Property(si => si.ReturnedQuantity).HasPrecision(10, 3);

                // CRITICAL: Add item-level discount configurations
                entity.Property(si => si.ItemDiscountPercentage)
                      .HasPrecision(5, 2)
                      .HasDefaultValue(0);

                entity.Property(si => si.ItemDiscountAmount)
                      .HasPrecision(18, 2)
                      .HasDefaultValue(0);

                // Add check constraints
                entity.HasCheckConstraint("CK_SaleItem_ItemDiscountPercentage",
                                         "[ItemDiscountPercentage] >= 0 AND [ItemDiscountPercentage] <= 100");
                entity.HasCheckConstraint("CK_SaleItem_ItemDiscountAmount",
                                         "[ItemDiscountAmount] >= 0");
            });

            modelBuilder.Entity<Return>(entity =>
            {
                entity.Property(r => r.SubTotal).HasPrecision(18, 2);
                entity.Property(r => r.GSTAmount).HasPrecision(18, 2);
                entity.Property(r => r.DiscountAmount).HasPrecision(18, 2);
                entity.Property(r => r.TotalAmount).HasPrecision(18, 2);

                // Unique return number
                entity.HasIndex(r => r.ReturnNumber).IsUnique();
                entity.HasIndex(r => r.ReturnDate);
                entity.HasIndex(r => r.SaleId);
                entity.HasIndex(r => r.Status);
            });

            modelBuilder.Entity<ReturnItem>(entity =>
            {
                entity.Property(ri => ri.ReturnQuantity).HasPrecision(10, 3);
                entity.Property(ri => ri.UnitPrice).HasPrecision(18, 2);
                entity.Property(ri => ri.DiscountAmount).HasPrecision(18, 2);
                entity.Property(ri => ri.GSTRate).HasPrecision(5, 2);
                entity.Property(ri => ri.GSTAmount).HasPrecision(18, 2);
                entity.Property(ri => ri.LineTotal).HasPrecision(18, 2);
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(u => u.Username).IsUnique();
                entity.HasIndex(u => u.Email).IsUnique();
                entity.HasIndex(u => u.IsActive);
            });

            modelBuilder.Entity<Setting>(entity =>
            {
                entity.HasIndex(s => s.Key).IsUnique();
                entity.HasIndex(s => s.Category);
            });

            // Configure relationships
            modelBuilder.Entity<Product>()
                .HasOne(p => p.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SaleItem>()
                .HasOne(si => si.Sale)
                .WithMany(s => s.SaleItems)
                .HasForeignKey(si => si.SaleId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ReturnItem>()
                .HasOne(ri => ri.Return)
                .WithMany(r => r.ReturnItems)
                .HasForeignKey(ri => ri.ReturnId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ReturnItem>()
                .HasOne(ri => ri.SaleItem)
                .WithMany(si => si.ReturnItems)
                .HasForeignKey(ri => ri.SaleItemId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ReturnItem>()
                .HasOne(ri => ri.Product)
                .WithMany()
                .HasForeignKey(ri => ri.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            // Seed initial data
            SeedInitialData(modelBuilder);
        }

        private void SeedInitialData(ModelBuilder modelBuilder)
        {
            // Seed default categories
            modelBuilder.Entity<Category>().HasData(
                new Category { Id = 1, Name = "Men's Kurtas", Description = "Traditional kurtas for men", IsActive = true, CreatedAt = DateTime.Now },
                new Category { Id = 2, Name = "Women's Kurtas", Description = "Traditional kurtas for women", IsActive = true, CreatedAt = DateTime.Now },
                new Category { Id = 3, Name = "Dhotis", Description = "Traditional dhotis", IsActive = true, CreatedAt = DateTime.Now },
                new Category { Id = 4, Name = "Sarees", Description = "Traditional sarees", IsActive = true, CreatedAt = DateTime.Now },
                new Category { Id = 5, Name = "Shirts", Description = "Khadi shirts", IsActive = true, CreatedAt = DateTime.Now },
                new Category { Id = 6, Name = "Fabrics", Description = "Khadi fabrics by meter", IsActive = true, CreatedAt = DateTime.Now },
                new Category { Id = 7, Name = "Accessories", Description = "Khadi accessories and bags", IsActive = true, CreatedAt = DateTime.Now }
            );

            // Seed default admin user (password: Admin@123)
            modelBuilder.Entity<User>().HasData(
                new User
                {
                    Id = 1,
                    Username = "admin",
                    FullName = "Store Administrator",
                    Email = "admin@khadistore.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
                    Role = "Admin",
                    IsActive = true,
                    CreatedAt = DateTime.Now
                }
            );

            // Seed default settings
            modelBuilder.Entity<Setting>().HasData(
                new Setting { Id = 1, Key = "StoreName", Value = "Bhavena Khadi Bhavan", Description = "Store name for invoices", Category = "Store" },
                new Setting { Id = 2, Key = "StoreAddress", Value = "Shop No 102, Viklang Mart, Nr. Water Tank, Kaliyabid, Bhavnagar, Gujarat - 364002", Description = "Store address", Category = "Store" },
                new Setting { Id = 3, Key = "StorePhone", Value = "+91 278-4051174", Description = "Store phone number", Category = "Store" },
                new Setting { Id = 4, Key = "GSTNumber", Value = "27AAAAA0000A1Z5", Description = "Store GST number", Category = "Tax" },
                new Setting { Id = 5, Key = "InvoicePrefix", Value = "KHD", Description = "Invoice number prefix", Category = "Store" },
                new Setting { Id = 6, Key = "ReturnPrefix", Value = "RET", Description = "Return number prefix", Category = "Store" },
                new Setting { Id = 7, Key = "DefaultGSTRate", Value = "5.0", Description = "Default GST rate percentage", Category = "Tax" },
                new Setting { Id = 8, Key = "LowStockThreshold", Value = "5", Description = "Default low stock threshold", Category = "Inventory" },
                new Setting { Id = 9, Key = "Currency", Value = "INR", Description = "Store currency", Category = "Store" }
            );

            // CRITICAL FIX: Corrected seed products with proper names and sizes
            modelBuilder.Entity<Product>().HasData(
                // Men's Kurtas with different sizes
                new Product
                {
                    Id = 1,
                    Name = "Cotton Khadi Kurta",
                    Description = "Pure cotton khadi kurta in white color",
                    CategoryId = 1,
                    PurchasePrice = 400,
                    SalePrice = 650,
                    StockQuantity = 25,
                    MinimumStock = 5,
                    SKU = "KHD-CK-W-M-001",
                    FabricType = "Cotton Khadi",
                    Color = "White",
                    Size = "M",
                    Pattern = "Solid",
                    UnitOfMeasure = "Piece",
                    GSTRate = 5.0m,
                    IsActive = true,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                },
                new Product
                {
                    Id = 2,
                    Name = "Cotton Khadi Kurta",
                    Description = "Pure cotton khadi kurta in white color",
                    CategoryId = 1,
                    PurchasePrice = 400,
                    SalePrice = 650,
                    StockQuantity = 20,
                    MinimumStock = 5,
                    SKU = "KHD-CK-W-L-002",
                    FabricType = "Cotton Khadi",
                    Color = "White",
                    Size = "L",
                    Pattern = "Solid",
                    UnitOfMeasure = "Piece",
                    GSTRate = 5.0m,
                    IsActive = true,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                },
                new Product
                {
                    Id = 3,
                    Name = "Cotton Khadi Kurta",
                    Description = "Pure cotton khadi kurta in white color",
                    CategoryId = 1,
                    PurchasePrice = 400,
                    SalePrice = 650,
                    StockQuantity = 15,
                    MinimumStock = 5,
                    SKU = "KHD-CK-W-XL-003",
                    FabricType = "Cotton Khadi",
                    Color = "White",
                    Size = "XL",
                    Pattern = "Solid",
                    UnitOfMeasure = "Piece",
                    GSTRate = 5.0m,
                    IsActive = true,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                },

                // CRITICAL FIX: Women's Khadi Kurta with different sizes
                new Product
                {
                    Id = 4,
                    Name = "Women's Khadi Kurta",
                    Description = "Cotton khadi kurta for women in pink",
                    CategoryId = 2,
                    PurchasePrice = 380,
                    SalePrice = 580,
                    StockQuantity = 30,
                    MinimumStock = 8,
                    SKU = "KHD-WK-P-S-004",
                    FabricType = "Cotton Khadi",
                    Color = "Pink",
                    Size = "S",
                    Pattern = "Printed",
                    UnitOfMeasure = "Piece",
                    GSTRate = 5.0m,
                    IsActive = true,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                },
                new Product
                {
                    Id = 5,
                    Name = "Women's Khadi Kurta",
                    Description = "Cotton khadi kurta for women in pink",
                    CategoryId = 2,
                    PurchasePrice = 380,
                    SalePrice = 580,
                    StockQuantity = 25,
                    MinimumStock = 8,
                    SKU = "KHD-WK-P-M-005",
                    FabricType = "Cotton Khadi",
                    Color = "Pink",
                    Size = "M",
                    Pattern = "Printed",
                    UnitOfMeasure = "Piece",
                    GSTRate = 5.0m,
                    IsActive = true,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                },
                new Product
                {
                    Id = 6,
                    Name = "Women's Khadi Kurta",
                    Description = "Cotton khadi kurta for women in pink",
                    CategoryId = 2,
                    PurchasePrice = 380,
                    SalePrice = 580,
                    StockQuantity = 20,
                    MinimumStock = 8,
                    SKU = "KHD-WK-P-L-006",
                    FabricType = "Cotton Khadi",
                    Color = "Pink",
                    Size = "L",
                    Pattern = "Printed",
                    UnitOfMeasure = "Piece",
                    GSTRate = 5.0m,
                    IsActive = true,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                },

                // Other products
                new Product
                {
                    Id = 7,
                    Name = "Silk Khadi Saree",
                    Description = "Handwoven silk khadi saree in royal blue",
                    CategoryId = 4,
                    PurchasePrice = 1200,
                    SalePrice = 1800,
                    StockQuantity = 15,
                    MinimumStock = 3,
                    SKU = "KHD-SS-B-001",
                    FabricType = "Silk Khadi",
                    Color = "Blue",
                    Size = "Free Size",
                    Pattern = "Handloom",
                    UnitOfMeasure = "Piece",
                    GSTRate = 5.0m,
                    IsActive = true,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                },
                new Product
                {
                    Id = 8,
                    Name = "Traditional Dhoti",
                    Description = "Pure cotton dhoti in cream color",
                    CategoryId = 3,
                    PurchasePrice = 300,
                    SalePrice = 480,
                    StockQuantity = 20,
                    MinimumStock = 5,
                    SKU = "KHD-D-C-001",
                    FabricType = "Cotton Khadi",
                    Color = "Cream",
                    Size = "Free Size",
                    Pattern = "Solid",
                    UnitOfMeasure = "Piece",
                    GSTRate = 5.0m,
                    IsActive = true,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                },
                new Product
                {
                    Id = 9,
                    Name = "Khadi Cotton Fabric",
                    Description = "Pure khadi cotton fabric per meter",
                    CategoryId = 6,
                    PurchasePrice = 80,
                    SalePrice = 120,
                    StockQuantity = 100,
                    MinimumStock = 20,
                    SKU = "KHD-CF-N-001",
                    FabricType = "Cotton Khadi",
                    Color = "Natural",
                    Size = "Per Meter",
                    Pattern = "Plain",
                    UnitOfMeasure = "Meter",
                    GSTRate = 5.0m,
                    IsActive = true,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                }
            );

            // Seed sample customers
            modelBuilder.Entity<Customer>().HasData(
                new Customer
                {
                    Id = 1,
                    Name = "Rajesh Kumar",
                    Phone = "9876543210",
                    Email = "rajesh@example.com",
                    Address = "456 MG Road, Mumbai, Maharashtra - 400001",
                    TotalOrders = 0,
                    TotalPurchases = 0,
                    CreatedAt = DateTime.Now
                },
                new Customer
                {
                    Id = 2,
                    Name = "Priya Sharma",
                    Phone = "9876543211",
                    Email = "priya@example.com",
                    Address = "789 Park Street, Delhi - 110001",
                    TotalOrders = 0,
                    TotalPurchases = 0,
                    CreatedAt = DateTime.Now
                }
            );
        }
    }

    /// <summary>
    /// Database initialization helper
    /// </summary>
    public static class DatabaseInitializer
    {
        public static void Initialize(ApplicationDbContext context)
        {
            // Ensure database is created
            context.Database.EnsureCreated();

            // Apply any pending migrations
            if (context.Database.GetPendingMigrations().Any())
            {
                context.Database.Migrate();
            }
        }

        public static async Task InitializeAsync(ApplicationDbContext context)
        {
            // Ensure database is created
            await context.Database.EnsureCreatedAsync();

            // Apply any pending migrations
            var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
            if (pendingMigrations.Any())
            {
                await context.Database.MigrateAsync();
            }
        }

        public static void SeedTestData(ApplicationDbContext context)
        {
            // Add any additional test data if needed
            if (!context.Sales.Any())
            {
                // Add sample sales data for testing with item-level discounts
                var testSale = new Sale
                {
                    InvoiceNumber = "KHD000001",
                    SaleDate = DateTime.Today.AddDays(-1),
                    CustomerId = 1,
                    PaymentMethod = "Cash",
                    SubTotal = 650,
                    GSTAmount = 32.50m,
                    DiscountPercentage = 0,
                    DiscountAmount = 0,
                    TotalAmount = 682.50m,
                    Status = "Completed"
                };

                context.Sales.Add(testSale);
                context.SaveChanges();

                var testSaleItem = new SaleItem
                {
                    SaleId = testSale.Id,
                    ProductId = 1,
                    ProductName = "Cotton Khadi Kurta",
                    Quantity = 1,
                    UnitPrice = 650,
                    GSTRate = 5.0m,
                    GSTAmount = 32.50m,
                    LineTotal = 682.50m,
                    UnitOfMeasure = "Piece",
                    // Item-level discount fields
                    ItemDiscountPercentage = 0,
                    ItemDiscountAmount = 0
                };

                context.SaleItems.Add(testSaleItem);
                context.SaveChanges();

                // Update customer totals
                var customer = context.Customers.Find(1);
                if (customer != null)
                {
                    customer.TotalOrders = 1;
                    customer.TotalPurchases = 682.50m;
                    customer.LastPurchaseDate = DateTime.Today.AddDays(-1);
                    context.SaveChanges();
                }
            }
        }
    }
}