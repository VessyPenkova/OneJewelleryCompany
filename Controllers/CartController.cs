using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OneJevelsCompany.Core.Entities;
using OneJevelsCompany.Core.Enums;
using OneJevelsCompany.Infrastructure.Persistence;
using OneJevelsCompany.Web.Services.Cart;

namespace OneJevelsCompany.Web.Controllers
{
    public class CartController : Controller
    {
        private readonly ICartService _cart;
        private readonly AppDbContext _db;

        public CartController(ICartService cart, AppDbContext db)
        {
            _cart = cart;
            _db = db;
        }

        // ===== Cart screen =====
        public IActionResult Cart()
        {
            var items = _cart.GetCart(HttpContext);
            ViewBag.Total = items.Sum(i => i.LineTotal);

            return View(items);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Update(string sku, int qty)
        {
            _cart.UpdateQuantity(HttpContext, sku, qty);

            return RedirectToAction(nameof(Cart));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Remove(string sku)
        {
            _cart.Remove(HttpContext, sku);

            return RedirectToAction(nameof(Cart));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Clear()
        {
            _cart.Clear(HttpContext);

            return RedirectToAction(nameof(Cart));
        }

        // ===== Add a custom-built piece with per-component quantities =====
        // Expected form fields:
        // Category
        // Quantity
        // LaborPerPiece
        // DesignName
        // Components[ID].Quantity
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public async Task<IActionResult> AddCustomRecipe( JewelCategory Category, int Quantity, decimal LaborPerPiece, string? DesignName)
        {
            var picks = new List<(int id, int qty)>();

            foreach (var key in Request.Form.Keys)
            {
                if (!key.StartsWith("Components[") ||
                    !key.EndsWith("].Quantity"))
                {
                    continue;
                }

                var i1 = key.IndexOf('[') + 1;
                var i2 = key.IndexOf(']', i1);

                if (i1 <= 0 || i2 <= i1)
                {
                    continue;
                }

                if (!int.TryParse( key.Substring(i1, i2 - i1), out var compId))
                {
                    continue;
                }

                if (!int.TryParse( Request.Form[key], out var qty))
                {
                    qty = 0;
                }

                if (qty > 0)
                {
                    picks.Add((compId, qty));
                }
            }

            if (Quantity < 1 || picks.Count == 0)
            {
                TempData["Err"] = "Enter at least one component quantity and a valid finished quantity.";

                return RedirectToAction("Build", "Shop");
            }

            var ids = picks.Select(p => p.id).ToList();
            var comps = await _db.Components.Where(c => ids.Contains(c.Id)).ToListAsync();

            if (comps.Count != ids.Distinct().Count())
            {
                TempData["Err"] ="One or more selected components no longer exist.";

                return RedirectToAction("Build", "Shop");
            }

            decimal materials = 0m;

            foreach (var pick in picks)
            {
                var c = comps.First(x => x.Id == pick.id);

                materials += c.Price * pick.qty;
            }

            if (LaborPerPiece < 0)
            {
                LaborPerPiece = 0;
            }

            var unitPrice = materials + LaborPerPiece;
            var compsById = comps.ToDictionary(c => c.Id, c => c);
            var summary = string.Join( ", ", picks.Select( p => $"{p.qty}× {compsById[p.id].Name}"));
            var csv = string.Join( ",", picks.SelectMany( p => Enumerable.Repeat(p.id, p.qty)));

            var item = new CartItem
            {
                Sku = $"CUST-{Guid.NewGuid():N}".Substring(0, 12),
                Title = $"{(string.IsNullOrWhiteSpace(DesignName) ? "Custom" : DesignName.Trim())} ({Category})",
                Category = Category,
                Quantity = Quantity,
                UnitPrice = unitPrice,
                ComponentsSummary = summary,
                ComponentIdsCsv = csv,
                IsCustomBuild = true,
                CustomDesignName = string.IsNullOrWhiteSpace(DesignName) ? "Custom" : DesignName.Trim()
            };

            _cart.AddToCart(HttpContext, item);
            return RedirectToAction(nameof(Cart));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public async Task<IActionResult> AddComponents()
        {
            var picks = new List<(int id, int qty)>();

            foreach (var key in Request.Form.Keys)
            {
                if (!key.StartsWith("Components[") ||
                    !key.EndsWith("].Quantity"))
                {
                    continue;
                }

                var i1 = key.IndexOf('[') + 1;
                var i2 = key.IndexOf(']', i1);

                if (i1 <= 0 || i2 <= i1)
                {
                    continue;
                }

                if (!int.TryParse(key.Substring(i1, i2 - i1), out var compId))
                {
                    continue;
                }

                if (!int.TryParse(Request.Form[key], out var qty))
                {
                    qty = 0;
                }

                if (qty > 0)
                {
                    picks.Add((compId, qty));
                }
            }

            if (picks.Count == 0)
            {
                TempData["Err"] = "Please choose at least one component.";

                return RedirectToAction("Components", "Shop");
            }

            var ids = picks.Select(p => p.id).Distinct().ToList();

            var comps = await _db.Components
                .Where(c => ids.Contains(c.Id))
                .ToListAsync();

            if (comps.Count != ids.Count)
            {
                TempData["Err"] = "One or more selected components no longer exist.";

                return RedirectToAction("Components", "Shop");
            }

            foreach (var pick in picks)
            {
                var component = comps.First(c => c.Id == pick.id);

                var item = new CartItem
                {
                    Sku = $"COMP-{component.Id}",
                    Title = component.Name,
                    Quantity = pick.qty,
                    UnitPrice = component.Price,
                    ComponentsSummary = component.Name,
                    ComponentIdsCsv = component.Id.ToString(),
                    IsCustomBuild = false
                };

                _cart.AddToCart(HttpContext, item);
            }

            return RedirectToAction(nameof(Cart));
        }
    }
}