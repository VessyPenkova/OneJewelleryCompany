using System.ComponentModel.DataAnnotations;

namespace OneJevelsCompany.Web.Models.Admin
{
    public class AdminComponentEditVm
    {
        public int Id { get; set; }

        [Required, MaxLength(160)]
        public string Name { get; set; } = string.Empty;

        public decimal Price { get; set; }
        public int QuantityOnHand { get; set; }

        [MaxLength(80)]
        public string? Sku { get; set; }

        [MaxLength(40)]
        public string? Color { get; set; }

        [MaxLength(40)]
        public string? SizeLabel { get; set; }

        [MaxLength(120)]
        public string? Dimensions { get; set; }

        public string? CurrentImageUrl { get; set; }

        [MaxLength(4000)]
        public string? Description { get; set; }
    }
}
