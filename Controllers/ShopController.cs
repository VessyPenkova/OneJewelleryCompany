using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;                 // <-- needed for queries
using OneJevelsCompany.Web.Data;                    // <-- added earlier
using OneJevelsCompany.Web.Models;
using OneJevelsCompany.Web.Models.Manufacturing;    // ok even if unused
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
            _products = products;
            _cart = cart;
            _db = db;
        }

        // ===========================
        //  Collections & Build (yours)
        // ===========================

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Collections(JewelCategory? category)
        {
            var items = await _products.GetReadyCollectionsAsync(category);
            return View(items);
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Build(JewelCategory category = JewelCategory.Necklace)
        {
            var comps = await _products.GetComponentsAsync(type: null, forCategory: category);
            ViewBag.Category = category;
            return View(comps);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
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

            return RedirectToAction("Cart", "Cart");
        }

        // ===========================
        //  Per-component flow
        // ===========================

        [HttpGet]
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

        [HttpGet]
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public async Task<IActionResult> Configure(ConfigureComponentVm form)
        {
            if (!ModelState.IsValid)
                return View(form);

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

            return RedirectToAction("Cart", "Cart");
        }

        // ============================================
        //  Design Studio (sequence-based designs)
        // ============================================

        [HttpGet]
        [AllowAnonymous]
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

        public class DesignSegment
        {
            public int ComponentId { get; set; }
            public int Count { get; set; }
        }
        public class DesignPostVm
        {
            public string DesignName { get; set; } = "Custom Bracelet";
            public int Repeat { get; set; } = 1;
            public string SegmentsJson { get; set; } = "[]";
            public decimal LaborPerPiece { get; set; } = 10m;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public async Task<IActionResult> Design(DesignPostVm form)
        {
            // kept for compatibility
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
                return RedirectToAction(nameof(Design));
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
            if (totals.Keys.Any(id => !priceById.ContainsKey(id)))
            {
                TempData["err"] = "One or more selected components no longer exist.";
                return RedirectToAction(nameof(Design));
            }

            decimal materials = 0m;
            foreach (var kv in totals)
                if (priceById.TryGetValue(kv.Key, out var price))
                    materials += price * kv.Value;

            var unitPrice = materials + Math.Max(0, form.LaborPerPiece);

            string summary = string.Join(" · ",
                segments.Select(s =>
                {
                    var nm = nameById.TryGetValue(s.ComponentId, out var n) ? n : $"#{s.ComponentId}";
                    return $"{s.Count}× {nm}";
                }));
            if (form.Repeat > 1) summary += $" × repeat {form.Repeat}";

            var flatIds = new List<int>();
            for (int r = 0; r < form.Repeat; r++)
                foreach (var s in segments)
                    for (int i = 0; i < s.Count; i++)
                        flatIds.Add(s.ComponentId);

            var sku = $"DESIGN-{Guid.NewGuid():N}".ToUpperInvariant();
            _cart.AddToCart(HttpContext, new CartItem
            {
                Sku = sku,
                Title = string.IsNullOrWhiteSpace(form.DesignName) ? "Custom Bracelet" : form.DesignName.Trim(),
                Category = JewelCategory.Bracelet,
                UnitPrice = unitPrice,
                Quantity = 1,
                ComponentsSummary = summary,
                ComponentIdsCsv = string.Join(",", flatIds),
                IsCustomBuild = true,
                CustomDesignName = string.IsNullOrWhiteSpace(form.DesignName) ? "Custom Bracelet" : form.DesignName.Trim()
            });

            TempData["ok"] = "Your custom design was added to the cart.";
            return RedirectToAction("Cart", "Cart");
        }

        // ===== JSON submit used by the Design Studio “Send to Admin” =====

        public class SubmitDesignRow
        {
            public int ComponentId { get; set; }
            public int Count { get; set; }
            public int Mm { get; set; }
            public string? ImageUrl { get; set; }
            public string? Name { get; set; }
        }

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

            public string? PreviewDataUrl { get; set; }        // <— screenshot
            public List<SubmitDesignRow> Rows { get; set; } = new();
        }

        private static int EstimateCapacity(decimal lengthCm, int beadMm)
        {
            var usableMm = Math.Max(1m, lengthCm * 10m);
            var spacing = 1.05m;
            return Math.Max(1, (int)Math.Floor(usableMm / (beadMm * spacing)));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public async Task<IActionResult> SubmitDesign([FromBody] SubmitDesignVm vm)
        {
            if (vm == null || vm.Rows == null || vm.Rows.Count == 0)
                return BadRequest("No rows.");

            var cat = Enum.TryParse<JewelCategory>(vm.Category, true, out var parsed)
                ? parsed : JewelCategory.Bracelet;

            var length = vm.LengthCm ?? 18m;
            var beadMm = vm.BeadMm ?? 8;
            var oneCycle = vm.Rows.Sum(r => Math.Max(0, r.Count));
            if (oneCycle == 0) return BadRequest("Empty pattern.");

            var capacity = EstimateCapacity(length, beadMm);

            var requestedIds = vm.Rows.Where(r => r.Count > 0).Select(r => r.ComponentId).Distinct().ToList();
            var components = await _db.Components.Where(c => requestedIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id);
            if (components.Count != requestedIds.Count)
                return BadRequest("One or more selected components no longer exist.");

            var serverEstimate = vm.Rows
                .Where(r => r.Count > 0)
                .Sum(r => components[r.ComponentId].Price * r.Count);

            var patternJson = JsonSerializer.Serialize(vm.Rows);

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

                UnitPriceEstimate = serverEstimate,
                CustomerName = vm.CustomerName,
                CustomerEmail = vm.CustomerEmail,
                CustomerPhone = vm.CustomerPhone,

                // NEW: save screenshot + preview-bead count
                PreviewDataUrl = vm.PreviewDataUrl,
                PreviewBeads = Math.Min(capacity, oneCycle) // rough, UI shows the exact number
            };

            _db.DesignOrders.Add(order);
            await _db.SaveChangesAsync();

            return Json(new
            {
                id = order.Id,
                redirect = Url.Action(nameof(DesignSubmitted), "Shop", new { id = order.Id })
            });
        }

        // Thank-you page
        [HttpGet]
        [AllowAnonymous]
        public IActionResult DesignSubmitted(int id)
            => View("~/Views/Shop/DesignSubmitted.cshtml", id);

        // ===========================
        //  NEW: Designs gallery
        // ===========================
        public class DesignGalleryItem
        {
            public string Title { get; set; } = "";
            public string? ImageUrl { get; set; }    // for built-in designs
            public string? DataUrl { get; set; }     // for custom (data URL)
            public string Category { get; set; } = "Bracelet";
            public int Rating { get; set; }          // popularity
            public string? LinkUrl { get; set; }     // optional details link
            public bool IsCustom { get; set; }
        }

        [HttpGet("/Designs")]
        [AllowAnonymous]
        public async Task<IActionResult> Designs()
        {
            // built-in "Designs" table if you use it
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
                })
                .ToListAsync();

            // group custom orders by exact pattern JSON to compute popularity
            var customs = await _db.DesignOrders
                .Where(o => o.PreviewDataUrl != null && o.PreviewDataUrl != "")
                .GroupBy(o => o.PatternJson)
                .Select(g => new
                {
                    Count = g.Count(),
                    Last = g.OrderByDescending(x => x.CreatedUtc).FirstOrDefault()
                })
                .ToListAsync();

            var customCards = customs
                .Where(x => x.Last != null)
                .Select(x => new DesignGalleryItem
                {
                    Title = $"Custom design #{x.Last!.Id}",
                    DataUrl = x.Last!.PreviewDataUrl,
                    Category = x.Last!.Category.ToString(),
                    Rating = x.Count,
                    LinkUrl = Url.Action(nameof(DesignSubmitted), "Shop", new { id = x.Last!.Id }),
                    IsCustom = true
                })
                .ToList();

            // merge and show highest rating first, then newest
            var items = builtin
                .Concat(customCards)
                .OrderByDescending(i => i.Rating)
                .ThenByDescending(i => i.IsCustom) // custom usually newer
                .ToList();

            return View("~/Views/Shop/Designs.cshtml", items);
        }

        // ======== Small VMs used by Details/Configure ========
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

            [Range(1, 9999)]
            public int Quantity { get; set; } = 1;

            public int MaxQty { get; set; } = 1;
        }
    }
}
