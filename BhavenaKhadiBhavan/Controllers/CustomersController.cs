using BhavenaKhadiBhavan.Models;
using BhavenaKhadiBhavan.Services;
using Microsoft.AspNetCore.Mvc;

namespace BhavenaKhadiBhavan.Controllers
{
    public class CustomersController : Controller
    {
        private readonly ICustomerService _customerService;

        public CustomersController(ICustomerService customerService)
        {
            _customerService = customerService;
        }

        /// <summary>
        /// Display customers list with search
        /// </summary>
        public async Task<IActionResult> Index(string search, string customerType)
        {
            try
            {
                List<Customer> customers;

                if (!string.IsNullOrWhiteSpace(search))
                {
                    customers = await _customerService.SearchCustomersAsync(search);
                }
                else
                {
                    customers = await _customerService.GetAllCustomersAsync();
                }

                // Filter by customer type
                if (!string.IsNullOrWhiteSpace(customerType))
                {
                    customers = customerType.ToLower() switch
                    {
                        "new" => customers.Where(c => c.TotalOrders == 0).ToList(),
                        "regular" => customers.Where(c => c.TotalOrders >= 2 && c.TotalOrders < 5).ToList(),
                        "loyal" => customers.Where(c => c.TotalOrders >= 5).ToList(),
                        "inactive" => customers.Where(c => c.DaysSinceLastPurchase > 90).ToList(),
                        _ => customers
                    };
                }

                ViewBag.CurrentSearch = search;
                ViewBag.CurrentCustomerType = customerType;
                ViewBag.TotalCustomers = customers.Count;
                ViewBag.NewCustomers = customers.Count(c => c.TotalOrders == 0);
                ViewBag.RegularCustomers = customers.Count(c => c.TotalOrders >= 2 && c.TotalOrders < 5);
                ViewBag.LoyalCustomers = customers.Count(c => c.TotalOrders >= 5);

                return View(customers);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error loading customers: " + ex.Message;
                return View(new List<Customer>());
            }
        }

        /// <summary>
        /// Show customer details with purchase history
        /// </summary>
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var customer = await _customerService.GetCustomerByIdAsync(id);
                if (customer == null)
                {
                    TempData["Error"] = "Customer not found.";
                    return RedirectToAction(nameof(Index));
                }

                // Get customer's purchase history
                var sales = await _customerService.GetCustomerSalesAsync(id);
                ViewBag.PurchaseHistory = sales;
                ViewBag.TotalSales = sales.Count;
                ViewBag.TotalValue = sales.Sum(s => s.TotalAmount);
                ViewBag.LastPurchase = sales.OrderByDescending(s => s.SaleDate).FirstOrDefault()?.SaleDate;

                return View(customer);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error loading customer details: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// Show create customer form
        /// </summary>
        public IActionResult Create()
        {
            return View(new Customer());
        }

        /// <summary>
        /// Handle create customer form submission
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Customer customer)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // Check for duplicate phone if provided
                    if (!string.IsNullOrWhiteSpace(customer.Phone))
                    {
                        var existingCustomer = await _customerService.GetCustomerByPhoneAsync(customer.Phone);
                        if (existingCustomer != null)
                        {
                            ModelState.AddModelError("Phone", "A customer with this phone number already exists.");
                            return View(customer);
                        }
                    }

                    // Validate phone number format (Indian mobile)
                    if (!string.IsNullOrWhiteSpace(customer.Phone) && customer.Phone.Length != 10)
                    {
                        ModelState.AddModelError("Phone", "Please enter a valid 10-digit mobile number.");
                        return View(customer);
                    }

