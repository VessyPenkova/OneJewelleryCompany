using System.ComponentModel.DataAnnotations;

namespace OneJevelsCompany.Web.Models
{
    public class Order
    {
        public int Id { get; set; }

        [MaxLength(40)]
        public string OrderType { get; set; } = "Jewelry";

        [MaxLength(160)]
        public string? CustomerEmail { get; set; }

        [MaxLength(200)]
        public string? ShippingAddress { get; set; }

        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        public decimal Total { get; set; }

        [MaxLength(40)]
        public string Status { get; set; } = "Pending";

        [MaxLength(100)]
        public string? PaymentProviderId { get; set; }

        public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    }
}