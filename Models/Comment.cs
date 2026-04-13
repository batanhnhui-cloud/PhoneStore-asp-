using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhoneStore.Models
{
    public class Comment
    {
        [Key]
        public int Id { get; set; }

        public int ProductId { get; set; }
        [ForeignKey("ProductId")]
        public Product? Product { get; set; }

        // UserId bây giờ có dấu ? (có thể null nếu là khách vãng lai)
        public string? UserId { get; set; }
        [ForeignKey("UserId")]
        public ApplicationUser? User { get; set; }

        // --- THÊM 4 CỘT MỚI CHO KHÁCH VÃNG LAI ---
        public string? GuestGender { get; set; } // Anh hoặc Chị
        public string? GuestName { get; set; }   // Họ tên khách
        public string? GuestPhone { get; set; }  // Số điện thoại
        public string? GuestEmail { get; set; }  // Email (Có thể null)

        [Required(ErrorMessage = "Vui lòng nhập nội dung")]
        public string Content { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public int? ParentCommentId { get; set; }
        [ForeignKey("ParentCommentId")]
        public Comment? ParentComment { get; set; }

        public ICollection<Comment>? Replies { get; set; }
    }
}