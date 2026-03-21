namespace OneJevelsCompany.Web.Models.Admin
{
    public class DashboardVm
    {
        // KPIs
        public decimal TotalSales { get; set; }
        public decimal TodaysSales { get; set; }
        public decimal NotInitiatedValue { get; set; }   // Orders not Paid
        public decimal DelayedJobsValue { get; set; }    // from DesignOrders
        public decimal JobsOnHoldValue { get; set; }     // from DesignOrders

        // Order summary blocks
        public int ItemOrdersCount { get; set; }
        public int JewelryOrdersCount { get; set; }
        public int DesignOrdersCount { get; set; }

        // Recent orders for dashboard sections
        public List<DashboardOrderRowVm> RecentItemOrders { get; set; } = new();
        public List<DashboardOrderRowVm> RecentJewelryOrders { get; set; } = new();
        public List<DashboardOrderRowVm> RecentDesignOrders { get; set; } = new();

        // Variance by category (Orders -> OrderItems)
        public List<SalesVarianceVm> Variances { get; set; } = new();

        // “Customer” (grouped by CustomerEmail)
        public CustomerVm? Customer { get; set; }

        public List<MilestoneVm> Milestones { get; set; } = new();

        // Chart
        public List<string> TrendLabels { get; set; } = new();
        public List<decimal> TrendSales { get; set; } = new();
    }

    public class DashboardOrderRowVm
    {
        public int Id { get; set; }
        public string Type { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedUtc { get; set; }
        public string Title { get; set; } = string.Empty;
    }
}