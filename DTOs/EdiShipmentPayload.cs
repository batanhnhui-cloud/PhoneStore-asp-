using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PhoneStore.DTOs
{
    // Lớp này dùng để hứng dữ liệu từ file JSON mà Hãng đẩy sang API của SunMobile
    public class EdiShipmentPayload
    {
        [Required(ErrorMessage = "Thiếu mã thùng (Master Barcode)")]
        public string MasterBarcode { get; set; } = null!;

        [Required(ErrorMessage = "Thiếu mã nhà cung cấp")]
        public string SupplierCode { get; set; } = null!;

        [Required(ErrorMessage = "Chưa xác định được ID Sản phẩm")]
        public int ProductId { get; set; }

        [Required(ErrorMessage = "Chưa xác định được ID Kho/Chi nhánh nhập")]
        public int BranchId { get; set; }

        [Required(ErrorMessage = "Danh sách IMEI không được để trống")]
        public List<string> ImeiList { get; set; } = new List<string>();
    }
}