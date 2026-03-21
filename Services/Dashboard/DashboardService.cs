using Microsoft.EntityFrameworkCore;
using OneJevelsCompany.Web.Data;
using OneJevelsCompany.Web.Models;
using OneJevelsCompany.Web.Models.Admin;

namespace OneJevelsCompany.Web.Services.Dashboard
{
    public class DashboardService : IDashboardService
    {
        private readonly AppDbContext db;

        public DashboardService(AppDbContext db)
        {
            this.db = db;
        }

        public async Task<DashboardVm> GetAsync()
        {
            var today = DateTime.UtcNow.Date;
            var since30Days = today.AddDays(-30);
            var trendStart = today.AddDays(-7 * 11);

            var orders = await db.Orders
                .AsNoTracking()
                .Include(o => o.Items)
                .ToListAsync();

            var orderItems = await db.OrderItems
                .AsNoTracking()
                .ToListAsync();

            var designOrders = await db.DesignOrders
                .AsNoTracking()
                .ToListAsync();

            var paidOrders = orders
                .Where(o => o.Status == "Paid")
                .ToList();

            var totalSales = paidOrders.Sum(o => o.Total);

            var todaysSales = paidOrders
                .Where(o => o.CreatedUtc.Date == today)
                .Sum(o => o.Total);

            var notInitiated = orders
                .Where(o => o.Status != "Paid")
                .Sum(o => o.Total);

            var delayedVal = designOrders
                .Where(d => d.Status == "Delayed")
                .Sum(d => (d.UnitPriceEstimate ?? 0m) * Math.Max(1, d.Quantity));

            var onHoldVal = designOrders
                .Where(d => d.Status == "OnHold")
                .Sum(d => (d.UnitPriceEstimate ?? 0m) * Math.Max(1, d.Quantity));

            var recentPaidOrders = paidOrders
                .Where(o => o.CreatedUtc >= since30Days)
                .ToList();

            var recentPaidOrderIds = recentPaidOrders
                .Select(o => o.Id)
                .ToHashSet();

            var grouped = orderItems
                .Where(oi => recentPaidOrderIds.Contains(oi.OrderId))
                .GroupBy(oi => oi.Category)
                .Select(g => new
                {
                    Category = g.Key,
                    Actual = g.Sum(x => x.UnitPrice * x.Quantity)
                })
                .ToList();

            var forecast = new Dictionary<JewelCategory, decimal>
            {
                { JewelCategory.Bracelet, 12000m },
                { JewelCategory.Necklace, 18000m }
            };

            var variances = grouped
                .Select(g => new SalesVarianceVm
                {
                    Category = g.Category.ToString(),
                    Actual = g.Actual,
                    Forecast = forecast.TryGetValue(g.Category, out var f) ? f : 10000m
                })
                .OrderBy(v => v.Category)
                .ToList();

            var topCustomerGroup = orders
                .GroupBy(o => new { o.CustomerEmail, o.ShippingAddress })
                .Select(g => new
                {
                    g.Key.CustomerEmail,
                    g.Key.ShippingAddress,
                    Last = g.Max(x => x.CreatedUtc),
                    Ltv = g.Sum(x => x.Total)
                })
                .OrderByDescending(x => x.Ltv)
                .FirstOrDefault();

            var customer = topCustomerGroup == null
                ? null
                : new CustomerVm
                {
                    Email = topCustomerGroup.CustomerEmail ?? "(guest)",
                    ShippingAddress = topCustomerGroup.ShippingAddress,
                    LastOrderOn = topCustomerGroup.Last,
                    LifetimeValue = topCustomerGroup.Ltv
                };

            var trendOrders = paidOrders
                .Where(o => o.CreatedUtc >= trendStart)
                .ToList();

            var groupedTrend = trendOrders
                .GroupBy(o => (int)((o.CreatedUtc - trendStart).TotalDays / 7))
                .Select(g => new
                {
                    Week = g.Key,
                    Total = g.Sum(x => x.Total)
                })
                .ToList();

            var labels = Enumerable.Range(0, 12)
                .Select(i => trendStart.AddDays(7 * i).ToString("MMM d"))
                .ToList();

            var values = Enumerable.Range(0, 12)
                .Select(i => groupedTrend.FirstOrDefault(t => t.Week == i)?.Total ?? 0m)
                .ToList();

            var milestones = designOrders
                .OrderByDescending(d => d.CreatedUtc)
                .Take(5)
                .Select(r => new MilestoneVm
                {
                    Title = $"{(string.IsNullOrWhiteSpace(r.DesignName) ? "Design" : r.DesignName)} — {r.Status}",
                    Tag = r.Status,
                    When = r.CreatedUtc
                })
                .ToList();

            var itemOrders = orders
                .Where(o => string.Equals(o.OrderType, "Item", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(o => o.CreatedUtc)
                .ToList();

            var jewelryOrders = orders
                .Where(o =>
                    string.Equals(o.OrderType, "Jewelry", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(o.OrderType, "Mixed", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(o => o.CreatedUtc)
                .ToList();

            var recentItemOrders = itemOrders
                .Take(5)
                .Select(o => new DashboardOrderRowVm
                {
                    Id = o.Id,
                    Type = o.OrderType,
                    CustomerEmail = o.CustomerEmail ?? "(guest)",
                    Total = o.Total,
                    Status = o.Status,
                    CreatedUtc = o.CreatedUtc,
                    Title = o.Items.Any()
                        ? string.Join(", ", o.Items.Select(i => i.Title).Take(2))
                        : $"Order #{o.Id}"
                })
                .ToList();

            var recentJewelryOrders = jewelryOrders
                .Take(5)
                .Select(o => new DashboardOrderRowVm
                {
                    Id = o.Id,
                    Type = o.OrderType,
                    CustomerEmail = o.CustomerEmail ?? "(guest)",
                    Total = o.Total,
                    Status = o.Status,
                    CreatedUtc = o.CreatedUtc,
                    Title = o.Items.Any()
                        ? string.Join(", ", o.Items.Select(i => i.Title).Take(2))
                        : $"Order #{o.Id}"
                })
                .ToList();

            var recentDesignOrders = designOrders
                .OrderByDescending(d => d.CreatedUtc)
                .Take(5)
                .Select(d => new DashboardOrderRowVm
                {
                    Id = d.Id,
                    Type = "Design",
                    CustomerEmail = d.CustomerEmail ?? "(guest)",
                    Total = (d.UnitPriceEstimate ?? 0m) * Math.Max(1, d.Quantity),
                    Status = d.Status ?? "Pending",
                    CreatedUtc = d.CreatedUtc,
                    Title = string.IsNullOrWhiteSpace(d.DesignName)
                        ? $"Design #{d.Id}"
                        : d.DesignName
                })
                .ToList();

            return new DashboardVm
            {
                TotalSales = totalSales,
                TodaysSales = todaysSales,
                NotInitiatedValue = notInitiated,
                DelayedJobsValue = delayedVal,
                JobsOnHoldValue = onHoldVal,
                ItemOrdersCount = itemOrders.Count,
                JewelryOrdersCount = jewelryOrders.Count,
                DesignOrdersCount = designOrders.Count,
                RecentItemOrders = recentItemOrders,
                RecentJewelryOrders = recentJewelryOrders,
                RecentDesignOrders = recentDesignOrders,
                Variances = variances,
                Customer = customer,
                TrendLabels = labels,
                TrendSales = values,
                Milestones = milestones
            };
        }
    }
}