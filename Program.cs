using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PhoneStore.Data;
using PhoneStore.Models;

var builder = WebApplication.CreateBuilder(args);

// --- 1. CẤU HÌNH DATABASE (SQL SERVER) ---
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Không tìm thấy chuỗi kết nối 'DefaultConnection'.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// --- 2. CẤU HÌNH IDENTITY (Xác thực & Phân quyền) ---
// Sử dụng ApplicationUser để hỗ trợ thuộc tính BranchId (Chi nhánh)
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options => {
    // Cấu hình mật khẩu đơn giản để thuận tiện cho việc làm đồ án
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 4;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;

    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// --- 3. CẤU HÌNH COOKIE CHO LOGIN TÙY CHỈNH ---
// Giúp hệ thống biết đường dẫn đến AccountController/Login của bạn
builder.Services.ConfigureApplicationCookie(options => {
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

// --- 4. CẤU HÌNH SESSION (Dành cho Giỏ hàng) ---
builder.Services.AddDistributedMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSession(options => {
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// --- 5. ĐĂNG KÝ CÁC DỊCH VỤ MVC ---
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// ====================================================================
// [THÊM MỚI QUAN TRỌNG]: CẤU HÌNH CORS CHO API (CẤP QUYỀN CHO APPLE)
// ====================================================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAppleEDI", policy =>
    {
        policy.AllowAnyOrigin()  // Cho phép mọi tên miền (kể cả file HTML ở Desktop)
              .AllowAnyMethod()  // Cho phép mọi method (POST, GET...)
              .AllowAnyHeader(); // Cho phép mọi header (JSON)
    });
});

var app = builder.Build();

// --- 6. KHỞI TẠO DỮ LIỆU (SEED DATA) ---
// Tự động tạo Roles (Admin, Staff, Customer) và tài khoản Admin mặc định
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        await DbInitializer.SeedRolesAndAdminAsync(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Lỗi khi khởi tạo dữ liệu mẫu (Seeding).");
    }
}

// --- 7. CẤU HÌNH HTTP REQUEST PIPELINE ---
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// ====================================================================
// [THÊM MỚI QUAN TRỌNG]: KÍCH HOẠT CORS VỪA CẤU HÌNH Ở TRÊN
// Bắt buộc phải đặt ở đây: Nằm DƯỚI UseRouting và TRÊN UseAuthorization
// ====================================================================
app.UseCors("AllowAppleEDI");

app.UseSession(); // LƯU Ý: Session phải đặt TRƯỚC Authentication
app.UseAuthentication(); // Ai là người đang truy cập?
app.UseAuthorization();  // Người đó có quyền làm gì?

// --- 8. CẤU HÌNH ROUTING ---
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

app.Run();