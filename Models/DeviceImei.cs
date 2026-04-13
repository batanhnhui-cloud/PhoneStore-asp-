using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhoneStore.Models
{
    public class DeviceImei
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(15, MinimumLength = 15)]
        public string Imei { get; set; } = null!;

        public int ProductId { get; set; }
        [ForeignKey("ProductId")]
        public virtual Product? Product { get; set; }

        public int BranchId { get; set; }
        [ForeignKey("BranchId")]
        public virtual Branch? Branch { get; set; }

        public string Status { get; set; } = "Available";

        // CHIẾC CỘT QUAN TRỌNG ĐANG BỊ THIẾU NẰM Ở ĐÂY:
        public string? MasterBarcode { get; set; }

        public int? OrderId { get; set; }
        [ForeignKey("OrderId")]
        public virtual Order? Order { get; set; }

        public DateTime? WarrantyActivationDate { get; set; }
        public DateTime? WarrantyExpirationDate { get; set; }
    }
}