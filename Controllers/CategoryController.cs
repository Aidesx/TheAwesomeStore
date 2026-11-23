using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project_385.Models;
using Project_385.Data;

namespace Project_385.Controllers
{
    public class CategoryController : Controller
    {
        private readonly DataContext _dataContext;

        public CategoryController(DataContext context)
        {
            _dataContext = context;
        }

        public async Task<IActionResult> Index(string Slug = "", int pg = 1)
        {
            if (string.IsNullOrEmpty(Slug))
            {
                return RedirectToAction("Index", "Home");
            }

            var category = await _dataContext.Categories.FirstOrDefaultAsync(c => c.Slug == Slug);

            if (category == null)
            {
                return NotFound($"Lỗi: Không tìm thấy danh mục là: {Slug}");
            }

            const int pageSize = 12;
            var productsByCategory = _dataContext.Products.AsNoTracking().Where(p => p.CategoryID == category.Id);
            var totalItems = await productsByCategory.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
            pg = Math.Clamp(pg, 1, totalPages);

            ViewBag.CategoryName = category.Name;
            ViewBag.CategorySlug = category.Slug;
            ViewBag.Pager = new Paginate(totalItems, pg, pageSize);

            return View(await productsByCategory.Include(p => p.Category).Include(p => p.Brand)
                .OrderByDescending(p => p.Id).Skip((pg - 1) * pageSize).Take(pageSize).ToListAsync());
        }
    }
}
