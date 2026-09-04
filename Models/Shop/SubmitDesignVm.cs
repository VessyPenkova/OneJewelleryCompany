using static OneJevelsCompany.Web.Controllers.ShopController;

namespace OneJevelsCompany.Web.Models.Shop
{
    public class SubmitDesignVm
    {
        public string Category { get; set; } = "Bracelet";
        public int Quantity { get; set; } = 1;
        public decimal? UnitPriceEstimate { get; set; }
        public decimal? LengthCm { get; set; }
        public int? BeadMm { get; set; }
        public string? Mode { get; set; }
        public int? Tilt { get; set; }
        public int? Rotate { get; set; }

        public string? CustomerName { get; set; }
        public string? CustomerEmail { get; set; }
        public string? CustomerPhone { get; set; }

        public string? PreviewDataUrl { get; set; }        // <— screenshot
        public List<SubmitDesignRow> Rows { get; set; } = new();
    }
}
