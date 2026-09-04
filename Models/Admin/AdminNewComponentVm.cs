using System.ComponentModel.DataAnnotations;

namespace OneJevelsCompany.Web.Models.Admin
{
    public class AdminNewComponentVm
    {
        [Required, MaxLength(160)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Category")]
        public int ComponentCategoryId { get; set; }

        [Range(0, double.MaxValue)]
        public decimal Price { get; set; }

        [MaxLength(80)]
        public string? Sku { get; set; }

        [MaxLength(40)]
        public string? Color { get; set; }

        [MaxLength(40)]
        public string? SizeLabel { get; set; }

        // e.g. "4x4;5x5;6x6"
        [MaxLength(120)]
        public string? Dimensions { get; set; }
    }


}
