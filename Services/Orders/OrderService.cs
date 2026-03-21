using Microsoft.EntityFrameworkCore;
using OneJevelsCompany.Web.Data;
using OneJevelsCompany.Web.Models;

namespace OneJevelsCompany.Web.Services.Orders
{
    public class OrderService : IOrderService
    {
        private readonly AppDbContext _db;

        public OrderService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<Order> CreateOrderAsync(string? email, string? address, IEnumerable<CartItem> items)
        {
            var cartItems = items.ToList();

            var order = new Order
            {
                CustomerEmail = email,
                ShippingAddress = address,
                Status = "Pending",
                CreatedUtc = DateTime.UtcNow,
                OrderType = ResolveOrderType(cartItems)
            };

            foreach (var i in cartItems)
            {
                order.Items.Add(new OrderItem
                {
                    Title = i.Title,
                    Category = i.Category,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    ComponentsSummary = i.ComponentsSummary,
                    ImageUrl = await ResolveImageUrlAsync(i),
                    ComponentIdsCsv = i.ComponentIdsCsv,
                    ReadyJewelId = i.ReadyJewelId,
                    CollectionId = i.CollectionId,
                    IsCustomBuild = IsCustomBuild(i),
                    CustomDesignName = GetCustomDesignName(i),
                    RecipeJson = null
                });
            }

            order.Total = order.Items.Sum(x => x.UnitPrice * x.Quantity);

            _db.Orders.Add(order);
            await _db.SaveChangesAsync();

            return order;
        }

        public async Task MarkPaidAsync(int orderId, string providerPaymentId)
        {
            var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
            if (order is null) return;

            order.Status = "Paid";
            order.PaymentProviderId = providerPaymentId;

            await _db.SaveChangesAsync();
        }

        public Task<Order?> GetAsync(int orderId)
        {
            return _db.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == orderId);
        }

        private async Task<string?> ResolveImageUrlAsync(CartItem item)
        {
            if (item.ReadyJewelId.HasValue)
            {
                return await _db.Jewels
                    .Where(j => j.Id == item.ReadyJewelId.Value)
                    .Select(j => j.ImageUrl)
                    .FirstOrDefaultAsync();
            }

            if (item.CollectionId.HasValue)
            {
                return await _db.Collections
                    .Where(c => c.Id == item.CollectionId.Value)
                    .Select(c => c.ImageUrl)
                    .FirstOrDefaultAsync();
            }

            if (!string.IsNullOrWhiteSpace(item.ComponentIdsCsv))
            {
                var firstIdText = item.ComponentIdsCsv
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .FirstOrDefault();

                if (int.TryParse(firstIdText, out var componentId))
                {
                    return await _db.Components
                        .Where(c => c.Id == componentId)
                        .Select(c => c.ImageUrl)
                        .FirstOrDefaultAsync();
                }
            }

            return null;
        }

        private static bool IsCustomBuild(CartItem item)
        {
            var sku = item.Sku?.Trim().ToUpperInvariant() ?? string.Empty;
            return sku.StartsWith("DESIGN-") || sku.StartsWith("CUST-");
        }

        private static string? GetCustomDesignName(CartItem item)
        {
            return IsCustomBuild(item) ? item.Title : null;
        }

        private static string ResolveOrderType(IEnumerable<CartItem> items)
        {
            var kinds = items
                .Select(GetCartItemKind)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (kinds.Count == 0)
                return "Jewelry";

            if (kinds.Count == 1)
                return kinds[0];

            return "Mixed";
        }

        private static string GetCartItemKind(CartItem item)
        {
            var sku = item.Sku?.Trim().ToUpperInvariant() ?? string.Empty;

            if (sku.StartsWith("COMP-"))
                return "Item";

            if (sku.StartsWith("READY-"))
                return "Jewelry";

            if (sku.StartsWith("DESIGN-") || sku.StartsWith("CUST-"))
                return "Jewelry";

            return "Jewelry";
        }
    }
}