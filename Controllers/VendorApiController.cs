using Microsoft.AspNetCore.Mvc;
using PhoneStore.Data;
using PhoneStore.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PhoneStore.Controllers.Api
{
    // Đường dẫn API này là dành riêng cho đối tác (Apple, Samsung...)
    [Route("api/vendor")]
    [ApiController]
    public class VendorApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public VendorApiController(ApplicationDbContext context)
        {
            _context = context;
        }

        public class VendorShipmentPayload
        {
            public string MasterBarcode { get; set; } = null!;
            public int ProductId { get; set; }
            public int BranchId { get; set; }
            public int Quantity { get; set; }
        }

        // API NHẬN DỮ LIỆU TỪ HÃNG (Không có giao diện web)
        [HttpPost("push-edi")]
        public async Task<IActionResult> ReceiveDataFromApple([FromBody] VendorShipmentPayload payload)
        {
            if (payload == null || payload.Quantity <= 0)
                return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ!" });

            // Kiểm tra chống đẩy trùng 1 mã thùng
            bool exists = _context.DeviceImeis.Any(d => d.MasterBarcode == payload.MasterBarcode);
            if (exists) return BadRequest(new { success = false, message = "Mã thùng này đã được gửi trước đó!" });

            // Tự động sinh mã IMEI theo yêu cầu của Hãng và lưu ở trạng thái "Đang chờ nhập kho"
            Random rnd = new Random();
            for (int i = 0; i < payload.Quantity; i++)
            {
                _context.DeviceImeis.Add(new DeviceImei
                {
                    Imei = "354" + rnd.Next(100000, 999999).ToString() + rnd.Next(100000, 999999).ToString(),
                    ProductId = payload.ProductId,
                    BranchId = payload.BranchId,
                    MasterBarcode = payload.MasterBarcode,
                    Status = "Pending_EDI" // <-- Lệnh cấm bán: Máy này mới chỉ có data, hàng thật chưa tới kho!
                });
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Hệ thống SunMobile xác nhận đã nhận được vận đơn điện tử từ Hãng.",
                masterBarcode = payload.MasterBarcode,
            });
        }
    }
}