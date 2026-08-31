using Microsoft.EntityFrameworkCore;
using OneJevelsCompany.Web.Data;
using OneJevelsCompany.Web.Models;

namespace OneJevelsCompany.Web.Services.Orders
{
    public class OrderService : IOrderService
    {
        private readonly AppDbContext _db;
        public OrderService(AppDbContext db) { _db = db; }

        public async Task<Order> CreateOrderAsync(string? email, string? address, IEnumerable<CartItem> items)
        {
            var order = new Order
            {
                CustomerEmail = email,
                ShippingAddress = address,
                Status = "Pending",
                CreatedUtc = DateTime.UtcNow
            };

            foreach (var i in items)
            {
                order.Items.Add(new OrderItem
                {
                    Title = i.Title,
                    Category = i.Category,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    ComponentsSummary = i.ComponentsSummary,
                    ComponentIdsCsv = i.ComponentIdsCsv,
                    ReadyJewelId = i.ReadyJewelId,
                    CollectionId = i.CollectionId,
                    IsCustomBuild = i.IsCustomBuild,
                    RecipeJson = i.RecipeJson,
                    CustomDesignName = i.CustomDesignName
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

        public Task<Order?> GetAsync(int orderId) =>
            _db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == orderId);
    }
}
