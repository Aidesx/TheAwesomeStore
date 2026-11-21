using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project_385.Areas.Admin.Filters;
using Project_385.Models;
using Project_385.Data;
using Project_385.Services;
using System.Threading.Tasks;
using System.Data;

namespace Project_385.Areas.Admin.Controllers
{
    [Area("Admin")]
    [AdminAuthorize("ManageOrder")]
    public class OrderController : Controller
    {
        private readonly DataContext _dataContext;
        private readonly PermissionService _permissionService;

        public OrderController(DataContext context, PermissionService permissionService)
        {
            _dataContext = context;
            _permissionService = permissionService;
        }

        public async Task<IActionResult> Index(int pg = 1)
        {
            const int pageSize = 10;
            if (pg < 1) pg = 1;

            int recsCount = await _dataContext.Orders.CountAsync();
            var pager = new Paginate(recsCount, pg, pageSize);
            int recSkip = (pg - 1) * pageSize;

            var data = await _dataContext.Orders
                .AsNoTracking()
                .OrderByDescending(p => p.Id)
                .Skip(recSkip)
                .Take(pager.PageSize)
                .ToListAsync();

            ViewBag.Pager = pager;
            return View(data);
        }

        public async Task<IActionResult> ViewOrder(string orderNumber)
        {
            var order = await _dataContext.Orders.FirstOrDefaultAsync(o => o.OrderNumber == orderNumber);

            if (order == null) return NotFound();
            var orderDetails = await _dataContext.OrderDetails.Where(o => o.OrderId == order.Id).ToListAsync();

            ViewBag.Order = order;
            return View(orderDetails);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(string orderNumber, int status)
        {
            await using var transaction = await _dataContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var order = await _dataContext.Orders.FirstOrDefaultAsync(o => o.OrderNumber == orderNumber);
            if (order == null) return NotFound();

            if (order.Status == (int)OrderStatus.Cancelled && status != (int)OrderStatus.Cancelled)
            {
                TempData["Error"] = "Đơn đã hủy và tồn kho đã được hoàn lại; không thể mở lại đơn này.";
                return RedirectToAction("ViewOrder", new { orderNumber });
            }

            if (status == (int)OrderStatus.Cancelled && order.Status != (int)OrderStatus.Cancelled && !order.StockReturned)
            {
                var orderItems = await _dataContext.OrderDetails.Where(item => item.OrderId == order.Id).ToListAsync();
                foreach (var item in orderItems)
                {
                    await _dataContext.Products
                        .Where(product => product.Id == item.ProductId)
                        .ExecuteUpdateAsync(update => update.SetProperty(product => product.StockQuantity, product => product.StockQuantity + item.Quantity));
                }

                order.StockReturned = true;
            }

            order.Status = status;
            await _dataContext.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData["Success"] = "Cập nhật trạng thái đơn hàng thành công!";
            return RedirectToAction("ViewOrder", new { orderNumber = orderNumber });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            await using var transaction = await _dataContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var order = await _dataContext.Orders
                .FromSql($"SELECT * FROM [Orders] WITH (UPDLOCK, ROWLOCK) WHERE [Id] = {id}")
                .FirstOrDefaultAsync();
            if (order == null)
            {
                TempData["Error"] = "Không tìm thấy đơn hàng này!";
                return RedirectToAction("Index");
            }
            

            var orderDetails = await _dataContext.OrderDetails
                .Where(o => o.OrderId == id)
                .ToListAsync();

            if (order.Status == (int)OrderStatus.Pending && !order.StockReturned)
            {
                foreach (var item in orderDetails)
                {
                    await _dataContext.Products
                        .Where(product => product.Id == item.ProductId)
                        .ExecuteUpdateAsync(update => update.SetProperty(product => product.StockQuantity, product => product.StockQuantity + item.Quantity));
                }
            }

            _dataContext.OrderDetails.RemoveRange(orderDetails);
            _dataContext.Orders.Remove(order);
            await _dataContext.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData["Success"] = "Đã xóa đơn hàng thành công";
            return RedirectToAction("Index");
        }
    } 
}
