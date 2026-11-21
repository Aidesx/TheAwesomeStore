using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project_385.Models;
using Project_385.Models.MoMo;
using Project_385.Models.ViewModels;
using Project_385.Data;
using Project_385.Infrastructure;
using Project_385.Services;
using Project_385.Services.MoMo;
using System.Security.Claims;
using System.Data;

namespace Project_385.Controllers
{
    public class CheckoutController : Controller
    {
        private readonly DataContext _dataContext;
        private readonly IMomoService _momoService;
        private readonly CartPricingService _cartPricingService;
        private readonly IEmailSender _emailSender;
        private readonly IEmailTemplateRenderer _emailRenderer;
        private readonly ILogger<CheckoutController> _logger;

        public CheckoutController(
            DataContext dataContext,
            IMomoService momoService,
            CartPricingService cartPricingService,
            IEmailSender emailSender,
            IEmailTemplateRenderer emailRenderer,
            ILogger<CheckoutController> logger)
        {
            _dataContext = dataContext;
            _momoService = momoService;
            _cartPricingService = cartPricingService;
            _emailSender = emailSender;
            _emailRenderer = emailRenderer;
            _logger = logger;
        }

        public IActionResult Index()
        {
            var cartItems = HttpContext.Session.GetJson<List<CartItemModel>>("Cart") ?? new List<CartItemModel>();
            var viewModel = _cartPricingService.BuildSummary(cartItems);

            if (User.Identity?.IsAuthenticated ?? false)
            {
                viewModel.ReceiverName = User.Identity.Name ?? string.Empty;
            }

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckOut(
            double? currentLatitude,
            double? currentLongitude,
            string receiverName,
            string phoneNumber,
            string shippingAddress,
            string paymentMethod = "COD")
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name;
            if (string.IsNullOrEmpty(userEmail)) return RedirectToAction("Login", "Account");

            var cartItems = HttpContext.Session.GetJson<List<CartItemModel>>("Cart") ?? new List<CartItemModel>();
            if (!cartItems.Any())
            {
                TempData["Error"] = "Giỏ hàng trống";
                return RedirectToAction("Index", "Cart");
            }

            var products = await _dataContext.Products
                .Where(product => cartItems.Select(item => item.ProductId).Contains(product.Id))
                .ToDictionaryAsync(product => product.Id);
            foreach (var item in cartItems)
            {
                if (!products.TryGetValue(item.ProductId, out var product) || item.Quantity < 1 || item.Quantity > product.StockQuantity)
                {
                    TempData["Error"] = products.TryGetValue(item.ProductId, out product)
                        ? $"Số lượng {product.Name} vượt quá tồn kho hiện tại ({product.StockQuantity})."
                        : "Một sản phẩm trong giỏ hàng không còn tồn tại.";
                    return RedirectToAction("Index", "Cart");
                }

                item.ProductName = product.Name;
                item.Price = product.Price;
                item.Image = product.Image;
            }

            var pricing = _cartPricingService.BuildSummary(cartItems, currentLatitude, currentLongitude);
            if (!pricing.CanDeliver)
            {
                TempData["Error"] = pricing.DeliveryMessage ?? "Vui lòng lấy vị trí hiện tại để tính phí di chuyển.";
                return RedirectToAction("Index");
            }

            await using var transaction = await _dataContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            foreach (var itemGroup in cartItems.GroupBy(item => item.ProductId))
            {
                var quantity = itemGroup.Sum(item => item.Quantity);
                var changed = await _dataContext.Products
                    .Where(product => product.Id == itemGroup.Key && product.StockQuantity >= quantity)
                    .ExecuteUpdateAsync(update => update.SetProperty(product => product.StockQuantity, product => product.StockQuantity - quantity));

                if (changed != 1)
                {
                    await transaction.RollbackAsync();
                    TempData["Error"] = "Tồn kho vừa thay đổi. Vui lòng kiểm tra lại giỏ hàng.";
                    return RedirectToAction("Index", "Cart");
                }
            }

            var order = new OrderModels
            {
                OrderNumber = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
                OrderDate = DateTime.Now,
                UserName = userEmail,
                ReceiverName = !string.IsNullOrWhiteSpace(receiverName) ? receiverName : userEmail,
                PhoneNumber = phoneNumber ?? string.Empty,
                ShippingAddress = shippingAddress ?? string.Empty,
                PaymentMethod = paymentMethod == "MoMo" ? "MoMo" : "COD",
                Total = pricing.GrandTotal,
                Status = (int)OrderStatus.Pending,
                StockReturned = false
            };

            _dataContext.Orders.Add(order);
            await _dataContext.SaveChangesAsync();

            var orderDetails = cartItems.Select(cart => new OrderDetailModels
            {
                OrderId = order.Id,
                ProductId = cart.ProductId,
                ProductName = cart.ProductName,
                Quantity = cart.Quantity,
                Price = cart.Price
            }).ToList();

            _dataContext.OrderDetails.AddRange(orderDetails);
            await _dataContext.SaveChangesAsync();
            await transaction.CommitAsync();

            // Handling COD payment method
            if (order.PaymentMethod == "COD")
            {
                try
                {
                    await SendOrderEmail(userEmail, order, orderDetails);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Không thể gửi email xác nhận cho đơn hàng #{OrderNumber}", order.OrderNumber);
                }

                HttpContext.Session.Remove("Cart");
                TempData["Success"] = $"Đặt hàng thành công! Mã đơn hàng: #{order.OrderNumber}";
                return RedirectToAction("OrderSuccess", new { orderNumber = order.OrderNumber });
            }

            // Handling MoMo payment method
            var requestInfo = new OrderInfoModel
            {
                FullName = order.ReceiverName,
                OrderId = order.OrderNumber,
                OrderInfo = "Thanh toán đơn hàng " + order.OrderNumber,
                Amount = ((long)order.Total).ToString()
            };

            var responseFromMomo = await _momoService.CreatePaymentMomo(requestInfo);

            if (responseFromMomo != null && !string.IsNullOrEmpty(responseFromMomo.PayUrl))
            {
                return Redirect(responseFromMomo.PayUrl);
            }

            await CancelPendingOrderAndRestoreStock(order);
            TempData["Error"] = "Không thể khởi tạo giao dịch MoMo. Đơn hàng đã được hủy và tồn kho đã được hoàn lại.";
            return RedirectToAction("OrderSuccess", new { orderNumber = order.OrderNumber });
        }

