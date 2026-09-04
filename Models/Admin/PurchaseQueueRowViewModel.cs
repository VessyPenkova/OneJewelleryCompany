namespace OneJevelsCompany.Web.Models.Admin
{
    public class PurchaseQueueRowViewModel
    {
        public int PurchaseNeedId { get; set; }

        public int ComponentId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Sku { get; set; }

        public string? Dimensions { get; set; }

        public string? SizeLabel { get; set; }

        public string? ImageUrl { get; set; }

        public int Stock { get; set; }

        public decimal Price { get; set; }

        public int NeededQty { get; set; }

        public int MinOrderQty { get; set; }

        public int SuggestedQty { get; set; }

        public DateTime LastUpdatedUtc { get; set; }
    }
}