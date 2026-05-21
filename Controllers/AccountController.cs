using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhoneStore.Models;

namespace PhoneStore.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _emailSender;
        public AccountController(SignInManager<ApplicationUser> signInManager,
                             UserManager<ApplicationUser> userManager,
                             IEmailSender emailSender) // <--- Thêm tham số này
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _emailSender = emailSender; // <--- Gán giá trị
        }

        // --- ĐĂNG NHẬP ---
        [HttpGet]
        public IActionResult Login() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, false);
                if (result.Succeeded)
                {
                    var user = await _userManager.FindByEmailAsync(model.Email);
                    var roles = await _userManager.GetRolesAsync(user);

                    if (roles.Contains("Admin") || roles.Contains("Staff"))
                        return RedirectToAction("Inventory", "Staff");

                    return RedirectToAction("Index", "Home");
                }
                ModelState.AddModelError(string.Empty, "Tài khoản hoặc mật khẩu không chính xác.");
            }
            return View(model);
        }

        // --- ĐĂNG KÝ TÀI KHOẢN KHÁCH HÀNG ---
        [HttpGet]
        public IActionResult Register() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                // 1. KIỂM TRA SỐ ĐIỆN THOẠI ĐÃ TỒN TẠI CHƯA
                var phoneExists = await _userManager.Users.AnyAsync(u => u.PhoneNumber == model.PhoneNumber);

                if (phoneExists)
                {
                    ModelState.AddModelError("PhoneNumber", "Số điện thoại này đã được đăng ký cho một tài khoản khác.");
                    return View(model);
                }

                // 2. Tiếp tục logic đăng ký cũ của bạn...
                var user = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    FullName = model.FullName,
                    PhoneNumber = model.PhoneNumber
                };

                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    await _signInManager.SignInAsync(user, isPersistent: false);
                    return RedirectToAction("Index", "Home");
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }
            return View(model);
        }

        // 1. Mở trang nhập email
        [HttpGet] public IActionResult ForgotPassword() => View();

        // 2. Xử lý gửi email reset
        [HttpPost]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user != null)
            {
                var code = await _userManager.GeneratePasswordResetTokenAsync(user);
                var callbackUrl = Url.Action("ResetPassword", "Account", new { email = model.Email, code = code }, Request.Scheme);
                await _emailSender.SendEmailAsync(model.Email, "Đặt lại mật khẩu", $"Bấm vào đây để reset: {callbackUrl}");
            }
            return View("ForgotPasswordConfirmation"); // Tạo view này thông báo khách check mail
        }

        // 3. Mở trang đặt mật khẩu mới
        [HttpGet] public IActionResult ResetPassword(string email, string code) => View(new ResetPasswordViewModel { Email = email, Code = code });

        // 4. Lưu mật khẩu mới
        [HttpPost]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);
            var result = await _userManager.ResetPasswordAsync(user, model.Code, model.Password);
            if (result.Succeeded) return View("ResetPasswordConfirmation");

            foreach (var error in result.Errors) ModelState.AddModelError("", error.Description);
            return View(model);
        }

        // --- ĐĂNG XUẤT ---
        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }
    }
}