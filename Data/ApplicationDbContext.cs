using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PhoneStore.Models;

namespace PhoneStore.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Product> Products { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Branch> Branches { get; set; }
        public DbSet<Inventory> Inventories { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderDetail> OrderDetails { get; set; }
        public DbSet<DeviceImei> DeviceImeis { get; set; }
        public DbSet<ImeiTransfer> ImeiTransfers { get; set; }
        public DbSet<CustomerAddress> CustomerAddresses { get; set; }

        public DbSet<Review> Reviews { get; set; }

        public DbSet<Comment> Comments { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // [THÊM MỚI] Ép IMEI phải là duy nhất, không được trùng lặp trong toàn hệ thống
            builder.Entity<DeviceImei>()
                .HasIndex(d => d.Imei)
                .IsUnique();

            // 1. Cho bảng Sản phẩm (Code cũ của bạn)
            builder.Entity<Product>()
                .Property(p => p.Price)
                .HasColumnType("decimal(18,2)");

            // 2. Cho bảng Đơn hàng (Code cũ của bạn)
            builder.Entity<Order>()
                .Property(o => o.TotalAmount)
                .HasColumnType("decimal(18,2)");

            // 3. Cho bảng Chi tiết đơn hàng (Code cũ của bạn)
            builder.Entity<OrderDetail>()
                .Property(od => od.Price)
                .HasColumnType("decimal(18,2)");
        }
    }
}