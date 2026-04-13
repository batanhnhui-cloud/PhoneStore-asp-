using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhoneStore.Data;
using PhoneStore.Models;

namespace PhoneStore.Controllers
{
    public class CustomerPortalVM
    {
        public ApplicationUser CurrentUser { get; set; } = null!;
        public List<Order> MyOrders { get; set; } = new List<Order>();
        public List<CustomerAddress> Addresses { get; set; } = new List<CustomerAddress>();
        public List<DeviceImei> PurchasedDevices { get; set; } = new List<DeviceImei>();
        public string ActiveTab { get; set; } = "profile";
    }

    [Authorize]
    public class CustomerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public CustomerController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // ==========================================
        // 1. GIAO DIỆN CHÍNH (DASHBOARD)
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Dashboard(string tab = "profile")
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var vm = new CustomerPortalVM { ActiveTab = tab, CurrentUser = user };

            // Lấy đơn hàng (Tìm theo cả ID và Số điện thoại để khớp đơn cũ)
            vm.MyOrders = await _context.Orders
                .Include(o => o.OrderDetails).ThenInclude(od => od.Product)
                .Where(o => o.UserId == user.Id || (user.PhoneNumber != null && o.Phone == user.PhoneNumber))
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            // Lấy sổ địa chỉ
            vm.Addresses = await _context.CustomerAddresses
                .Where(a => a.UserId == user.Id)
                .OrderByDescending(a => a.IsDefault)
                .ToListAsync();

            // TRUY VẤN LẤY THÔNG TIN BẢO HÀNH CHO CẢ ĐƠN CŨ
            var orderIds = vm.MyOrders.Select(o => o.Id).ToList();
            vm.PurchasedDevices = await _context.DeviceImeis
                .Include(d => d.Product)
                .Where(d => d.OrderId != null && orderIds.Contains(d.OrderId.Value))
                .OrderByDescending(d => d.WarrantyActivationDate)
                .ToListAsync();

            return View(vm);
        }

        // ==========================================
        // 2. CẬP NHẬT HỒ SƠ
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> UpdateProfile(string phoneNumber)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                user.PhoneNumber = phoneNumber;
                await _userManager.UpdateAsync(user);
                TempData["SuccessMsg"] = "Cập nhật hồ sơ thành công!";
            }
            return RedirectToAction("Dashboard", new { tab = "profile" });
        }

        // ==========================================
        // 3. QUẢN LÝ ĐỊA CHỈ
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> AddAddress(string receiverName, string phone, string fullAddress)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            bool hasAddress = await _context.CustomerAddresses.AnyAsync(a => a.UserId == user.Id);
            var newAddress = new CustomerAddress
            {
                UserId = user.Id,
                ReceiverName = receiverName,
                Phone = phone,
                FullAddress = fullAddress,
                IsDefault = !hasAddress
            };

            _context.CustomerAddresses.Add(newAddress);
            await _context.SaveChangesAsync();
            TempData["SuccessMsg"] = "Đã thêm địa chỉ mới!";
            return RedirectToAction("Dashboard", new { tab = "address" });
        }

        [HttpPost]
        public async Task<IActionResult> SetDefaultAddress(int addressId)
        {
            var user = await _userManager.GetUserAsync(User);
            var addresses = await _context.CustomerAddresses.Where(a => a.UserId == user.Id).ToListAsync();
            foreach (var addr in addresses) { addr.IsDefault = (addr.Id == addressId); }
            await _context.SaveChangesAsync();
            return RedirectToAction("Dashboard", new { tab = "address" });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteAddress(int addressId)
        {
            var address = await _context.CustomerAddresses.FindAsync(addressId);
            if (address != null) { _context.CustomerAddresses.Remove(address); await _context.SaveChangesAsync(); }
            return RedirectToAction("Dashboard", new { tab = "address" });
        }
    }
}