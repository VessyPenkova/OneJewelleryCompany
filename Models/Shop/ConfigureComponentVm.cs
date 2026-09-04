using System.ComponentModel.DataAnnotations;

namespace OneJevelsCompany.Web.Models.Shop
{
    public class ConfigureComponentVm
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string? ImageUrl { get; set; }
        public decimal Price { get; set; }

        public List<string> DimensionOptions { get; set; } = new();
        public string? SelectedDimension { get; set; }

        [Range(1, 9999)]
        public int Quantity { get; set; } = 1;

        public int MaxQty { get; set; } = 1;
    }
}
