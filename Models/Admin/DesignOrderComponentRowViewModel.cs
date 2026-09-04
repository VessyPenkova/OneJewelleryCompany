namespace OneJevelsCompany.Web.Models.Admin
{
    public class DesignOrderComponentRowViewModel
    {
        public int ComponentId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? ImageUrl { get; set; }

        public int Mm { get; set; }

        public int CountOneCycle { get; set; }

        public int PerPieceCount { get; set; }

        public int CountPerJewel { get; set; }

        public int NeededTotal { get; set; }

        public int Stock { get; set; }

        public decimal Price { get; set; }

        public decimal CostPerJewel { get; set; }
    }
}