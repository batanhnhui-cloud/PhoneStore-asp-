using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PhoneStore.Data;
using PhoneStore.Helpers;
using PhoneStore.Models;

namespace PhoneStore.Controllers
{
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public CartController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        private List<CartItem> GetCart() { var cart = HttpContext.Session.Get<List<CartItem>>("Cart"); return cart ?? new List<CartItem>(); }

        public IActionResult Index() => View(GetCart());

        [HttpPost]
        public async Task<IActionResult> AddToCart(int productId, int quantity = 1)
        {
            var product = await _context.Products.FindAsync(productId);
            if (product == null) return NotFound();
            var cart = GetCart();
            var item = cart.FirstOrDefault(c => c.ProductId == productId);
            if (item != null) item.Quantity += quantity;
            else cart.Add(new CartItem { ProductId = product.Id, ProductName = product.Name, Price = product.Price, Quantity = quantity, ImageUrl = product.ImageUrl });
            HttpContext.Session.Set("Cart", cart);
            return RedirectToAction(nameof(Index));
        }

        public IActionResult RemoveFromCart(int id)
        {
            var cart = GetCart();
            var item = cart.FirstOrDefault(c => c.ProductId == id);
            if (item != null) { cart.Remove(item); HttpContext.Session.Set("Cart", cart); }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public IActionResult UpdateQuantity(int id, int quantity)
        {
            var cart = GetCart();
            var item = cart.FirstOrDefault(c => c.ProductId == id);
            if (item != null) { if (quantity > 0) item.Quantity = quantity; else cart.Remove(item); HttpContext.Session.Set("Cart", cart); }
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Checkout()
        {
            var cart = GetCart(); if (!cart.Any()) return RedirectToAction(nameof(Index));
            ViewBag.Branches = new SelectList(await _context.Branches.ToListAsync(), "Id", "Name");
            ViewBag.SavedAddresses = new List<CustomerAddress>();
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user != null) ViewBag.SavedAddresses = await _context.CustomerAddresses.Where(a => a.UserId == user.Id).OrderByDescending(a => a.IsDefault).ToListAsync();
            }
            return View(cart);
        }

        [HttpPost]
        public async Task<IActionResult> Checkout(string customerName, string phone, string address, int branchId)
        {
            var cart = GetCart(); if (!cart.Any()) return RedirectToAction(nameof(Index));

            string? currentUserId = null;
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user != null) currentUserId = user.Id;
            }

            // ĐÚNG CHUẨN THỰC TẾ: Đơn hàng tạo ra phải ở trạng thái "Pending" (Chờ xử lý)
            var order = new Order
            {
                UserId = currentUserId,
                CustomerName = customerName,
                Phone = phone,
                Address = address,
                BranchId = branchId,
                OrderDate = DateTime.Now,
                Status = "Pending", // ĐÃ ĐỔI TỪ SUCCESS THÀNH PENDING
                TotalAmount = cart.Sum(c => c.Price * c.Quantity)
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            // Chỉ lưu chi tiết sản phẩm khách muốn mua (Không tự động xuất kho nữa)
            foreach (var item in cart)
            {
                _context.OrderDetails.Add(new OrderDetail
                {
                    OrderId = order.Id,
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    Price = item.Price
                });
            }

            await _context.SaveChangesAsync();
            HttpContext.Session.Remove("Cart");

            return RedirectToAction("CheckoutSuccess", new { orderId = order.Id });
        }

        public IActionResult CheckoutSuccess(int orderId) { ViewBag.OrderId = orderId; return View(); }
    }
}