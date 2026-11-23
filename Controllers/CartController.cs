using Microsoft.AspNetCore.Mvc;
using Project_385.Models.ViewModels;
using Project_385.Models;
using Project_385.Data;
using Project_385.Infrastructure;
using Project_385.Services;

namespace Project_385.Controllers
{
    public class CartController : Controller
    {
        private readonly DataContext _dataContext;
        private readonly CartPricingService _cartPricingService;

        public CartController(DataContext _Context, CartPricingService cartPricingService)
        {
            _dataContext = _Context;
            _cartPricingService = cartPricingService;
        }

        public IActionResult Index()
        {
            List<CartItemModel> CartItems = HttpContext.Session.GetJson<List<CartItemModel>>("Cart") ?? new List<CartItemModel>();
            CartItemViewModel caVN = _cartPricingService.BuildSummary(CartItems);
            return View(caVN);
        }

        public IActionResult Checkout()
        {
            return View("~/Views/Checkout/Index.cshtml");
        }

        [HttpPost]
        public async Task<IActionResult> Add(int id, int quantity = 1)
        {
            ProductModels product = await _dataContext.Products.FindAsync(id);

            if (product == null)
                return NotFound(new { success = false, message = "Không tìm thấy sản phẩm." });

            quantity = Math.Clamp(quantity, 1, 100);

            List<CartItemModel> cart = HttpContext.Session.GetJson<List<CartItemModel>>("Cart") ?? new List<CartItemModel>();
            CartItemModel cartItem = cart.Where(x => x.ProductId == id).FirstOrDefault();
            var requestedTotal = (cartItem?.Quantity ?? 0) + quantity;
            if (product.StockQuantity <= 0)
                return Conflict(new { success = false, message = "Sản phẩm hiện đã hết hàng." });
            if (requestedTotal > product.StockQuantity)
                return Conflict(new { success = false, message = $"Chỉ còn {product.StockQuantity} sản phẩm trong kho." });

            if (cartItem == null)
            {
                var newItem = new CartItemModel(product);
                newItem.Quantity = quantity;
                cart.Add(newItem);
            }
            else
            {
                cartItem.Quantity = Math.Min(cartItem.Quantity + quantity, 100);
            }

            HttpContext.Session.SetJson("Cart", cart);
            return Json(new { success = true, message = "Đã thêm sản phẩm vào giỏ hàng." });
        }


        public IActionResult Increase(int id)
        {
            List<CartItemModel> cart = HttpContext.Session.GetJson<List<CartItemModel>>("Cart") ?? new List<CartItemModel>();
            CartItemModel cartItem = cart.Where(c => c.ProductId == id).FirstOrDefault();

            if (cartItem != null && cartItem.Quantity >= 1)
            {
                var stock = _dataContext.Products.Where(p => p.Id == id).Select(p => p.StockQuantity).FirstOrDefault();
                if (cartItem.Quantity < stock) ++cartItem.Quantity;
                else TempData["Error"] = "Không thể tăng thêm vì đã đạt số lượng tồn kho.";
            }
            else if (cartItem != null)
            {
                cart.RemoveAll(p => p.ProductId == id);
            }
            if (cart.Count == 0)
            {
                HttpContext.Session.Remove("Cart");
            }
            else
            {
                HttpContext.Session.SetJson("Cart", cart);
            }
            TempData["Success"] = "Cập nhật số lượng thành công";
            return RedirectToAction("Index");
        }

        public IActionResult Decrease(int id)
        {
            List<CartItemModel> cart = HttpContext.Session.GetJson<List<CartItemModel>>("Cart") ?? new List<CartItemModel>();
            CartItemModel cartItem = cart.Where(c => c.ProductId == id).FirstOrDefault();

            if (cartItem != null)
            {
                if (cartItem.Quantity > 1)
                {
                    --cartItem.Quantity;
                }
                else
                {
                    cart.RemoveAll(p => p.ProductId == id);
                }
            }

            if (cart.Count == 0)
            {
                HttpContext.Session.Remove("Cart");
            }
            else
            {
                HttpContext.Session.SetJson("Cart", cart);
            }
            TempData["Success"] = "Cập nhật số lượng thành công";
            return RedirectToAction("Index");
        }

        public IActionResult Remove(int id)
        {
            List<CartItemModel> cart = HttpContext.Session.GetJson<List<CartItemModel>>("Cart") ?? new List<CartItemModel>();
            cart.RemoveAll(p => p.ProductId == id);

            if (cart.Count == 0)
            {
                HttpContext.Session.Remove("Cart");
            }
            else
            {
                HttpContext.Session.SetJson("Cart", cart);
            }
            TempData["Success"] = "Đã xóa sản phẩm khỏi giỏ hàng";
            return RedirectToAction("Index");
        }

        public IActionResult Clear()
        {
            HttpContext.Session.Remove("Cart");
            TempData["Success"] = "Đã xóa toàn bộ giỏ hàng";
            return RedirectToAction("Index");
        }
    }
}
