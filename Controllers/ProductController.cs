using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Project_385.Models;
using Project_385.Models.ViewModels;
using Project_385.Data;

namespace Project_385.Controllers
{
    public class ProductController : Controller
    {
        private readonly DataContext _dataContext;
        private readonly UserManager<AppUserModels> _userManager;
        public ProductController(DataContext context, UserManager<AppUserModels> userManager)
        {
            _dataContext = context;
            _userManager = userManager;
        }
        public IActionResult Index()
        {
            var featuredProductId = _dataContext.Products
                .OrderByDescending(p => p.Id)
                .Select(p => p.Id)
                .FirstOrDefault();

            if (featuredProductId == 0)
            {
                return RedirectToAction("Index", "Home");
            }

            return RedirectToAction("Details", new { Id = featuredProductId });
        }

        public async Task<IActionResult> Details(int? Id)
        {
            if (Id == null) return RedirectToAction("Index");

            var productsByID = await _dataContext.Products
                .Include(p => p.Ratings)
                .Include(p => p.Category)
                .Include(p => p.Brand)
                .Where(b => b.Id == Id)
                .FirstOrDefaultAsync();

            if (productsByID == null) return RedirectToAction("Index");

            bool hasReviewed = false;
            if (User.Identity.IsAuthenticated)
            {
                var currentUser = await _userManager.GetUserAsync(User); 
                if (currentUser != null)
                {
                    hasReviewed = await _dataContext.Ratings
                        .AnyAsync(r => r.ProductId == Id && r.Email == currentUser.Email);

                    ViewBag.UserEmail = currentUser.Email;
                    ViewBag.UserName = currentUser.UserName;
                }
            }
            ViewBag.HasReviewed = hasReviewed;

            var viewModels = new ProductDetailViewModels
            {
                Product = productsByID,
                Ratings = productsByID.Ratings?.ToList() ?? new List<RatingModels>(),
                Rating = new RatingModels { ProductId = productsByID.Id }
            };

            ViewBag.RelatedByBrand = await _dataContext.Products.Where(p => p.BrandID == productsByID.BrandID && p.Id != productsByID.Id).OrderByDescending(p => p.Id).Take(4).ToListAsync();
            ViewBag.RelatedByCategory = await _dataContext.Products.Where(p => p.CategoryID == productsByID.CategoryID && p.Id != productsByID.Id).OrderByDescending(p => p.Id).Take(6).ToListAsync();

            return View(viewModels);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize] 
        public async Task<IActionResult> CommentProduct([Bind(Prefix = "")] ProductDetailViewModels form)
        {
            var rating = form.Rating;
            if (rating == null || rating.ProductId == 0) return RedirectToAction("Index", "Home");
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return RedirectToAction("Login", "Account");

            string userEmail = currentUser.Email;

            var alreadyExists = await _dataContext.Ratings.AnyAsync(r => r.ProductId == rating.ProductId && r.Email == userEmail);

            if (alreadyExists)
            {
                TempData["Error"] = "Bạn đã đánh giá sản phẩm này rồi!";
                return RedirectToAction("Details", new { Id = rating.ProductId });
            }

            if (ModelState.IsValid)
            {
                rating.Email = userEmail;
                rating.Name = currentUser.UserName;

                _dataContext.Ratings.Add(rating);
                await _dataContext.SaveChangesAsync();
                TempData["Success"] = "Cảm ơn bạn đã đánh giá!";
            }
            else
            {
                TempData["Error"] = "Vui lòng nhập nội dung đánh giá.";
            }

            return RedirectToAction("Details", new { Id = rating.ProductId });
        }
        [HttpGet]
        [HttpPost]
        public async Task<IActionResult> Search(string search, int pg = 1, int? categoryId = null, int? brandId = null)
        {
            const int pageSize = 12;
            ViewBag.Categories = new SelectList(await _dataContext.Categories.AsNoTracking().Where(item => item.Status == 1).OrderBy(item => item.Name).ToListAsync(), "Id", "Name", categoryId);
            ViewBag.Brands = new SelectList(await _dataContext.Brands.AsNoTracking().Where(item => item.Status == 1).OrderBy(item => item.Name).ToListAsync(), "Id", "Name", brandId);
            ViewBag.CategoryId = categoryId;
            ViewBag.BrandId = brandId;
            var trimmedSearch = search?.Trim() ?? string.Empty;
            var query = _dataContext.Products.AsNoTracking();
            if (!string.IsNullOrEmpty(trimmedSearch))
            {
                query = query.Where(p => EF.Functions.Collate(p.Name, "Latin1_General_100_CI_AI_SC").Contains(trimmedSearch)
                    || (p.Description != null && EF.Functions.Collate(p.Description, "Latin1_General_100_CI_AI_SC").Contains(trimmedSearch)));
            }
            if (categoryId.HasValue) query = query.Where(product => product.CategoryID == categoryId.Value);
            if (brandId.HasValue) query = query.Where(product => product.BrandID == brandId.Value);
            var totalItems = await query.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
            pg = Math.Clamp(pg, 1, totalPages);
            var products = await query.Include(p => p.Category).Include(p => p.Brand)
                .OrderByDescending(p => p.Id).Skip((pg - 1) * pageSize).Take(pageSize)
                .ToListAsync();

            ViewBag.Keyword = trimmedSearch;
            ViewBag.Pager = new Paginate(totalItems, pg, pageSize);
            return View(products);
        }
    }
}
