using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project_385.Models;
using Project_385.Data;

namespace Project_385.Controllers
{
    public class BrandController : Controller
    {
        private readonly DataContext _dataContext;

        public BrandController(DataContext context)
        {
            _dataContext = context;
        }

        public async Task<IActionResult> Index(string Slug = "", int pg = 1)
        {
            if (string.IsNullOrEmpty(Slug))
            {
                return RedirectToAction("Index", "Home");
            }

            var brands = await _dataContext.Brands.FirstOrDefaultAsync(c => c.Slug == Slug);

            if (brands == null)
            {
                return NotFound($"Lỗi: Không tìm thấy hãng là: {Slug}");
            }

            const int pageSize = 12;
            var productsByBrand = _dataContext.Products.AsNoTracking().Where(p => p.BrandID == brands.Id);
            var totalItems = await productsByBrand.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
            pg = Math.Clamp(pg, 1, totalPages);

            ViewBag.BrandName = brands.Name;
            ViewBag.BrandSlug = brands.Slug;
            ViewBag.Pager = new Paginate(totalItems, pg, pageSize);

            return View(await productsByBrand.Include(p => p.Category).Include(p => p.Brand)
                .OrderByDescending(p => p.Id).Skip((pg - 1) * pageSize).Take(pageSize).ToListAsync());
        }
    }
}
