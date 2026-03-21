namespace OneJevelsCompany.Web.Models
{
    public class OrderItem
    {
        public int Id { get; set; }

        public int OrderId { get; set; }
        public Order Order { get; set; } = default!;

        public string Title { get; set; } = string.Empty;

        public JewelCategory Category { get; set; }

        public int Quantity { get; set; }

        public decimal UnitPrice { get; set; }

        public string? ComponentsSummary { get; set; }

        // Image snapshot for admin/order history screens
        public string? ImageUrl { get; set; }

        // Existing
        public string? ComponentIdsCsv { get; set; }

        public int? ReadyJewelId { get; set; }

        // Supports ready-made collections
        public int? CollectionId { get; set; }

        public bool IsCustomBuild { get; set; } = false;

        public string? RecipeJson { get; set; }

        public string? CustomDesignName { get; set; }
    }
}