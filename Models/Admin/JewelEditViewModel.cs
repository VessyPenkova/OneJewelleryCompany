using System.ComponentModel.DataAnnotations;
using OneJevelsCompany.Core.Enums;

namespace OneJevelsCompany.Web.Models.Admin
{
    public class JewelEditViewModel
    {
        public int Id { get; set; }

        [Required, MaxLength(160)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public JewelCategory Category { get; set; }

        [Range(0, double.MaxValue)]
        public decimal BasePrice { get; set; }

        [Range(0, int.MaxValue)]
        public int QuantityOnHand { get; set; }

        // for preview on Edit
        public string? CurrentImageUrl { get; set; }
    }
}