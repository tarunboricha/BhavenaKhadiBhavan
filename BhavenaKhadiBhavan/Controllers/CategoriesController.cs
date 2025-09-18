using Microsoft.AspNetCore.Mvc;
using BhavenaKhadiBhavan.Data;
using Microsoft.EntityFrameworkCore;
using BhavenaKhadiBhavan.Models;


namespace BhavenaKhadiBhavan.Controllers
{
    public class CategoriesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CategoriesController(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Display categories list
        /// </summary>
        public async Task<IActionResult> Index()
        {
            try
            {
                var categories = await _context.Categories
                    .Include(c => c.Products)
                    .OrderBy(c => c.Name)
                    .ToListAsync();

                return View(categories);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error loading categories: " + ex.Message;
                return View(new List<Category>());
            }
        }

        /// <summary>
        /// Show category details
        /// </summary>
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var category = await _context.Categories
                    .Include(c => c.Products.Where(p => p.IsActive))
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (category == null)
                {
                    TempData["Error"] = "Category not found.";
                    return RedirectToAction(nameof(Index));
                }

                return View(category);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error loading category: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// Show create category form
        /// </summary>
        public IActionResult Create()
        {
            return View(new Category());
        }

        /// <summary>
        /// Handle create category form submission
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Category category)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // Check for duplicate name
                    var existingCategory = await _context.Categories
                        .FirstOrDefaultAsync(c => c.Name.ToLower() == category.Name.ToLower() && c.IsActive);

                    if (existingCategory != null)
                    {
                        ModelState.AddModelError("Name", "A category with this name already exists.");
                        return View(category);
                    }

                    category.CreatedAt = DateTime.Now;
                    _context.Categories.Add(category);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = $"Category '{category.Name}' created successfully!";
                    return RedirectToAction(nameof(Index));
                }

                return View(category);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error creating category: " + ex.Message;
                return View(category);
            }
        }

        /// <summary>
        /// Show edit category form
        /// </summary>
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var category = await _context.Categories.FindAsync(id);
                if (category == null)
                {
                    TempData["Error"] = "Category not found.";
                    return RedirectToAction(nameof(Index));
                }

                return View(category);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error loading category: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// Handle edit category form submission
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Category category)
        {
            if (id != category.Id)
            {
                TempData["Error"] = "Invalid category ID.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                if (ModelState.IsValid)
                {
                    // Check for duplicate name
                    var existingCategory = await _context.Categories
                        .FirstOrDefaultAsync(c => c.Name.ToLower() == category.Name.ToLower() &&
                                           c.Id != id && c.IsActive);

                    if (existingCategory != null)
                    {
                        ModelState.AddModelError("Name", "A category with this name already exists.");
                        return View(category);
                    }

                    _context.Update(category);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = $"Category '{category.Name}' updated successfully!";
                    return RedirectToAction(nameof(Index));
                }

                return View(category);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error updating category: " + ex.Message;
                return View(category);
            }
        }

        /// <summary>
        /// Show delete confirmation
        /// </summary>
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var category = await _context.Categories
                    .Include(c => c.Products)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (category == null)
                {
                    TempData["Error"] = "Category not found.";
                    return RedirectToAction(nameof(Index));
                }

                return View(category);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error loading category: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// Handle delete confirmation
        /// </summary>
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var category = await _context.Categories
                    .Include(c => c.Products)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (category == null)
                {
                    TempData["Error"] = "Category not found.";
                    return RedirectToAction(nameof(Index));
                }

                // Check if category has products
                if (category.Products.Any(p => p.IsActive))
                {
                    TempData["Warning"] = $"Category '{category.Name}' cannot be deleted as it has active products. It has been deactivated instead.";
                    category.IsActive = false;
                    await _context.SaveChangesAsync();
                }
                else
                {
                    _context.Categories.Remove(category);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = $"Category '{category.Name}' deleted successfully!";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error deleting category: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// AJAX endpoint to get categories
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetCategories()
        {
            try
            {
                var categories = await _context.Categories
                    .Where(c => c.IsActive)
                    .Select(c => new
                    {
                        id = c.Id,
                        name = c.Name,
                        productCount = c.Products.Count(p => p.IsActive)
                    })
                    .OrderBy(c => c.name)
                    .ToListAsync();

                return Json(categories);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }
    }
}
