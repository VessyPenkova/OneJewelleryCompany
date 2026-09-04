using System.Text.Json;
using OneJevelsCompany.Core.Entities;

namespace OneJevelsCompany.Web.Services.Cart
{
    public class CartService : ICartService
    {
        private const string Key = "cart";

        public List<CartItem> GetCart(HttpContext http)
        {
            var json = http.Session.GetString(Key);
            return string.IsNullOrEmpty(json)
                ? new List<CartItem>()
                : JsonSerializer.Deserialize<List<CartItem>>(json) ?? new List<CartItem>();
        }

        public void AddToCart(HttpContext http, CartItem item)
        {
            var cart = GetCart(http);
            var existing = cart.FirstOrDefault(i => i.Sku == item.Sku);
            if (existing is null) cart.Add(item);
            else existing.Quantity += item.Quantity;

            Save(http, cart);
        }

        public void UpdateQuantity(HttpContext http, string sku, int quantity)
        {
            var cart = GetCart(http);
            var existing = cart.FirstOrDefault(i => i.Sku == sku);
            if (existing != null)
            {
                existing.Quantity = Math.Max(1, quantity);
                Save(http, cart);
            }
        }

        public void Remove(HttpContext http, string sku)
        {
            var cart = GetCart(http);
            cart.RemoveAll(i => i.Sku == sku);
            Save(http, cart);
        }

        public void Clear(HttpContext http) => Save(http, new List<CartItem>());

        public decimal Total(HttpContext http) => GetCart(http).Sum(i => i.LineTotal);

        private static void Save(HttpContext http, List<CartItem> cart) =>
            http.Session.SetString(Key, JsonSerializer.Serialize(cart));
    }
}
