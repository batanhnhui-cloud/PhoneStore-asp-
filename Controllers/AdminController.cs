using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PhoneStore.Data;
using PhoneStore.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PhoneStore.Controllers
{
    public class TopProductVM
    {
        public string ProductName { get; set; } = null!;
        public int TotalSold { get; set; }
        public decimal TotalRevenue { get; set; }
    }

    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public AdminController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // ==========================================
        // 0. BẢNG ĐIỀU KHIỂN
        // ==========================================
        public async Task<IActionResult> Dashboard()
        {
            ViewBag.TotalRevenue = await _context.Orders.Where(o => o.Status == "Success").SumAsync(o => o.TotalAmount);
            ViewBag.TotalOrders = await _context.Orders.CountAsync();
            ViewBag.TotalDevices = await _context.DeviceImeis.CountAsync(d => d.Status == "Available");
            ViewBag.TotalBranches = await _context.Branches.CountAsync();

            var recentOrders = await _context.Orders
                .Include(o => o.Branch)
                .OrderByDescending(o => o.OrderDate)
                .Take(5)
                .ToListAsync();

            return View(recentOrders);
        }

        // ==========================================
        // 1. QUẢN LÝ ĐƠN HÀNG
        // ==========================================
        public async Task<IActionResult> OrderManagement()
        {
            var orders = await _context.Orders
                .Include(o => o.Branch)
                .Include(o => o.OrderDetails)
                .ThenInclude(od => od.Product)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            return View(orders);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateOrderStatus(int orderId, string status)
        {
            var order = await _context.Orders.Include(o => o.DeviceImeis).FirstOrDefaultAsync(x => x.Id == orderId);
            if (order != null)
            {
                // CHẶN: Không cho phép Admin đổi thẳng sang Success nếu chưa gán IMEI
                if (status == "Success" && (order.DeviceImeis == null || !order.DeviceImeis.Any()))
                {
                    TempData["ErrorMessage"] = "Không thể đổi sang Hoàn thành! Vui lòng sử dụng chức năng Xuất kho (Fulfill) để quét mã IMEI trước.";
                    return RedirectToAction(nameof(OrderManagement));
                }

                order.Status = status;

                // Nếu hủy đơn thì trả máy về kho
                if (status == "Cancelled" && order.DeviceImeis != null)
                {
                    foreach (var device in order.DeviceImeis)
                    {
                        device.Status = "Available";
                        device.OrderId = null;
                        device.WarrantyActivationDate = null;
                        device.WarrantyExpirationDate = null;
                    }
                }

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đã cập nhật trạng thái đơn hàng #{order.Id} thành công!";
            }
            return RedirectToAction(nameof(OrderManagement));
        }

        [HttpPost]
        public async Task<IActionResult> DeleteOrder(int orderId)
        {
            var order = await _context.Orders.Include(o => o.DeviceImeis).FirstOrDefaultAsync(x => x.Id == orderId);
            if (order != null)
            {
                // Trả máy về kho trước khi xóa đơn
                if (order.DeviceImeis != null)
                {
                    foreach (var device in order.DeviceImeis)
                    {
                        device.Status = "Available";
                        device.OrderId = null;
                    }
                }

                _context.Orders.Remove(order);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đã xóa đơn hàng #{orderId}!";
            }
            return RedirectToAction(nameof(OrderManagement));
        }

        // ==========================================
        // 2. QUẢN LÝ KHO (INVENTORY)
        // ==========================================
        public async Task<IActionResult> ManageInventory()
        {
            var stats = await _context.DeviceImeis
                .Include(d => d.Product)
                .Include(d => d.Branch)
                .Where(d => d.Status == "Available")
                .GroupBy(d => new { d.BranchId, BranchName = d.Branch!.Name, d.ProductId, ProductName = d.Product!.Name })
                .Select(g => new InventoryStatVM
                {
                    BranchName = g.Key.BranchName,
                    ProductName = g.Key.ProductName,
                    AvailableCount = g.Count()
                })
                .OrderBy(x => x.BranchName).ThenBy(x => x.ProductName)
                .ToListAsync();

            return View(stats);
        }

        // ==========================================
        // 3. QUẢN LÝ KHÁCH HÀNG
        // ==========================================
        public async Task<IActionResult> ManageCustomers()
        {
            var admins = await _userManager.GetUsersInRoleAsync("Admin");
            var staffs = await _userManager.GetUsersInRoleAsync("Staff");
            var excludeIds = admins.Select(u => u.Id).Union(staffs.Select(u => u.Id)).ToList();

            var customers = await _userManager.Users.Where(u => !excludeIds.Contains(u.Id)).ToListAsync();
            var customerList = new List<CustomerViewModel>();

            foreach (var cus in customers)
            {
                var orders = await _context.Orders.Where(o => o.UserId == cus.Id && o.Status == "Success").ToListAsync();
                var totalSpent = orders.Sum(o => o.TotalAmount);
                string rank = totalSpent >= 50000000 ? "VÀNG" : (totalSpent >= 20000000 ? "BẠC" : "ĐỒNG");

                customerList.Add(new CustomerViewModel
                {
                    Id = cus.Id,
                    FullName = cus.FullName ?? "Khách hàng",
                    Email = cus.Email,
                    PhoneNumber = cus.PhoneNumber ?? "N/A",
                    TotalSpent = totalSpent,
                    TotalOrders = orders.Count,
                    Rank = rank
                });
            }
            return View(customerList.OrderByDescending(c => c.TotalSpent).ToList());
        }

        // ==========================================
        // 4. QUẢN LÝ CHI NHÁNH (CÁC HÀM BỊ THIẾU ĐÃ ĐƯỢC KHÔI PHỤC)
        // ==========================================
        public async Task<IActionResult> ManageBranches() => View(await _context.Branches.ToListAsync());

        public IActionResult CreateBranch() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateBranch([Bind("Name,Address")] Branch branch)
        {
            ModelState.Remove("Users"); ModelState.Remove("Inventories"); ModelState.Remove("Orders");
            if (ModelState.IsValid)
            {
                _context.Add(branch); await _context.SaveChangesAsync();

                // Tự động tạo kho trống cho chi nhánh mới
                var products = await _context.Products.ToListAsync();
                foreach (var p in products)
                    _context.Inventories.Add(new Inventory { ProductId = p.Id, BranchId = branch.Id, StockQuantity = 0 });
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(ManageBranches));
            }
            return View(branch);
        }

        public async Task<IActionResult> EditBranch(int id)
        {
            var branch = await _context.Branches.FindAsync(id);
            if (branch == null) return NotFound();
            return View(branch);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditBranch(int id, [Bind("Id,Name,Address")] Branch branch)
        {
            if (id != branch.Id) return NotFound();
            ModelState.Remove("Users"); ModelState.Remove("Inventories"); ModelState.Remove("Orders");
            if (ModelState.IsValid)
            {
                _context.Update(branch);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(ManageBranches));
            }
            return View(branch);
        }

        public async Task<IActionResult> DeleteBranch(int id)
        {
            var branch = await _context.Branches.FindAsync(id);
            if (branch != null)
            {
                _context.Branches.Remove(branch);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(ManageBranches));
        }

        // ==========================================
        // 5. QUẢN LÝ NHÂN VIÊN (ĐÃ SỬA LỖI SẬP TRANG)
        // ==========================================
        public async Task<IActionResult> ManageStaff()
        {
            var staffInRole = await _userManager.GetUsersInRoleAsync("Staff");
            var staffIds = staffInRole.Select(s => s.Id).ToList();
            var staffList = await _context.Users.Include(u => u.Branch).Where(u => staffIds.Contains(u.Id)).ToListAsync();
            return View(staffList);
        }

        public IActionResult CreateStaff()
        {
            ViewBag.Branches = new SelectList(_context.Branches, "Id", "Name");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateStaff(string fullName, string email, string password, int branchId)
        {
            var branchExists = await _context.Branches.AnyAsync(b => b.Id == branchId);
            if (!branchExists)
            {
                ModelState.AddModelError("", "Vui lòng chọn một chi nhánh hợp lệ.");
                ViewBag.Branches = new SelectList(_context.Branches, "Id", "Name");
                return View();
            }

            var user = new ApplicationUser { UserName = email, Email = email, FullName = fullName, BranchId = branchId };
            var result = await _userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, "Staff");
                return RedirectToAction(nameof(ManageStaff));
            }

            foreach (var error in result.Errors) ModelState.AddModelError("", error.Description);

            // SỬA LỖI SẬP TRANG: Nếu tạo thất bại, phải load lại ViewBag.Branches
            ViewBag.Branches = new SelectList(_context.Branches, "Id", "Name", branchId);
            return View();
        }

        public async Task<IActionResult> EditStaff(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();
            ViewBag.Branches = new SelectList(_context.Branches, "Id", "Name", user.BranchId);
            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditStaff(string id, string fullName, string email, int branchId, string? newPassword)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            user.FullName = fullName;
            user.Email = email;
            user.UserName = email;
            user.BranchId = branchId;

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                if (!string.IsNullOrWhiteSpace(newPassword))
                {
                    await _userManager.RemovePasswordAsync(user);
                    await _userManager.AddPasswordAsync(user, newPassword);
                }
                return RedirectToAction(nameof(ManageStaff));
            }

            foreach (var error in result.Errors) ModelState.AddModelError("", error.Description);
            ViewBag.Branches = new SelectList(_context.Branches, "Id", "Name", branchId);
            return View(user);
        }

        public async Task<IActionResult> DeleteStaff(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null) await _userManager.DeleteAsync(user);
            return RedirectToAction(nameof(ManageStaff));
        }

        // ==========================================
        // 6. BÁO CÁO DOANH THU
        // ==========================================
        public async Task<IActionResult> RevenueReport()
        {
            var report = await _context.Orders.Where(o => o.Status == "Success").Include(o => o.Branch)
                .GroupBy(o => o.Branch.Name).Select(g => new { BranchName = g.Key ?? "Online", TotalAmount = g.Sum(o => o.TotalAmount) })
                .ToDictionaryAsync(x => x.BranchName, x => x.TotalAmount);
            return View(report);
        }

        // ==========================================
        // 7. QUẢN LÝ BÌNH LUẬN VÀ HỎI ĐÁP (Q&A)
        // ==========================================
        public async Task<IActionResult> ManageComments()
        {
            // 1. Lấy tất cả Đánh giá (Reviews)
            var reviews = await _context.Reviews
                .Include(r => r.Product)
                .Include(r => r.User)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            // 2. Lấy tất cả Câu hỏi (Comments gốc - không phải câu trả lời)
            var questions = await _context.Comments
                .Include(c => c.Product)
                .Include(c => c.User)
                .Include(c => c.Replies) // Include cả câu trả lời để đếm xem đã ai trả lời chưa
                .Where(c => c.ParentCommentId == null) // Chỉ lấy câu hỏi gốc
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            ViewBag.Reviews = reviews;
            ViewBag.Questions = questions;

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> DeleteReview(int id)
        {
            var review = await _context.Reviews.FindAsync(id);
            if (review != null) { _context.Reviews.Remove(review); await _context.SaveChangesAsync(); TempData["SuccessMessage"] = "Đã xóa đánh giá!"; }
            return RedirectToAction(nameof(ManageComments));
        }

        [HttpPost]
        public async Task<IActionResult> DeleteComment(int id)
        {
            // Lấy comment và tất cả các câu trả lời con của nó để xóa sạch
            var comment = await _context.Comments.Include(c => c.Replies).FirstOrDefaultAsync(c => c.Id == id);
            if (comment != null)
            {
                if (comment.Replies != null) _context.Comments.RemoveRange(comment.Replies); // Xóa câu trả lời con
                _context.Comments.Remove(comment); // Xóa câu hỏi gốc
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Đã xóa câu hỏi/bình luận!";
            }
            return RedirectToAction(nameof(ManageComments));
        }

    }
}