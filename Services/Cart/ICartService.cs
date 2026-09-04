using OneJevelsCompany.Core.Entities;

namespace OneJevelsCompany.Web.Services.Cart
{
    public interface ICartService
    {
        List<CartItem> GetCart(HttpContext http);
        void AddToCart(HttpContext http, CartItem item);
        void UpdateQuantity(HttpContext http, string sku, int quantity);
        void Remove(HttpContext http, string sku);
        void Clear(HttpContext http);
        decimal Total(HttpContext http);
    }
}
