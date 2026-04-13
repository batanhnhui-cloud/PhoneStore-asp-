using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhoneStore.Data;
using PhoneStore.Models;
using PhoneStore.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PhoneStore.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    public class EdiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public EdiController(ApplicationDbContext context)
        {
            _context = context;
        }

        // POST: api/edi/receive-shipment
        // Endpoint này sẽ nhận gói tin từ Hãng đẩy sang
        [HttpPost("receive-shipment")]
        public async Task<IActionResult> ReceiveShipment([FromBody] EdiShipmentPayload payload)
        {
            // 1. Kiểm tra xem file gửi sang có trống không
            if (payload == null || payload.ImeiList == null || !payload.ImeiList.Any())
            {
                return BadRequest(new { success = false, message = "Dữ liệu EDI không hợp lệ hoặc danh sách IMEI rỗng." });
            }

            // 2. Kiểm tra Sản phẩm và Chi nhánh có thực sự tồn tại trong DB SunMobile không
            var productExists = await _context.Products.AnyAsync(p => p.Id == payload.ProductId);
            if (!productExists)
                return BadRequest(new { success = false, message = $"Không tìm thấy Sản phẩm có ID: {payload.ProductId}" });

            var branchExists = await _context.Branches.AnyAsync(b => b.Id == payload.BranchId);
            if (!branchExists)
                return BadRequest(new { success = false, message = $"Không tìm thấy Kho/Chi nhánh có ID: {payload.BranchId}" });

            // 3. Lấy ra những IMEI đã tồn tại trong DB để so sánh (Ngăn chặn lỗi nhập đúp)
            var existingImeis = await _context.DeviceImeis
                .Where(d => payload.ImeiList.Contains(d.Imei))
                .Select(d => d.Imei)
                .ToListAsync();

            var newImeis = new List<DeviceImei>();
            int duplicateCount = 0;

            // 4. Lọc dữ liệu: Chỉ lấy những mã IMEI chưa từng có trong hệ thống
            foreach (var imei in payload.ImeiList)
            {
                if (existingImeis.Contains(imei))
                {
                    duplicateCount++;
                    continue; // Bỏ qua mã này vì đã nhập kho từ trước
                }

                newImeis.Add(new DeviceImei
                {
                    Imei = imei,
                    ProductId = payload.ProductId,
                    BranchId = payload.BranchId,
                    Status = "Available" // Đánh dấu máy này sẵn sàng để bán
                });
            }

            // 5. Lưu hàng loạt vào Cơ sở dữ liệu
            if (newImeis.Any())
            {
                await _context.DeviceImeis.AddRangeAsync(newImeis);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = $"Nhập kho EDI thành công! Đã thêm {newImeis.Count} thiết bị từ thùng {payload.MasterBarcode}. Đã bỏ qua {duplicateCount} IMEI trùng lặp.",
                    masterBarcode = payload.MasterBarcode,
                    importedCount = newImeis.Count
                });
            }

            return BadRequest(new { success = false, message = "Tất cả mã IMEI trong lô hàng này đã bị trùng (đã tồn tại trong hệ thống)." });
        }
    }
}