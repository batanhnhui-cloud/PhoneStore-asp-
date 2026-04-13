using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations;
namespace PhoneStore.Models
{
    public class Product
    {
        public int Id { get; set; }
        [Required] public string Name { get; set; }
        [Required] public decimal Price { get; set; } // Chốt kiểu decimal
        public string? ImageUrl { get; set; }
        public int CategoryId { get; set; }
        public virtual Category? Category { get; set; }
        public virtual ICollection<DeviceImei> DeviceImeis { get; set; } = new List<DeviceImei>();
        public string? Specifications { get; set; }

        public ICollection<Review>? Reviews { get; set; }

        public ICollection<Comment>? Comments { get; set; }

    }
}