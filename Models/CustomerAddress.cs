using System.ComponentModel.DataAnnotations;

namespace PhoneStore.Models
{
    public class CustomerAddress
    {
        [Key]
        public int Id { get; set; }

        public string UserId { get; set; } = null!; // Liên kết với tài khoản đăng nhập

        [Required(ErrorMessage = "Vui lòng nhập tên người nhận")]
        public string ReceiverName { get; set; } = null!;

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
        public string Phone { get; set; } = null!;

        [Required(ErrorMessage = "Vui lòng nhập địa chỉ cụ thể")]
        public string FullAddress { get; set; } = null!;

        public bool IsDefault { get; set; } = false; // Có phải địa chỉ mặc định không?
    }
}