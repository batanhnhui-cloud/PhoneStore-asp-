namespace PhoneStore.Models
{
    public class InventoryStatVM
    {
        public int BranchId { get; set; }  // Thêm mới
        public int ProductId { get; set; } // Thêm mới
        public string BranchName { get; set; } = null!;
        public string ProductName { get; set; } = null!;
        public int AvailableCount { get; set; }
    }
}