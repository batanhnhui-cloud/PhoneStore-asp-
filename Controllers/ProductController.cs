using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PhoneStore.Data;
using PhoneStore.Models;

namespace PhoneStore.Controllers
{
    // Giữ Authorize ở đây để bảo vệ các hàm Quản lý (Thêm, Xóa, Sửa)
    [Authorize(Roles = "Admin,Staff")]
    public class ProductController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _hostEnvironment;
        private readonly UserManager<ApplicationUser> _userManager; // Đã thêm UserManager

        public ProductController(ApplicationDbContext context, IWebHostEnvironment hostEnvironment, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _hostEnvironment = hostEnvironment;
            _userManager = userManager;
        }

        // 1. TRANG DANH SÁCH SẢN PHẨM TRONG ADMIN
        public async Task<IActionResult> Index() => View(await _context.Products.Include(p => p.Category).ToListAsync());

        // ==========================================
        // 2. HÀM CHI TIẾT SẢN PHẨM (ĐÃ TÍCH HỢP BÌNH LUẬN & HỎI ĐÁP)
        // ==========================================
        [AllowAnonymous] // CỰC KỲ QUAN TRỌNG: Cho phép khách hàng vào xem
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Reviews!) // Load thêm bình luận
                .ThenInclude(r => r.User) // Load người bình luận
                .Include(p => p.Comments!) // Load thêm Hỏi đáp (Q&A)
                .ThenInclude(c => c.User)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (product == null) return NotFound();

            // Lấy thêm 4 máy cùng hãng để gợi ý
            ViewBag.RelatedProducts = await _context.Products
                .Where(p => p.CategoryId == product.CategoryId && p.Id != product.Id)
                .Take(4).ToListAsync();

            // KIỂM TRA ĐIỀU KIỆN ĐÁNH GIÁ NẾU KHÁCH ĐÃ ĐĂNG NHẬP
            bool canReview = false;
            bool hasReviewed = false;

            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user != null)
                {
                    // Đã mua hàng thành công chưa?
                    canReview = await _context.Orders
                        .Include(o => o.OrderDetails)
                        .AnyAsync(o => o.UserId == user.Id
                                    && o.Status == "Success"
                                    && o.OrderDetails.Any(od => od.ProductId == id));

                    // Đã bình luận bao giờ chưa?
                    hasReviewed = await _context.Reviews.AnyAsync(r => r.ProductId == id && r.UserId == user.Id);
                }
            }

            ViewBag.CanReview = canReview && !hasReviewed;
            ViewBag.HasReviewed = hasReviewed;

            return View(product);
        }

        // ==========================================
        // HÀM XỬ LÝ LƯU BÌNH LUẬN ĐÁNH GIÁ (REVIEW)
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous] // Mở cho khách hàng gọi hàm này (Bên trong sẽ tự check đăng nhập)
        public async Task<IActionResult> AddReview(int productId, int rating, string comment)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            bool canReview = await _context.Orders
                .Include(o => o.OrderDetails)
                .AnyAsync(o => o.UserId == user.Id && o.Status == "Success" && o.OrderDetails.Any(od => od.ProductId == productId));

            bool hasReviewed = await _context.Reviews.AnyAsync(r => r.ProductId == productId && r.UserId == user.Id);

            if (!canReview)
            {
                TempData["ErrorMsg"] = "Bạn phải mua sản phẩm này và nhận hàng thành công mới được đánh giá!";
                return Redirect($"{Url.Action("Details", "Product", new { id = productId })}#reviews");
            }

            if (hasReviewed)
            {
                TempData["ErrorMsg"] = "Bạn đã đánh giá sản phẩm này rồi!";
                return Redirect($"{Url.Action("Details", "Product", new { id = productId })}#reviews");
            }

            var review = new Review
            {
                ProductId = productId,
                UserId = user.Id,
                Rating = rating,
                Comment = comment,
                CreatedAt = DateTime.Now
            };

            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();

            TempData["SuccessMsg"] = "Cảm ơn bạn đã để lại đánh giá!";
            return Redirect($"{Url.Action("Details", "Product", new { id = productId })}#reviews");
        }

        // ==========================================
        // HÀM XỬ LÝ HỎI ĐÁP (Q&A) - HỖ TRỢ KHÁCH VÃNG LAI
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public async Task<IActionResult> AddComment(int productId, string content, int? parentCommentId, string? guestGender, string? guestName, string? guestPhone, string? guestEmail)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                TempData["ErrorMsg"] = "Nội dung không được để trống!";
                return Redirect($"{Url.Action("Details", "Product", new { id = productId })}#qa");
            }

            var comment = new Comment
            {
                ProductId = productId,
                Content = content,
                ParentCommentId = parentCommentId, // Ghi nhận ID của câu hỏi nếu đây là Trả lời
                CreatedAt = DateTime.Now
            };

            // Nếu ĐÃ đăng nhập
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var user = await _userManager.GetUserAsync(User);
                comment.UserId = user?.Id;
            }
            // Nếu LÀ KHÁCH VÃNG LAI
            else
            {
                if (string.IsNullOrWhiteSpace(guestName) || string.IsNullOrWhiteSpace(guestPhone))
                {
                    TempData["ErrorMsg"] = "Vui lòng nhập Họ tên và Số điện thoại để gửi bình luận!";
                    return Redirect($"{Url.Action("Details", "Product", new { id = productId })}#qa");
                }

                comment.GuestGender = guestGender;
                comment.GuestName = guestName;
                comment.GuestPhone = guestPhone;
                comment.GuestEmail = guestEmail;
            }

            _context.Comments.Add(comment);
            await _context.SaveChangesAsync();

            TempData["SuccessMsg"] = "Đã gửi bình luận thành công!";
            return Redirect($"{Url.Action("Details", "Product", new { id = productId })}#qa");
        }

        // 3. TẠO MỚI SẢN PHẨM
        public IActionResult Create()
        {
            ViewBag.Categories = new SelectList(_context.Categories, "Id", "Name");
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(Product product, IFormFile? ImageFile)
        {
            if (ModelState.IsValid)
            {
                if (ImageFile != null)
                {
                    string fileName = Guid.NewGuid().ToString() + Path.GetExtension(ImageFile.FileName);
                    string path = Path.Combine(_hostEnvironment.WebRootPath, "images/products");
                    if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                    using (var s = new FileStream(Path.Combine(path, fileName), FileMode.Create)) { await ImageFile.CopyToAsync(s); }
                    product.ImageUrl = "/images/products/" + fileName;
                }
                _context.Add(product);
                await _context.SaveChangesAsync();

                var branches = await _context.Branches.ToListAsync();
                foreach (var b in branches) _context.Inventories.Add(new Inventory { ProductId = product.Id, BranchId = b.Id, StockQuantity = 0 });
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewBag.Categories = new SelectList(_context.Categories, "Id", "Name", product.CategoryId);
            return View(product);
        }

        // 4. CHỈNH SỬA
        public async Task<IActionResult> Edit(int id)
        {
            var p = await _context.Products.FindAsync(id);
            if (p == null) return NotFound();
            ViewBag.Categories = new SelectList(_context.Categories, "Id", "Name", p.CategoryId);
            return View(p);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(Product p, IFormFile? ImageFile)
        {
            if (ModelState.IsValid)
            {
                if (ImageFile != null)
                {
                    string fileName = Guid.NewGuid().ToString() + Path.GetExtension(ImageFile.FileName);
                    string path = Path.Combine(_hostEnvironment.WebRootPath, "images/products", fileName);
                    using (var s = new FileStream(path, FileMode.Create)) { await ImageFile.CopyToAsync(s); }
                    p.ImageUrl = "/images/products/" + fileName;
                }
                _context.Update(p);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewBag.Categories = new SelectList(_context.Categories, "Id", "Name", p.CategoryId);
            return View(p);
        }

        // 5. XÓA
        public async Task<IActionResult> Delete(int id)
        {
            var p = await _context.Products.FindAsync(id);
            if (p != null) { _context.Products.Remove(p); await _context.SaveChangesAsync(); }
            return RedirectToAction(nameof(Index));
        }
    }
}