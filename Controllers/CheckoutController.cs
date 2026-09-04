using Microsoft.AspNetCore.Mvc;
using OneJevelsCompany.Core.Interfaces;
using OneJevelsCompany.Infrastructure.Persistence;
using OneJevelsCompany.Web.Services.Cart;

namespace OneJevelsCompany.Web.Controllers
{
    public class CheckoutController : Controller
    {
        private readonly ICartService _cart;
        private readonly IOrderService _orders;
        private readonly IPaymentService _payments;
        private readonly IInventoryService _inventory;
        private readonly AppDbContext _db;

        public CheckoutController(
            ICartService cart,
            IOrderService orders,
            IPaymentService payments,
            IInventoryService inventory,
            AppDbContext db)
        {
            _cart = cart;
            _orders = orders;
            _payments = payments;
            _inventory = inventory;
            _db = db;
        }

        // GET /Checkout
        [HttpGet]
        public IActionResult Index()
        {
            var items = _cart.GetCart(HttpContext);
            if (!items.Any())
                return RedirectToAction("Cart", "Cart");

            ViewBag.Total = items.Sum(i => i.LineTotal);
            return View();
        }

        // POST /Checkout/CreateOrder
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateOrder(string? email, string? address)
        {
            var items = _cart.GetCart(HttpContext);
            if (!items.Any())
                return RedirectToAction("Cart", "Cart");

            // 1) Validate inventory before placing the order
            var inStock = await _inventory.ValidateCartAsync(items);
            if (!inStock)
            {
                TempData["Error"] = "Some items are out of stock. Please adjust your cart.";
                return RedirectToAction("Cart", "Cart");
            }

            // Keep order creation, paid state and inventory deduction atomic.
            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var order = await _orders.CreateOrderAsync(email, address, items);
                var intent = await _payments.CreateOrUpdatePaymentIntentAsync(order.Id, order.Total);

                // Development payment service is simulated. Replace this with provider confirmation/webhook in production.
                await _orders.MarkPaidAsync(order.Id, intent.Id);

                var savedOrder = await _orders.GetAsync(order.Id)
                    ?? throw new InvalidOperationException("The newly-created order could not be reloaded.");
                await _inventory.DecrementOnPaidOrderAsync(savedOrder);

                await tx.CommitAsync();
                _cart.Clear(HttpContext);
                return RedirectToAction(nameof(Success), new { id = order.Id });
            }
            catch (InvalidOperationException ex)
            {
                await tx.RollbackAsync();
                TempData["Error"] = ex.Message;
                return RedirectToAction("Cart", "Cart");
            }
        }

        // GET /Checkout/Success/{id}
        [HttpGet]
        public async Task<IActionResult> Success(int id)
        {
            var order = await _orders.GetAsync(id);
            if (order is null)
                return NotFound();

            return View(order);
        }
    }
}