                    if (ModelState.IsValid)
                    {
                        await _customerService.CreateCustomerAsync(customer);
                        TempData["Success"] = $"Customer '{customer.Name}' created successfully!";
                        return RedirectToAction(nameof(Index));
                    }
                }

                return View(customer);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error creating customer: " + ex.Message;
                return View(customer);
            }
        }

        /// <summary>
        /// Show edit customer form
        /// </summary>
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var customer = await _customerService.GetCustomerByIdAsync(id);
                if (customer == null)
                {
                    TempData["Error"] = "Customer not found.";
                    return RedirectToAction(nameof(Index));
                }

                return View(customer);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error loading customer: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// Handle edit customer form submission
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Customer customer)
        {
            if (id != customer.Id)
            {
                TempData["Error"] = "Invalid customer ID.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                if (ModelState.IsValid)
                {
                    // Check for duplicate phone if provided
                    if (!string.IsNullOrWhiteSpace(customer.Phone))
                    {
                        var existingCustomer = await _customerService.GetCustomerByPhoneAsync(customer.Phone);
                        if (existingCustomer != null && existingCustomer.Id != id)
                        {
                            ModelState.AddModelError("Phone", "A customer with this phone number already exists.");
                            return View(customer);
                        }
                    }

                    // Validate phone number format (Indian mobile)
                    if (!string.IsNullOrWhiteSpace(customer.Phone) && customer.Phone.Length != 10)
                    {
                        ModelState.AddModelError("Phone", "Please enter a valid 10-digit mobile number.");
                        return View(customer);
                    }

                    if (ModelState.IsValid)
                    {
                        await _customerService.UpdateCustomerAsync(customer);
                        TempData["Success"] = $"Customer '{customer.Name}' updated successfully!";
                        return RedirectToAction(nameof(Details), new { id });
                    }
                }

                return View(customer);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error updating customer: " + ex.Message;
                return View(customer);
            }
        }

        /// <summary>
        /// AJAX endpoint to search customers
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> SearchCustomers(string term)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(term))
                {
                    return Json(new List<object>());
                }

                var customers = await _customerService.SearchCustomersAsync(term);
                var customerList = customers.Take(10).Select(c => new
                {
                    id = c.Id,
                    name = c.Name,
                    phone = c.Phone ?? "",
                    email = c.Email ?? "",
                    totalOrders = c.TotalOrders,
                    totalPurchases = c.TotalPurchases,
                    customerType = c.CustomerType,
                    displayText = $"{c.Name}" + (!string.IsNullOrEmpty(c.Phone) ? $" ({c.Phone})" : "")
                }).ToList();

                return Json(customerList);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        /// <summary>
        /// AJAX endpoint to get customer details
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetCustomerDetails(int customerId)
        {
            try
            {
                var customer = await _customerService.GetCustomerByIdAsync(customerId);
                if (customer == null)
                    return Json(new { error = "Customer not found" });

                return Json(new
                {
                    id = customer.Id,
                    name = customer.Name,
                    phone = customer.Phone ?? "",
                    email = customer.Email ?? "",
                    address = customer.Address ?? "",
                    totalOrders = customer.TotalOrders,
                    totalPurchases = customer.TotalPurchases,
                    lastPurchaseDate = customer.LastPurchaseDate?.ToString("dd/MM/yyyy"),
                    customerType = customer.CustomerType,
                    averageOrderValue = customer.AverageOrderValue
                });
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Customer analytics page
        /// </summary>
        public async Task<IActionResult> Analytics()
        {
            try
            {
                var customers = await _customerService.GetAllCustomersAsync();

                var analytics = new
                {
                    TotalCustomers = customers.Count,
                    NewCustomers = customers.Count(c => c.TotalOrders == 0),
                    RegularCustomers = customers.Count(c => c.TotalOrders >= 2 && c.TotalOrders < 5),
                    LoyalCustomers = customers.Count(c => c.TotalOrders >= 5),
                    InactiveCustomers = customers.Count(c => c.DaysSinceLastPurchase > 90),
                    TotalRevenue = customers.Sum(c => c.TotalPurchases),
                    AverageOrderValue = customers.Where(c => c.TotalOrders > 0).Average(c => c.AverageOrderValue),
                    TopCustomers = customers
                        .Where(c => c.TotalOrders > 0)
                        .OrderByDescending(c => c.TotalPurchases)
                        .Take(10)
                        .ToList(),
                    RecentCustomers = customers
                        .OrderByDescending(c => c.CreatedAt)
                        .Take(10)
                        .ToList(),
                    CustomersByType = new Dictionary<string, int>
                    {
                        ["New"] = customers.Count(c => c.TotalOrders == 0),
                        ["Second-time"] = customers.Count(c => c.TotalOrders == 1),
                        ["Regular"] = customers.Count(c => c.TotalOrders >= 2 && c.TotalOrders < 5),
                        ["Loyal"] = customers.Count(c => c.TotalOrders >= 5)
                    }
                };

                return View(analytics);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error loading customer analytics: " + ex.Message;
                return View();
            }
        }

        /// <summary>
        /// Export customers to CSV
        /// </summary>
        public async Task<IActionResult> ExportToCSV()
        {
            try
            {
                var customers = await _customerService.GetAllCustomersAsync();

                var csv = new System.Text.StringBuilder();
                csv.AppendLine("Name,Phone,Email,Address,Total Orders,Total Purchases,Last Purchase Date,Customer Type,Days Since Last Purchase");

                foreach (var customer in customers)
                {
                    csv.AppendLine($"\"{customer.Name}\",\"{customer.Phone}\",\"{customer.Email}\",\"{customer.Address}\"," +
                                  $"{customer.TotalOrders},{customer.TotalPurchases:F2}," +
                                  $"\"{customer.LastPurchaseDate?.ToString("dd/MM/yyyy")}\",\"{customer.CustomerType}\"," +
                                  $"{customer.DaysSinceLastPurchase}");
                }

                var fileName = $"Customers_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                return File(System.Text.Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", fileName);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error exporting customers: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }
    }
}
