using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PhoneStore.Data;
using PhoneStore.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PhoneStore.Controllers
{
    public class AutoImportRequest { public int BranchId { get; set; } public int ProductId { get; set; } public int Quantity { get; set; } public string MasterBarcode { get; set; } = null!; }
    public class BarcodeRequest { public string MasterBarcode { get; set; } = null!; }

    // Model dùng để hiển thị Danh sách Khách hàng
    public class CustomerViewModel
    {
        public string Id { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public decimal TotalSpent { get; set; }
        public int TotalOrders { get; set; }
        public string Rank { get; set; } = null!;
    }

    // ==========================================
    // THÊM 2 CLASS NÀY CHO TỒN KHO THÔNG MINH
    // ==========================================
    public class InventoryProductVM
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = null!;
        public string? ImageUrl { get; set; }
        public int TotalStock { get; set; }
        public List<BranchStockVM> BranchStocks { get; set; } = new List<BranchStockVM>();
    }

    public class BranchStockVM
    {
        public int BranchId { get; set; }
        public string BranchName { get; set; } = null!;
        public int Quantity { get; set; }
    }

    [Authorize(Roles = "Staff, Admin")]
    public class StaffController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public StaffController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // ==========================================
        // 1. QUẢN LÝ KHÁCH HÀNG (CRM)
        // ==========================================
        [HttpGet]
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
                    PhoneNumber = cus.PhoneNumber ?? "Chưa cập nhật",
                    TotalSpent = totalSpent,
                    TotalOrders = orders.Count,
                    Rank = rank
                });
            }
            return View(customerList.OrderByDescending(c => c.TotalSpent).ToList());
        }

        [HttpGet]
        public async Task<IActionResult> EditCustomer(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();
            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCustomer(string id, string fullName, string phoneNumber)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            user.FullName = fullName;
            user.PhoneNumber = phoneNumber;

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "Cập nhật thông tin khách hàng thành công!";
                return RedirectToAction(nameof(ManageCustomers));
            }
            foreach (var error in result.Errors) ModelState.AddModelError("", error.Description);
            return View(user);
        }

        [HttpPost]
        public async Task<IActionResult> DeleteCustomer(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null)
            {
                await _userManager.DeleteAsync(user);
                TempData["SuccessMessage"] = "Đã xóa khách hàng thành công!";
            }
            return RedirectToAction(nameof(ManageCustomers));
        }

        // ==========================================
        // 2. NGHIỆP VỤ KHO VÀ ĐƠN HÀNG
        // ==========================================

        // ĐÃ NÂNG CẤP HÀM NÀY
        [HttpGet]
        public async Task<IActionResult> Inventory(int? searchBranchId)
        {
            // Lấy danh sách chi nhánh để làm bộ lọc
            ViewBag.Branches = new SelectList(await _context.Branches.ToListAsync(), "Id", "Name", searchBranchId);

            var query = _context.DeviceImeis
                .Include(d => d.Product)
                .Include(d => d.Branch)
                .Where(d => d.Status == "Available");

            // Lọc theo chi nhánh nếu có chọn
            if (searchBranchId.HasValue)
            {
                query = query.Where(d => d.BranchId == searchBranchId);
            }

            var rawData = await query.ToListAsync();

            // Nhóm dữ liệu: Lớp 1 là Sản phẩm -> Lớp 2 là Chi nhánh
            var stats = rawData
                .GroupBy(d => new { d.ProductId, d.Product!.Name, d.Product.ImageUrl })
                .Select(g => new InventoryProductVM
                {
                    ProductId = g.Key.ProductId,
                    ProductName = g.Key.Name,
                    ImageUrl = g.Key.ImageUrl,
                    TotalStock = g.Count(),
                    BranchStocks = g.GroupBy(b => new { b.BranchId, b.Branch!.Name })
                                    .Select(bg => new BranchStockVM
                                    {
                                        BranchId = bg.Key.BranchId,
                                        BranchName = bg.Key.Name,
                                        Quantity = bg.Count()
                                    }).ToList()
                })
                .OrderByDescending(x => x.TotalStock)
                .ToList();

            return View(stats);
        }

        [HttpGet]
        public async Task<IActionResult> ImeiDetails(int branchId, int productId)
        {
            var imeis = await _context.DeviceImeis
                .Include(d => d.Product)
                .Include(d => d.Branch)
                .Where(d => d.BranchId == branchId && d.ProductId == productId && d.Status == "Available")
                .OrderByDescending(d => d.Id)
                .ToListAsync();

            if (!imeis.Any()) return RedirectToAction(nameof(Inventory));

            ViewBag.BranchName = imeis.First().Branch!.Name;
            ViewBag.ProductName = imeis.First().Product!.Name;

            return View(imeis);
        }

        [HttpGet]
        public async Task<IActionResult> ImportImei()
        {
            ViewBag.Branches = new SelectList(await _context.Branches.ToListAsync(), "Id", "Name");
            ViewBag.Products = new SelectList(await _context.Products.ToListAsync(), "Id", "Name");
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ProcessEdiBarcode([FromBody] BarcodeRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.MasterBarcode))
                return BadRequest(new { success = false, message = "Vui lòng nhập mã thùng!" });

            string barcode = request.MasterBarcode.Trim();
            var pendingDevices = await _context.DeviceImeis
                .Include(d => d.Product)
                .Include(d => d.Branch)
                .Where(d => d.MasterBarcode == barcode && d.Status == "Pending_EDI")
                .ToListAsync();

            if (!pendingDevices.Any())
            {
                bool alreadyImported = await _context.DeviceImeis.AnyAsync(d => d.MasterBarcode == barcode && d.Status == "Available");
                if (alreadyImported)
                    return Json(new { success = false, message = $"CẢNH BÁO: Thùng hàng [{barcode}] này ĐÃ ĐƯỢC NHẬP VÀO KHO trước đó!" });

                return Json(new { success = false, message = $"LỖI: Mã thùng [{barcode}] không hợp lệ! Không có vận đơn điện tử từ Hãng gửi tới." });
            }

            var firstDevice = pendingDevices.First();
            return Json(new
            {
                success = true,
                supplierName = "Hãng phân phối ủy quyền",
                productName = firstDevice.Product!.Name,
                branchName = firstDevice.Branch!.Name,
                quantity = pendingDevices.Count,
                imeiList = pendingDevices.Select(d => d.Imei).ToList()
            });
        }

        [HttpPost]
        public async Task<IActionResult> ConfirmEdiImport([FromBody] BarcodeRequest request)
        {
            var pendingDevices = await _context.DeviceImeis
                .Where(d => d.MasterBarcode == request.MasterBarcode && d.Status == "Pending_EDI")
                .ToListAsync();

            if (!pendingDevices.Any())
                return Json(new { success = false, message = "Lô hàng không tồn tại hoặc đã được xử lý!" });

            foreach (var device in pendingDevices)
            {
                device.Status = "Available";
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = $"XÁC NHẬN THÀNH CÔNG! Đã cộng {pendingDevices.Count} máy vào Tồn kho." });
        }

        [HttpPost]
        public async Task<IActionResult> ImportImei(int branchId, int productId, string imeiList)
        {
            if (string.IsNullOrWhiteSpace(imeiList))
            {
                TempData["ErrorMessage"] = "Vui lòng nhập ít nhất 1 mã IMEI!";
                return RedirectToAction(nameof(ImportImei));
            }

            var imeis = imeiList.Split(new[] { '\r', '\n', ',' }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(i => i.Trim()).Distinct().ToList();

            int successCount = 0;
            int duplicateCount = 0;

            foreach (var imei in imeis)
            {
                if (imei.Length != 15 || !imei.All(char.IsDigit)) continue;

                bool exists = await _context.DeviceImeis.AnyAsync(d => d.Imei == imei);
                if (exists)
                {
                    duplicateCount++;
                    continue;
                }

                _context.DeviceImeis.Add(new DeviceImei
                {
                    Imei = imei,
                    ProductId = productId,
                    BranchId = branchId,
                    Status = "Available"
                });
                successCount++;
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Đã nhập kho thủ công thành công {successCount} máy. Bỏ qua {duplicateCount} mã trùng lặp.";
            return RedirectToAction(nameof(Inventory));
        }

        [HttpGet]
        public async Task<IActionResult> GetImportHistory()
        {
            var history = await _context.DeviceImeis
                .Include(d => d.Product)
                .Include(d => d.Branch)
                .Where(d => d.MasterBarcode != null && d.Status != "Pending_EDI")
                .GroupBy(d => new { d.MasterBarcode, ProductName = d.Product!.Name, BranchName = d.Branch!.Name })
                .Select(g => new {
                    masterBarcode = g.Key.MasterBarcode,
                    productName = g.Key.ProductName,
                    branchName = g.Key.BranchName,
                    quantity = g.Count()
                })
                .OrderByDescending(x => x.masterBarcode)
                .Take(20)
                .ToListAsync();

            return Json(new { success = true, data = history });
        }

        [HttpGet]
        public async Task<IActionResult> OrderManagement()
        {
            var orders = await _context.Orders
                .Include(o => o.OrderDetails).ThenInclude(od => od.Product)
                .Include(o => o.Branch).OrderByDescending(o => o.OrderDate).ToListAsync();
            return View(orders);
        }

        [HttpGet]
        public async Task<IActionResult> FulfillOrder(int id)
        {
            var order = await _context.Orders.Include(o => o.OrderDetails).ThenInclude(od => od.Product).FirstOrDefaultAsync(o => o.Id == id);
            if (order == null || order.Status != "Pending") return RedirectToAction(nameof(OrderManagement));
            return View(order);
        }

        [HttpPost]
        public async Task<IActionResult> FulfillOrder(int orderId, List<string> scannedImeis)
        {
            var order = await _context.Orders.Include(o => o.OrderDetails).ThenInclude(od => od.Product).FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null) return NotFound();

            scannedImeis = scannedImeis.Where(i => !string.IsNullOrWhiteSpace(i)).Select(i => i.Trim()).ToList();

            List<DeviceImei> devicesToProcess = new List<DeviceImei>();

            foreach (var detail in order.OrderDetails)
            {
                var imeisForThisProduct = new List<DeviceImei>();

                foreach (var sImei in scannedImeis.ToList())
                {
                    var d = await _context.DeviceImeis.FirstOrDefaultAsync(x => x.Imei == sImei && x.Status == "Available");

                    if (d != null && d.ProductId == detail.ProductId)
                    {
                        imeisForThisProduct.Add(d);
                        scannedImeis.Remove(sImei);
                    }
                }

                if (imeisForThisProduct.Count != detail.Quantity)
                {
                    TempData["ErrorMessage"] = $"Lỗi: Sản phẩm [{detail.Product?.Name}] yêu cầu {detail.Quantity} máy, nhưng mã IMEI bạn nhập không khớp hoặc không đúng loại sản phẩm này!";
                    return RedirectToAction(nameof(FulfillOrder), new { id = orderId });
                }

                devicesToProcess.AddRange(imeisForThisProduct);
            }

            foreach (var device in devicesToProcess)
            {
                device.Status = "Sold";
                device.OrderId = order.Id;
                device.WarrantyActivationDate = DateTime.Now;
                device.WarrantyExpirationDate = DateTime.Now.AddMonths(12);
            }

            order.Status = "Success";
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"✅ Đã xuất kho thành công Đơn hàng #{orderId}!";
            return RedirectToAction(nameof(OrderManagement));
        }

        [HttpPost]
        public async Task<IActionResult> UpdateOrderStatus(int id, string status)
        {
            var order = await _context.Orders.Include(o => o.DeviceImeis).FirstOrDefaultAsync(o => o.Id == id);
            if (order != null)
            {
                order.Status = status;
                if (status == "Cancelled" && order.DeviceImeis != null)
                {
                    foreach (var device in order.DeviceImeis)
                    {
                        device.Status = "Available"; device.OrderId = null;
                        device.WarrantyActivationDate = null;
                        device.WarrantyExpirationDate = null;
                    }
                    TempData["SuccessMessage"] = $"Đã hủy đơn #{id} và trả máy về kho!";
                }
                else TempData["SuccessMessage"] = $"Đã cập nhật trạng thái đơn #{id}!";
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(OrderManagement));
        }

        [HttpGet]
        public async Task<IActionResult> POS()
        {
            ViewBag.Branches = new SelectList(await _context.Branches.ToListAsync(), "Id", "Name");
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> POS(string customerName, string phone, int branchId, string scannedImeis)
        {
            if (string.IsNullOrWhiteSpace(scannedImeis)) return RedirectToAction(nameof(POS));
            var imeis = scannedImeis.Split(new[] { '\r', '\n', ',' }, StringSplitOptions.RemoveEmptyEntries).Select(i => i.Trim()).Distinct().ToList();

            var validDevices = await _context.DeviceImeis.Include(d => d.Product)
                .Where(d => imeis.Contains(d.Imei) && d.Status == "Available" && d.BranchId == branchId).ToListAsync();

            if (validDevices.Count != imeis.Count)
            {
                TempData["ErrorMessage"] = "Lỗi: IMEI không tồn tại hoặc không thuộc chi nhánh này!";
                return RedirectToAction(nameof(POS));
            }

            var newOrder = new Order { CustomerName = customerName ?? "Khách lẻ", Phone = phone ?? "", Address = "Mua tại quầy", OrderDate = DateTime.Now, Status = "Success", BranchId = branchId, TotalAmount = validDevices.Sum(d => d.Product!.Price) };
            _context.Orders.Add(newOrder); await _context.SaveChangesAsync();

            foreach (var group in validDevices.GroupBy(d => d.ProductId))
                _context.OrderDetails.Add(new OrderDetail { OrderId = newOrder.Id, ProductId = group.Key, Quantity = group.Count(), Price = group.First().Product!.Price });

            foreach (var device in validDevices)
            {
                device.Status = "Sold"; device.OrderId = newOrder.Id;
                device.WarrantyActivationDate = DateTime.Now; device.WarrantyExpirationDate = DateTime.Now.AddMonths(12);
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"✅ Đã tạo hóa đơn #{newOrder.Id}!";
            return RedirectToAction(nameof(POS));
        }

        [HttpGet]
        public async Task<IActionResult> TransferStock()
        {
            ViewBag.Branches = new SelectList(await _context.Branches.ToListAsync(), "Id", "Name");
            var pendingTransfers = await _context.ImeiTransfers.Include(t => t.DeviceImei).ThenInclude(d => d.Product).Include(t => t.FromBranch).Include(t => t.ToBranch)
                .Where(t => t.Status == "Pending").OrderByDescending(t => t.TransferDate).ToListAsync();
            return View(pendingTransfers);
        }

        [HttpPost]
        public async Task<IActionResult> CreateTransfer(int fromBranchId, int toBranchId, string scannedImeis)
        {
            if (string.IsNullOrWhiteSpace(scannedImeis)) return RedirectToAction(nameof(TransferStock));
            var imeis = scannedImeis.Split(new[] { '\r', '\n', ',' }, StringSplitOptions.RemoveEmptyEntries).Select(i => i.Trim()).Distinct().ToList();
            var validDevices = await _context.DeviceImeis.Where(d => imeis.Contains(d.Imei) && d.Status == "Available" && d.BranchId == fromBranchId).ToListAsync();

            foreach (var device in validDevices)
            {
                device.Status = "Transferring";
                _context.ImeiTransfers.Add(new ImeiTransfer { DeviceImeiId = device.Id, FromBranchId = fromBranchId, ToBranchId = toBranchId, TransferDate = DateTime.Now, Status = "Pending" });
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(TransferStock));
        }

        [HttpPost]
        public async Task<IActionResult> ReceiveTransfer(int transferId)
        {
            var transfer = await _context.ImeiTransfers.Include(t => t.DeviceImei).FirstOrDefaultAsync(t => t.Id == transferId);
            if (transfer != null && transfer.Status == "Pending")
            {
                transfer.Status = "Completed"; transfer.ReceiveDate = DateTime.Now;
                if (transfer.DeviceImei != null) { transfer.DeviceImei.BranchId = transfer.ToBranchId; transfer.DeviceImei.Status = "Available"; }
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(TransferStock));
        }

        // ==========================================
        // 3. QUẢN LÝ BÌNH LUẬN VÀ HỎI ĐÁP
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> ManageComments()
        {
            var reviews = await _context.Reviews
                .Include(r => r.Product)
                .Include(r => r.User)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            var questions = await _context.Comments
                .Include(c => c.Product)
                .Include(c => c.User)
                .Include(c => c.Replies)
                .Where(c => c.ParentCommentId == null)
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
            if (review != null)
            {
                _context.Reviews.Remove(review);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Đã xóa đánh giá!";
            }
            return RedirectToAction(nameof(ManageComments));
        }

        [HttpPost]
        public async Task<IActionResult> DeleteComment(int id)
        {
            var comment = await _context.Comments.Include(c => c.Replies).FirstOrDefaultAsync(c => c.Id == id);
            if (comment != null)
            {
                if (comment.Replies != null) _context.Comments.RemoveRange(comment.Replies);
                _context.Comments.Remove(comment);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Đã xóa câu hỏi!";
            }
            return RedirectToAction(nameof(ManageComments));
        }
    }
}