using CoreDesignOrder = OneJevelsCompany.Core.Entities.DesignOrder;

namespace OneJevelsCompany.Web.Models.Admin
{
    public class DesignOrderDetailsViewModel
    {
        public CoreDesignOrder Order { get; set; } = null!;

        public int Repeats { get; set; }

        public int RepeatsPerPiece { get; set; }

        public List<DesignOrderComponentRowViewModel> Rows { get; set; } = new();

        public int TotalBeadsPerPiece { get; set; }

        public int TotalBeadsAll { get; set; }

        public decimal MaterialsCostPerJewel { get; set; }

        public string NewJewelName { get; set; } = string.Empty;

        public decimal? NewJewelPrice { get; set; }

        public bool CreateJewel { get; set; } = true;
    }
}