        [HttpGet]
        public async Task<IActionResult> OrderSuccess(string orderNumber)
        {
            if (string.IsNullOrEmpty(orderNumber)) return RedirectToAction("Index", "Home");

            var order = await _dataContext.Orders
                .Include(o => o.OrderDetails)
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber);

            if (order == null) return RedirectToAction("Index", "Home");

            var userEmail = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name;
            if (!User.IsInRole("Admin") && !string.IsNullOrEmpty(userEmail) && order.UserName != userEmail)
            {
                TempData["Error"] = "Bạn không có quyền truy cập đơn hàng này.";
                return RedirectToAction("Index", "Home");
            }

            return View(order);
        }

        private async Task SendOrderEmail(string userEmail, OrderModels order, List<OrderDetailModels> orderDetails)
        {
            var itemListHtml = "";
            foreach (var item in orderDetails)
            {
                itemListHtml += $"<tr><td>{item.ProductName}</td><td>{item.Quantity}</td><td>{item.Price.ToString("#,##0")} đ</td></tr>";
            }

            var body = await _emailRenderer.RenderAsync("OrderSuccess", new Dictionary<string, string>
            {
                { "UserName", userEmail },
                { "OrderNumber", order.OrderNumber },
                { "OrderDate", order.OrderDate.ToString("dd/MM/yyyy HH:mm") },
                { "Total", order.Total.ToString("#,##0") + " đ" },
                { "ItemList", itemListHtml }
            });

            await _emailSender.SendEmailAsync(userEmail, $"Xác nhận đơn hàng #{order.OrderNumber}", body);
        }

        private async Task CancelPendingOrderAndRestoreStock(OrderModels order)
        {
            await using var transaction = await _dataContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var currentOrder = await _dataContext.Orders.FirstOrDefaultAsync(item => item.Id == order.Id);
            if (currentOrder?.Status == (int)OrderStatus.Pending)
            {
                if (!currentOrder.StockReturned)
                {
                    var orderItems = await _dataContext.OrderDetails.Where(item => item.OrderId == order.Id).ToListAsync();
                    foreach (var item in orderItems)
                    {
                        await _dataContext.Products
                            .Where(product => product.Id == item.ProductId)
                            .ExecuteUpdateAsync(update => update.SetProperty(product => product.StockQuantity, product => product.StockQuantity + item.Quantity));
                    }

                    currentOrder.StockReturned = true;
                }

                currentOrder.Status = (int)OrderStatus.Cancelled;
                await _dataContext.SaveChangesAsync();
            }

            await transaction.CommitAsync();
        }
    }
}
