using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OneJevelsCompany.Web.Data;
using OneJevelsCompany.Web.Models;
using OneJevelsCompany.Web.Models.Admin;
using OneJevelsCompany.Web.Services.Inventory;
using System.Text.Json;

namespace OneJevelsCompany.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IInventoryService _inventory;
        private readonly IWebHostEnvironment _env;

        public AdminController(AppDbContext db, IInventoryService inventory, IWebHostEnvironment env)
        {
            _db = db;
            _inventory = inventory;
            _env = env;
        }

        public IActionResult Index() => View();

        // =====================================================================
        // ===================== Catalog lists =================================
        // =====================================================================
        public async Task<IActionResult> Components()
        {
            var items = await _db.Components
                .Include(c => c.Category)
                .OrderBy(c => c.Category!.SortOrder)
                .ThenBy(c => c.Category!.Name)
                .ThenBy(c => c.Name)
                .ToListAsync();

            return View(items);
        }

        public async Task<IActionResult> Jewels()
        {
            var items = await _db.Jewels
                .OrderBy(j => j.Name)
                .ToListAsync();

            return View(items);
        }

        public async Task<IActionResult> Invoices()
        {
            var list = await _db.Invoices
                .OrderByDescending(i => i.IssuedOnUtc)
                .Include(i => i.Lines)
                .ToListAsync();

            return View(list);
        }

        // =====================================================================
        // ========================= Create Invoice ============================
        // =====================================================================
        [HttpGet]
        public async Task<IActionResult> NewInvoice()
        {
            await FillSelectListsAsync();
            return View(new InvoiceInputModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NewInvoice(InvoiceInputModel vm)
        {
            // Keep only valid lines (must pick item depending on type)
            vm.Lines = vm.Lines
                .Where(l =>
                       (l.LineType == "Component" && l.ComponentId.HasValue) ||
                       (l.LineType == "Jewel" && l.JewelId.HasValue))
                .ToList();

            if (!vm.Lines.Any())
                ModelState.AddModelError(string.Empty, "Add at least one valid line.");

            if (!ModelState.IsValid)
            {
                await FillSelectListsAsync();
                return View(vm);
            }

            var invoice = new Invoice
            {
                Number = vm.Number,
                SupplierName = vm.SupplierName,
                IssuedOnUtc = vm.IssuedOnUtc,
                TotalCost = vm.Lines.Sum(l => l.UnitCost * l.Quantity),
                Lines = vm.Lines.Select(l => new InvoiceLine
                {
                    ComponentId = l.LineType == "Component" ? l.ComponentId : null,
                    JewelId = l.LineType == "Jewel" ? l.JewelId : null,
                    Quantity = l.Quantity,
                    UnitCost = l.UnitCost,
                    Note = l.Note
                }).ToList()
            };

            await _inventory.ApplyInvoiceAsync(invoice);
            TempData["Ok"] = $"Invoice {invoice.Number} saved. Stock updated.";
            return RedirectToAction(nameof(Invoices));
        }

        // =====================================================================
        // ========================= Helpers ===================================
        // =====================================================================
        private async Task LoadCategoriesAsync()
        {
            ViewBag.Categories = await _db.ComponentCategories
                .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
                .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name })
                .ToListAsync();
        }

        // Populate selects for invoice screen (categories + components + jewels)
        private async Task FillSelectListsAsync()
        {
            await LoadCategoriesAsync();

            // Components (pretty label)
            ViewBag.Components = await _db.Components
                .Include(c => c.Category)
                .OrderBy(c => c.Category!.SortOrder)
                .ThenBy(c => c.Category!.Name)
                .ThenBy(c => c.Name)
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = $"{c.Category!.Name} • {c.Name} (stock {c.QuantityOnHand})"
                })
                .ToListAsync();

            // Raw components (id + category id) for client-side filtering
            ViewBag.ComponentsRaw = await _db.Components
                .OrderBy(c => c.ComponentCategoryId).ThenBy(c => c.Name)
                .Select(c => new
                {
                    c.Id,
                    c.ComponentCategoryId,
                    Text = $"{c.Name} (stock {c.QuantityOnHand})"
                })
                .ToListAsync();

            // Jewels
            ViewBag.Jewels = await _db.Jewels
                .OrderBy(j => j.Name)
                .Select(j => new SelectListItem
                {
                    Value = j.Id.ToString(),
                    Text = $"{j.Name} (stock {j.QuantityOnHand})"
                })
                .ToListAsync();
        }

        private async Task<string?> SaveImageAsync(IFormFile? file, string folder)
        {
            if (file == null || file.Length == 0) return null;

            var root = Path.Combine(_env.WebRootPath, "uploads", folder);
            Directory.CreateDirectory(root);

            var name = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";
            var path = Path.Combine(root, name);

            using (var stream = System.IO.File.Create(path))
                await file.CopyToAsync(stream);

            return $"/uploads/{folder}/{name}";
        }

        // ======================================================================
        // = NEW helper: save base64 PNG data URL to disk and return short path =
        // ======================================================================
        private async Task<string?> SaveDataUrlImageAsync(string? dataUrl, string folder)
        {
            if (string.IsNullOrWhiteSpace(dataUrl)) return null;

            // If it's already a short path (e.g., /uploads/...), just pass it through.
            if (!dataUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                return dataUrl;

            // Extract base64 after the first comma
            var comma = dataUrl.IndexOf(',');
            if (comma < 0) return null;
            var base64 = dataUrl[(comma + 1)..];

            byte[] bytes;
            try
            {
                base64 = base64.Replace(' ', '+');
                bytes = Convert.FromBase64String(base64);
            }
            catch
            {
                return null;
            }

            var root = Path.Combine(_env.WebRootPath, "uploads", folder);
            Directory.CreateDirectory(root);

            var name = $"{Guid.NewGuid():N}.png";
            var path = Path.Combine(root, name);

            await System.IO.File.WriteAllBytesAsync(path, bytes);

            return $"/uploads/{folder}/{name}";
        }

        // =====================================================================
        // ===================== New / Edit Jewel ==============================
        // =====================================================================
        [HttpGet]
        public IActionResult NewJewel()
        {
            return View(new JewelEditViewModel { Category = JewelCategory.Necklace });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NewJewel(JewelEditViewModel vm, IFormFile? ImageFile)
        {
            if (!ModelState.IsValid) return View(vm);

            var imageUrl = await SaveImageAsync(ImageFile, "jewels");

            var j = new Jewel
            {
                Name = vm.Name,
                Category = vm.Category,
                BasePrice = vm.BasePrice,
                QuantityOnHand = vm.QuantityOnHand,
                ImageUrl = imageUrl
            };

            _db.Jewels.Add(j);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Jewels));
        }

        [HttpGet]
        public async Task<IActionResult> EditJewel(int id)
        {
            var j = await _db.Jewels.FindAsync(id);
            if (j == null) return NotFound();

            var vm = new JewelEditViewModel
            {
                Id = j.Id,
                Name = j.Name,
                Category = j.Category,
                BasePrice = j.BasePrice,
                QuantityOnHand = j.QuantityOnHand,
                CurrentImageUrl = j.ImageUrl
            };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditJewel(JewelEditViewModel vm, IFormFile? ImageFile)
        {
            if (!ModelState.IsValid) return View(vm);

            var j = await _db.Jewels.FindAsync(vm.Id);
            if (j == null) return NotFound();

            j.Name = vm.Name;
            j.Category = vm.Category;
            j.BasePrice = vm.BasePrice;
            j.QuantityOnHand = vm.QuantityOnHand;

            if (ImageFile != null)
                j.ImageUrl = await SaveImageAsync(ImageFile, "jewels");

            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Jewels));
        }

        // =====================================================================
        // ===================== DESIGN ORDERS LIST ============================
        // =====================================================================
        [HttpGet("/Admin/DesignOrders")]
        public async Task<IActionResult> DesignOrders()
        {
            var orders = await _db.DesignOrders
                .OrderByDescending(o => o.CreatedUtc)
                .ToListAsync();

            return View("~/Views/Admin/DesignOrders.cshtml", orders);
        }

        // =====================================================================
        // ===================== READY DESIGN ORDERS LIST ======================
        // =====================================================================
        [HttpGet("/Admin/ReadyDesignOrders")]
        public async Task<IActionResult> ReadyDesignOrders()
        {
            var orders = await _db.DesignOrders
                .Where(o => o.Status == "Built")
                .OrderByDescending(o => o.CreatedUtc)
                .ToListAsync();

            return View("~/Views/Admin/ReadyDesignOrders.cshtml", orders);
        }

        // =====================================================================
        // ============= DESIGN ORDER DETAILS + BUILD PROTOCOL==================
        // =====================================================================
        public class DesignOrderDetailsVm
        {
            public DesignOrder Order { get; set; } = null!;
            public int Repeats { get; set; }
            public int RepeatsPerPiece { get; set; }
            public List<Row> Rows { get; set; } = new();
            public int TotalBeadsPerPiece { get; set; }
            public int TotalBeadsAll { get; set; }
            public decimal MaterialsCostPerJewel { get; set; }

            public class Row
            {
                public int ComponentId { get; set; }
                public string Name { get; set; } = "";
                public string? ImageUrl { get; set; }
                public int Mm { get; set; }
                public int CountOneCycle { get; set; }
                public int PerPieceCount { get; set; }
                public int CountPerJewel { get; set; }
                public int NeededTotal { get; set; }
                public int Stock { get; set; }
                public decimal Price { get; set; }
                public decimal CostPerJewel { get; set; }
            }

            public string NewJewelName { get; set; } = "";
            public decimal? NewJewelPrice { get; set; }
            public bool CreateJewel { get; set; } = true;
        }

        private sealed class PatternRowDto
        {
            public int ComponentId { get; set; }
            public int Count { get; set; }
            public int Mm { get; set; }
            public string? ImageUrl { get; set; }
            public string? Name { get; set; }
        }

        private static int CalcRepeats(int capacityEstimate, int oneCycleBeads)
        {
            if (oneCycleBeads <= 0) return 1;
            var r = (int)Math.Round((double)capacityEstimate / (double)oneCycleBeads);
            return Math.Max(1, r);
        }

        // GET /Admin/DesignOrder/{id}
        [HttpGet("/Admin/DesignOrder/{id:int}")]
        public async Task<IActionResult> DesignOrder(int id)
        {
            var o = await _db.DesignOrders.FirstOrDefaultAsync(x => x.Id == id);
            if (o == null) return NotFound();

            // Parse pattern JSON (case-insensitive)
            List<PatternRowDto> rowsDto;
            try
            {
                rowsDto = JsonSerializer.Deserialize<List<PatternRowDto>>(
                    string.IsNullOrWhiteSpace(o.PatternJson) ? "[]" : o.PatternJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                ) ?? new List<PatternRowDto>();
            }
            catch
            {
                rowsDto = new List<PatternRowDto>();
            }

            var ids = rowsDto.Select(r => r.ComponentId).Distinct().ToList();
            var comps = await _db.Components
                .Where(c => ids.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c);

            var repeats = CalcRepeats(o.CapacityEstimate, o.OneCycleBeads);

            var vm = new DesignOrderDetailsVm
            {
                Order = o,
                Repeats = repeats,
                RepeatsPerPiece = repeats,
                NewJewelName = !string.IsNullOrWhiteSpace(o.DesignName) ? o.DesignName! : $"Custom #{o.Id}",
                NewJewelPrice = o.UnitPriceEstimate
            };

            decimal totalMaterials = 0m;
            int totalBeadsPerPiece = 0;

            foreach (var r in rowsDto)
            {
                comps.TryGetValue(r.ComponentId, out var c);

                var oneCycle = Math.Max(0, r.Count);
                var perPiece = oneCycle * Math.Max(1, repeats);
                var needed = perPiece * Math.Max(1, o.Quantity);

                var unitPrice = c?.Price ?? 0m;
                var costPerPiece = unitPrice * perPiece;

                vm.Rows.Add(new DesignOrderDetailsVm.Row
                {
                    ComponentId = r.ComponentId,
                    Name = !string.IsNullOrWhiteSpace(r.Name) ? r.Name! : (c?.Name ?? $"#{r.ComponentId}"),
                    ImageUrl = string.IsNullOrWhiteSpace(r.ImageUrl) ? c?.ImageUrl : r.ImageUrl,
                    Mm = r.Mm,
                    CountOneCycle = oneCycle,
                    PerPieceCount = perPiece,
                    CountPerJewel = perPiece,
                    NeededTotal = needed,
                    Stock = c?.QuantityOnHand ?? 0,
                    Price = unitPrice,
                    CostPerJewel = costPerPiece
                });

                totalMaterials += costPerPiece;
                totalBeadsPerPiece += perPiece;
            }

            vm.MaterialsCostPerJewel = totalMaterials;
            vm.TotalBeadsPerPiece = totalBeadsPerPiece;
            vm.TotalBeadsAll = totalBeadsPerPiece * Math.Max(1, o.Quantity);

            return View("~/Views/Admin/DesignOrderDetails.cshtml", vm);
        }

        // GET /Admin/DesignOrderProtocol/{id}
        [HttpGet("/Admin/DesignOrderProtocol/{id:int}")]
        public async Task<IActionResult> DesignOrderProtocol(int id)
        {
            var o = await _db.DesignOrders.FirstOrDefaultAsync(x => x.Id == id);
            if (o == null) return NotFound();

            List<PatternRowDto> rowsDto;
            try
            {
                rowsDto = JsonSerializer.Deserialize<List<PatternRowDto>>(
                    string.IsNullOrWhiteSpace(o.PatternJson) ? "[]" : o.PatternJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                ) ?? new List<PatternRowDto>();
            }
            catch
            {
                rowsDto = new List<PatternRowDto>();
            }

            var ids = rowsDto.Select(r => r.ComponentId).Distinct().ToList();
            var comps = await _db.Components
                .Where(c => ids.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c);

            var repeats = CalcRepeats(o.CapacityEstimate, o.OneCycleBeads);

            var vm = new DesignOrderDetailsVm
            {
                Order = o,
                Repeats = repeats,
                RepeatsPerPiece = repeats,
                NewJewelName = !string.IsNullOrWhiteSpace(o.DesignName) ? o.DesignName! : $"Custom #{o.Id}",
                NewJewelPrice = o.UnitPriceEstimate
            };

            decimal totalMaterials = 0m;
            int totalBeadsPerPiece = 0;

            foreach (var r in rowsDto)
            {
                comps.TryGetValue(r.ComponentId, out var c);

                var oneCycle = Math.Max(0, r.Count);
                var perPiece = oneCycle * Math.Max(1, repeats);
                var needed = perPiece * Math.Max(1, o.Quantity);
                var unitPrice = c?.Price ?? 0m;
                var costPerPiece = unitPrice * perPiece;

                vm.Rows.Add(new DesignOrderDetailsVm.Row
                {
                    ComponentId = r.ComponentId,
                    Name = !string.IsNullOrWhiteSpace(r.Name) ? r.Name! : (c?.Name ?? $"#{r.ComponentId}"),
                    ImageUrl = string.IsNullOrWhiteSpace(r.ImageUrl) ? c?.ImageUrl : r.ImageUrl,
                    Mm = r.Mm,
                    CountOneCycle = oneCycle,
                    PerPieceCount = perPiece,
                    CountPerJewel = perPiece,
                    NeededTotal = needed,
                    Stock = c?.QuantityOnHand ?? 0,
                    Price = unitPrice,
                    CostPerJewel = costPerPiece
                });

                totalMaterials += costPerPiece;
                totalBeadsPerPiece += perPiece;
            }

            vm.MaterialsCostPerJewel = totalMaterials;
            vm.TotalBeadsPerPiece = totalBeadsPerPiece;
            vm.TotalBeadsAll = totalBeadsPerPiece * Math.Max(1, o.Quantity);

            return View("~/Views/Admin/DesignOrderProtocol.cshtml", vm);
        }

        // POST /Admin/DesignOrder/{id}/Build
        [HttpPost("/Admin/DesignOrder/{id:int}/Build")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BuildDesignOrder(
            int id,
            [FromForm] string newJewelName,
            [FromForm] decimal? newJewelPrice,
            [FromForm] int qty,
            [FromForm] bool createJewel = true)
        {
            var o = await _db.DesignOrders.FirstOrDefaultAsync(x => x.Id == id);
            if (o == null) return NotFound();

            List<PatternRowDto> rowsDto;
            try
            {
                rowsDto = JsonSerializer.Deserialize<List<PatternRowDto>>(
                    string.IsNullOrWhiteSpace(o.PatternJson) ? "[]" : o.PatternJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                ) ?? new List<PatternRowDto>();
            }
            catch { rowsDto = new List<PatternRowDto>(); }

            var rows = rowsDto
                .Select(r => (componentId: r.ComponentId, count: Math.Max(0, r.Count)))
                .Where(t => t.componentId > 0 && t.count > 0)
                .ToList();

            var ids = rows.Select(r => r.componentId).Distinct().ToList();
            var comps = await _db.Components
                .Where(c => ids.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id);

            var repeats = CalcRepeats(o.CapacityEstimate, o.OneCycleBeads);
            var qtyToBuild = Math.Max(1, qty);

            // Check stock + enqueue shortages
            var shortages = new List<string>();
            foreach (var r in rows)
            {
                var need = r.count * repeats * qtyToBuild;
                if (comps.TryGetValue(r.componentId, out var c))
                {
                    var shortage = need - c.QuantityOnHand;
                    if (shortage > 0)
                    {
                        shortages.Add($"{c.Name}: need {need}, have {c.QuantityOnHand}");
                        var moq = Math.Max(1, c.MinOrderQty);
                        await UpsertPurchaseNeedAsync(c.Id, shortage, moq, o.Id);
                    }
                }
                else
                {
                    shortages.Add($"Component #{r.componentId} not found.");
                }
            }

            if (shortages.Any())
            {
                await _db.SaveChangesAsync();
                TempData["Err"] = "Not enough stock:\n" + string.Join("\n", shortages) +
                                  "\n\nMissing quantities were added to the Purchase Queue.";
                return RedirectToAction(nameof(DesignOrder), new { id });
            }

            // Consume stock
            foreach (var r in rows)
            {
                var need = r.count * repeats * qtyToBuild;
                comps[r.componentId].QuantityOnHand -= need;
            }

            Jewel? newJewel = null;
            if (createJewel)
            {
                var previewPath = await SaveDataUrlImageAsync(o.PreviewDataUrl, folder: "previews");

                newJewel = new Jewel
                {
                    Name = string.IsNullOrWhiteSpace(newJewelName) ? $"Custom #{o.Id}" : newJewelName.Trim(),
                    Category = o.Category,
                    BasePrice = newJewelPrice ?? (o.UnitPriceEstimate ?? 0m),
                    QuantityOnHand = qtyToBuild,
                    ImageUrl = previewPath
                };
                _db.Jewels.Add(newJewel);
                await _db.SaveChangesAsync();

                foreach (var r in rows)
                {
                    _db.JewelComponents.Add(new JewelComponent
                    {
                        JewelId = newJewel.Id,
                        ComponentId = r.componentId,
                        QuantityPerJewel = r.count * repeats
                    });
                }
            }

            o.Status = "Built";
            if (!string.IsNullOrWhiteSpace(newJewelName)) o.DesignName = newJewelName;
            if (newJewelPrice.HasValue) o.UnitPriceEstimate = newJewelPrice;

            await _db.SaveChangesAsync();

            TempData["Ok"] = createJewel
                ? $"Built successfully. Stock consumed. Jewel '{newJewel!.Name}' created."
                : "Built successfully. Stock consumed.";

            return RedirectToAction(nameof(DesignOrder), new { id });
        }

        // =====================================================================
        // =================== RESTOCK / PURCHASE QUEUE ========================
        // =====================================================================
        public class PurchaseQueueRowVm
        {
            public int PurchaseNeedId { get; set; }
            public int ComponentId { get; set; }
            public string Name { get; set; } = "";
            public string? Sku { get; set; }
            public string? Dimensions { get; set; }
            public string? SizeLabel { get; set; }
            public string? ImageUrl { get; set; }
            public int Stock { get; set; }
            public decimal Price { get; set; }
            public int NeededQty { get; set; }
            public int MinOrderQty { get; set; }
            public int SuggestedQty { get; set; }
            public DateTime LastUpdatedUtc { get; set; }
        }

        [HttpGet("/Admin/PurchaseQueue")]
        public async Task<IActionResult> PurchaseQueue()
        {
            var rows = await _db.PurchaseNeeds
                .Include(p => p.Component)
                .Where(p => p.NeededQty > 0)
                .OrderByDescending(p => p.LastUpdatedUtc)
                .ToListAsync();

            var vm = rows.Select(p =>
            {
                var c = p.Component;
                var moq = Math.Max(1, c.MinOrderQty);
                var suggested = Math.Max(moq, (int)Math.Ceiling((double)p.NeededQty / moq) * moq);

                return new PurchaseQueueRowVm
                {
                    PurchaseNeedId = p.Id,
                    ComponentId = p.ComponentId,
                    Name = c.Name,
                    Sku = c.Sku,
                    Dimensions = c.Dimensions,
                    SizeLabel = c.SizeLabel,
                    ImageUrl = c.ImageUrl,
                    Stock = c.QuantityOnHand,
                    Price = c.Price,
                    NeededQty = p.NeededQty,
                    MinOrderQty = moq,
                    SuggestedQty = suggested,
                    LastUpdatedUtc = p.LastUpdatedUtc
                };
            }).ToList();

            ViewBag.TotalItems = vm.Count;
            ViewBag.TotalSuggestedCost = vm.Sum(x => x.SuggestedQty * x.Price);

            return View("~/Views/Admin/PurchaseQueue.cshtml", vm);
        }

        private sealed class NeedSource
        {
            public int designOrderId { get; set; }
            public int qty { get; set; }
            public DateTime createdUtc { get; set; }
        }

        private async Task UpsertPurchaseNeedAsync(int componentId, int addQty, int moqSnapshot, int designOrderId)
        {
            if (addQty <= 0) return;

            var need = await _db.PurchaseNeeds.FirstOrDefaultAsync(x => x.ComponentId == componentId);
            if (need == null)
            {
                need = new PurchaseNeed
                {
                    ComponentId = componentId,
                    NeededQty = addQty,
                    MinOrderQtyUsed = moqSnapshot,
                    CreatedUtc = DateTime.UtcNow,
                    LastUpdatedUtc = DateTime.UtcNow,
                    SourcesJson = JsonSerializer.Serialize(new[]
                    {
                        new NeedSource{ designOrderId = designOrderId, qty = addQty, createdUtc = DateTime.UtcNow }
                    })
                };
                _db.PurchaseNeeds.Add(need);
            }
            else
            {
                need.NeededQty += addQty;
                need.MinOrderQtyUsed = moqSnapshot;
                need.LastUpdatedUtc = DateTime.UtcNow;

                List<NeedSource> list;
                try { list = JsonSerializer.Deserialize<List<NeedSource>>(need.SourcesJson ?? "[]") ?? new(); }
                catch { list = new(); }
                list.Add(new NeedSource { designOrderId = designOrderId, qty = addQty, createdUtc = DateTime.UtcNow });
                need.SourcesJson = JsonSerializer.Serialize(list);
            }
        }
    }
}
