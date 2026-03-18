using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore; // ok even if unused
using OneJevelsCompany.Web.Data;     // ok even if unused
using OneJevelsCompany.Web.Models;
using OneJevelsCompany.Web.Models.Manufacturing;
using OneJevelsCompany.Web.Routing;
using OneJevelsCompany.Web.Services.Cart;
using OneJevelsCompany.Web.Services.Product;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace OneJevelsCompany.Web.Controllers
{
    public class ShopController : Controller
    {
        private readonly IProductService _products;
        private readonly ICartService _cart;
        private readonly AppDbContext _db;

        public ShopController(IProductService products, ICartService cart, AppDbContext db)
        {
            _products = products; _cart = cart; _db = db;
        }

        // ========== ANONYMOUS: Browse & Buy Ready-Made Jewelry ==========
        [HttpGet("/Collections", Name = RouteNames.Shop.Collections)]
        [AllowAnonymous]
        public async Task<IActionResult> Collections(JewelCategory? category)
        {
            var items = await _products.GetReadyCollectionsAsync(category);
            return View(items);
        }

        [HttpPost("/Shop/AddReady", Name = RouteNames.Shop.AddReady)]
        [ValidateAntiForgeryToken, AllowAnonymous]
        public async Task<IActionResult> AddReady(int id, int qty = 1)
        {
            var ready = await _products.GetJewelAsync(id);
            if (ready is null) return NotFound();

            var price = ready.TotalPrice();
            var sku = $"READY-{ready.Id}";

            _cart.AddToCart(HttpContext, new CartItem
            {
                Sku = sku,
                Title = ready.Name,
                Category = ready.Category,
                UnitPrice = price,
                Quantity = Math.Max(1, qty),
                ComponentsSummary = string.Join(", ", ready.Components.Select(c => c.Component.Name)),
                ReadyJewelId = ready.Id
            });

            return RedirectToRoute(RouteNames.Cart.View);
        }

        [HttpGet("/Shop/Details/{id:int}", Name = RouteNames.Shop.Details)]
        [AllowAnonymous]
        public async Task<IActionResult> Details(int id)
        {
            var c = await _products.GetComponentAsync(id);
            if (c is null) return NotFound();

            var vm = new ComponentDetailsVm
            {
                Id = c.Id,
                Name = c.Name,
                ImageUrl = c.ImageUrl,
                Color = c.Color,
                SizeLabel = c.SizeLabel,
                Dimensions = c.Dimensions,
                Description = c.Description
            };

            return View(vm);
        }

        [HttpGet("/Shop/Components")]
        [Authorize]
        public async Task<IActionResult> Components()
        {
            var comps = await _products.GetComponentsAsync();
            return View("Build", comps);
        }

        [HttpGet("/Shop/Configure/{id:int}", Name = RouteNames.Shop.ConfigureGet)]
        [AllowAnonymous]
        public async Task<IActionResult> Configure(int id)
        {
            var c = await _products.GetComponentAsync(id);
            if (c is null) return NotFound();

            var opts = (c.Dimensions ?? string.Empty)
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .DefaultIfEmpty("Стандарт")
                .ToList();

            var vm = new ConfigureComponentVm
            {
                Id = c.Id,
                Name = c.Name,
                ImageUrl = c.ImageUrl,
                Price = c.Price,
                DimensionOptions = opts,
                Quantity = 1,
                MaxQty = Math.Max(1, c.QuantityOnHand)
            };

            return View(vm);
        }

        [HttpPost("/Shop/Configure", Name = RouteNames.Shop.ConfigurePost)]
        [ValidateAntiForgeryToken, AllowAnonymous]
        public async Task<IActionResult> Configure(ConfigureComponentVm form)
        {
            if (!ModelState.IsValid) return View(form);

            var c = await _products.GetComponentAsync(form.Id);
            if (c is null) return NotFound();

            var max = Math.Max(0, c.QuantityOnHand);
            if (max == 0)
            {
                ModelState.AddModelError("", "This component is out of stock.");
                form.MaxQty = 0;
                return View(form);
            }
            if (form.Quantity < 1) form.Quantity = 1;
            if (form.Quantity > max) form.Quantity = max;

            var dim = string.IsNullOrWhiteSpace(form.SelectedDimension) ? "Std" : form.SelectedDimension.Trim();
            var sku = $"COMP-{c.Id}-{dim}".ToUpperInvariant();

            _cart.AddToCart(HttpContext, new CartItem
            {
                Sku = sku,
                Title = $"{c.Name} ({dim})",
                Category = JewelCategory.Necklace,
                UnitPrice = c.Price,
                Quantity = form.Quantity,
                ComponentsSummary = $"Dimension: {dim}",
                ComponentIdsCsv = c.Id.ToString()
            });

            return RedirectToRoute(RouteNames.Cart.View);
        }

        [HttpGet("/Designs", Name = RouteNames.Shop.DesignsGallery)]
        [AllowAnonymous]
        public async Task<IActionResult> Designs()
        {
            var builtin = await _db.Designs
                .OrderBy(d => d.Name)
                .Select(d => new DesignGalleryItem
                {
                    Title = d.Name,
                    ImageUrl = d.ImageUrl,
                    Category = d.Category.ToString(),
                    Rating = 0,
                    LinkUrl = null,
                    IsCustom = false
                }).ToListAsync();

            var customs = await _db.DesignOrders
                .Where(o => o.PreviewDataUrl != null && o.PreviewDataUrl != "")
                .GroupBy(o => o.PatternJson)
                .Select(g => new { Count = g.Count(), Last = g.OrderByDescending(x => x.CreatedUtc).FirstOrDefault() })
                .ToListAsync();

            var customCards = customs.Where(x => x.Last != null).Select(x => new DesignGalleryItem
            {
                Title = $"Custom design #{x.Last!.Id}",
                DataUrl = x.Last!.PreviewDataUrl,
                Category = x.Last!.Category.ToString(),
                Rating = x.Count,
                LinkUrl = Url.RouteUrl(RouteNames.Shop.DesignSubmitted, new { id = x.Last!.Id }),
                IsCustom = true
            }).ToList();

            var items = builtin.Concat(customCards)
                .OrderByDescending(i => i.Rating)
                .ThenByDescending(i => i.IsCustom)
                .ToList();

            return View("~/Views/Shop/Designs.cshtml", items);
        }

        // ========== 🔒 AUTHENTICATED: Custom Design Creation ==========

        [HttpGet("/Build", Name = RouteNames.Shop.BuildGet)]
        [Authorize] // 🔒 Requires login
        public async Task<IActionResult> Build(JewelCategory category = JewelCategory.Necklace)
        {
            var comps = await _products.GetComponentsAsync(type: null, forCategory: category);
            ViewBag.Category = category;
            return View(comps);
        }

        [HttpPost("/Build", Name = RouteNames.Shop.BuildPost)]
        [Authorize] // 🔒 Requires login
        [Obsolete("Use CartController.AddCustomRecipe (form POST) for quantity-based custom builds.")]
        public async Task<IActionResult> Build([FromBody] BuildRequest req)
        {
            if (req is null || req.ComponentIds == null || req.ComponentIds.Count == 0)
                return BadRequest("No components selected.");

            var price = await _products.CalculateCustomPriceAsync(req.ComponentIds);
            var summary = await _products.DescribeComponentsAsync(req.ComponentIds);
            var sku = $"CUSTOM-{req.Category}-{Guid.NewGuid():N}".ToUpperInvariant();

            _cart.AddToCart(HttpContext, new CartItem
            {
                Sku = sku,
                Title = $"Custom {req.Category}",
                Category = req.Category,
                UnitPrice = price,
                Quantity = 1,
                ComponentsSummary = summary,
                ComponentIdsCsv = string.Join(",", req.ComponentIds)
            });

            return Ok(new { sku, total = price, summary });
        }

        [HttpGet("/Shop/Design", Name = RouteNames.Shop.DesignGet)]
        [Authorize] // 🔒 Requires login
        public async Task<IActionResult> Design()
        {
            var all = await _products.GetComponentsAsync();
            var withImages = all
                .Where(c => !string.IsNullOrWhiteSpace(c.ImageUrl))
                .OrderBy(c => c.Category?.SortOrder ?? 999)
                .ThenBy(c => c.Name)
                .ToList();

            return View(withImages);
        }

        public class DesignSegment { public int ComponentId { get; set; } public int Count { get; set; } }
        public class DesignPostVm
        {
            public string DesignName { get; set; } = "Custom Bracelet";
            public int Repeat { get; set; } = 1;
            public string SegmentsJson { get; set; } = "[]";
            public decimal LaborPerPiece { get; set; } = 10m;
        }

        [HttpPost("/Shop/Design", Name = RouteNames.Shop.DesignPost)]
        [Authorize] // 🔒 Requires login
        public async Task<IActionResult> Design(DesignPostVm form)
        {
            List<DesignSegment> segments = new();
            using (var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(form.SegmentsJson) ? "[]" : form.SegmentsJson))
            {
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    var id = el.GetProperty("componentId").GetInt32();
                    var cnt = Math.Max(0, el.GetProperty("count").GetInt32());
                    if (cnt > 0) segments.Add(new DesignSegment { ComponentId = id, Count = cnt });
                }
            }
            if (segments.Count == 0 || form.Repeat < 1)
            {
                TempData["err"] = "Please add at least one bead to the sequence.";
                return RedirectToRoute(RouteNames.Shop.DesignGet);
            }

            var totals = new Dictionary<int, int>();
            foreach (var s in segments)
            {
                if (!totals.ContainsKey(s.ComponentId)) totals[s.ComponentId] = 0;
                totals[s.ComponentId] += s.Count * form.Repeat;
            }

            var comps = await _products.GetComponentsAsync();
            var priceById = comps.ToDictionary(c => c.Id, c => c.Price);
            var nameById = comps.ToDictionary(c => c.Id, c => c.Name);

            decimal materials = 0m;
            foreach (var kv in totals) if (priceById.TryGetValue(kv.Key, out var price)) materials += price * kv.Value;

            var unitPrice = materials + Math.Max(0, form.LaborPerPiece);

            string summary = string.Join(" · ", segments.Select(s =>
            {
                var nm = nameById.TryGetValue(s.ComponentId, out var n) ? n : $"#{s.ComponentId}";
                return $"{s.Count}× {nm}";
            }));
            if (form.Repeat > 1) summary += $" × repeat {form.Repeat}";

            var flatIds = new List<int>();
            for (int r = 0; r < form.Repeat; r++)
                foreach (var s in segments)
                    for (int i = 0; i < s.Count; i++) flatIds.Add(s.ComponentId);

            var sku = $"DESIGN-{Guid.NewGuid():N}".ToUpperInvariant();
            _cart.AddToCart(HttpContext, new CartItem
            {
                Sku = sku,
                Title = string.IsNullOrWhiteSpace(form.DesignName) ? "Custom Bracelet" : form.DesignName.Trim(),
                Category = JewelCategory.Bracelet,
                UnitPrice = unitPrice,
                Quantity = 1,
                ComponentsSummary = summary,
                ComponentIdsCsv = string.Join(",", flatIds)
            });

            TempData["ok"] = "Your custom design was added to the cart.";
            return RedirectToRoute(RouteNames.Cart.View);
        }

        public class SubmitDesignRow { public int ComponentId { get; set; } public int Count { get; set; } public int Mm { get; set; } public string? ImageUrl { get; set; } public string? Name { get; set; } }
        public class SubmitDesignVm
        {
            public string Category { get; set; } = "Bracelet";
            public int Quantity { get; set; } = 1;
            public decimal? UnitPriceEstimate { get; set; }
            public decimal? LengthCm { get; set; }
            public int? BeadMm { get; set; }
            public string? Mode { get; set; }
            public int? Tilt { get; set; }
            public int? Rotate { get; set; }
            public string? CustomerName { get; set; }
            public string? CustomerEmail { get; set; }
            public string? CustomerPhone { get; set; }
            public string? PreviewDataUrl { get; set; }
            public List<SubmitDesignRow> Rows { get; set; } = new();
        }

        private static int EstimateCapacity(decimal lengthCm, int beadMm)
        {
            var usableMm = Math.Max(1m, lengthCm * 10m);
            var spacing = 1.05m;
            return Math.Max(1, (int)Math.Floor(usableMm / (beadMm * spacing)));
        }

        [HttpPost("/Shop/SubmitDesign", Name = RouteNames.Shop.SubmitDesign)]
        [Authorize] // 🔒 Requires login
        public async Task<IActionResult> SubmitDesign([FromBody] SubmitDesignVm vm)
        {
            if (vm == null || vm.Rows == null || vm.Rows.Count == 0) return BadRequest("No rows.");

            var cat = Enum.TryParse<JewelCategory>(vm.Category, true, out var parsed) ? parsed : JewelCategory.Bracelet;

            var length = vm.LengthCm ?? 18m;
            var beadMm = vm.BeadMm ?? 8;
            var oneCycle = vm.Rows.Sum(r => Math.Max(0, r.Count));
            if (oneCycle == 0) return BadRequest("Empty pattern.");

            var capacity = EstimateCapacity(length, beadMm);
            var patternJson = JsonSerializer.Serialize(vm.Rows);

            // Capture authenticated user info
            var userEmail = User.Identity?.Name ?? vm.CustomerEmail;
            var userName = User.Identity?.Name;

            var order = new DesignOrder
            {
                Category = cat,
                Quantity = Math.Max(1, vm.Quantity),
                LengthCm = length,
                BeadMm = beadMm,
                Mode = string.IsNullOrWhiteSpace(vm.Mode) ? "circle" : vm.Mode!,
                Tilt = vm.Tilt ?? 65,
                Rotate = vm.Rotate ?? -10,
                PatternJson = patternJson,
                OneCycleBeads = oneCycle,
                CapacityEstimate = capacity,
                UnitPriceEstimate = vm.UnitPriceEstimate,
                CustomerName = vm.CustomerName ?? userName,
                CustomerEmail = userEmail,
                CustomerPhone = vm.CustomerPhone,
                PreviewDataUrl = vm.PreviewDataUrl,
                PreviewBeads = Math.Min(capacity, oneCycle)
            };

            _db.DesignOrders.Add(order);
            await _db.SaveChangesAsync();

            return Json(new
            {
                id = order.Id,
                redirect = Url.RouteUrl(RouteNames.Shop.DesignSubmitted, new { id = order.Id })
            });
        }

        [HttpGet("/Shop/DesignSubmitted/{id:int}", Name = RouteNames.Shop.DesignSubmitted)]
        [Authorize] // 🔒 Requires login to view your own designs
        public async Task<IActionResult> DesignSubmitted(int id)
        {
            var order = await _db.DesignOrders.FirstOrDefaultAsync(o => o.Id == id);
            if (order == null) return NotFound();

            // Optional: Only allow viewing own designs (comment out if not needed)
            var userEmail = User.Identity?.Name;
            if (!User.IsInRole("Admin") && order.CustomerEmail != userEmail)
            {
                return Forbid(); // Users can only see their own designs
            }

            return View("~/Views/Shop/DesignSubmitted.cshtml", id);
        }

        // ========== View Models ==========
        public class DesignGalleryItem
        {
            public string Title { get; set; } = "";
            public string? ImageUrl { get; set; }
            public string? DataUrl { get; set; }
            public string Category { get; set; } = "Bracelet";
            public int Rating { get; set; }
            public string? LinkUrl { get; set; }
            public bool IsCustom { get; set; }
        }

        public class ComponentDetailsVm
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public string? ImageUrl { get; set; }
            public string? Color { get; set; }
            public string? SizeLabel { get; set; }
            public string? Dimensions { get; set; }
            public string? Description { get; set; }
        }

        public class ConfigureComponentVm
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public string? ImageUrl { get; set; }
            public decimal Price { get; set; }
            public List<string> DimensionOptions { get; set; } = new();
            public string? SelectedDimension { get; set; }
            [Range(1, 9999)] public int Quantity { get; set; } = 1;
            public int MaxQty { get; set; } = 1;
        }
    }
}