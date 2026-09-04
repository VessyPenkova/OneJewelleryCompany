namespace OneJevelsCompany.Web.Models.Shop
{
    public class DesignPostVm
    {
        public string DesignName { get; set; } = "Custom Bracelet";

        public int Repeat { get; set; } = 1;

        public string SegmentsJson { get; set; } = "[]";

        public decimal LaborPerPiece { get; set; } = 10m;
    }
}