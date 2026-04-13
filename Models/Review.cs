using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhoneStore.Models
{
    public class Review
    {
        [Key]
        public int Id { get; set; }

        public int ProductId { get; set; }
        [ForeignKey("ProductId")]
        public Product? Product { get; set; }

        public string UserId { get; set; } = null!;
        [ForeignKey("UserId")]
        public ApplicationUser? User { get; set; }

        [Range(1, 5)]
        public int Rating { get; set; } // Số sao (1 đến 5)

        [Required(ErrorMessage = "Vui lòng nhập nội dung đánh giá")]
        public string Comment { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}