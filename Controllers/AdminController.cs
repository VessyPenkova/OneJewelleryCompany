using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OneJevelsCompany.Web.Data;
using OneJevelsCompany.Web.Models;
using OneJevelsCompany.Web.Models.Admin;
using OneJevelsCompany.Web.Services.Common;
using OneJevelsCompany.Web.Services.Dashboard;
using OneJevelsCompany.Web.Services.Inventory;
using System.Text.Json;

namespace OneJevelsCompany.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IInventoryService _inventory;
        private readonly IDashboardService _dashboard;
        private readonly IImageStorage _images;

        public AdminController(
            AppDbContext db,
            IInventoryService inventory,
            IDashboardService dashboard,
            IImageStorage images)
        {
            _db = db;
            _inventory = inventory;
            _dashboard = dashboard;
            _images = images;
        }

        // ====================== NESTED VMs (used by several views) ======================

        public class DesignOrderDetailsVm
        {
            public DesignOrder Order { get; set; } = null!;
            public int Repeats { get; set; }
            public int RepeatsPerPiece { get; set; }
            public List<Row> Rows { get; set; } = new();
            public int TotalBeadsPerPiece { get; set; }
            public int TotalBeadsAll { get; set; }
            public decimal MaterialsCostPerJewel { get; set; }
            public string NewJewelName { get; set; } = "";
            public decimal? NewJewelPrice { get; set; }
            public bool CreateJewel { get; set; } = true;

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
        }

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

        public IActionResult Index() => RedirectToAction(nameof(Dashboard));

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
            var items = await _db.Jewels.OrderBy(j => j.Name).ToListAsync();
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
                }).ToList()
            };

            await _inventory.ApplyInvoiceAsync(invoice);
            TempData["Ok"] = $"Invoice {invoice.Number} saved. Stock updated.";
            return RedirectToAction(nameof(Invoices));
        }

        private async Task FillSelectListsAsync()
        {
            ViewBag.Categories = await _db.ComponentCategories
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name })
                .ToListAsync();

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

            ViewBag.ComponentsRaw = await _db.Components
                .OrderBy(c => c.ComponentCategoryId)
                .ThenBy(c => c.Name)
                .Select(c => new
                {
                    c.Id,
                    c.ComponentCategoryId,
                    Text = $"{c.Name} (stock {c.QuantityOnHand})"
                })
                .ToListAsync();

            ViewBag.Jewels = await _db.Jewels
                .OrderBy(j => j.Name)
                .Select(j => new SelectListItem
                {
                    Value = j.Id.ToString(),
                    Text = $"{j.Name} (stock {j.QuantityOnHand})"
                })
                .ToListAsync();
        }

        [HttpGet]
        public IActionResult NewJewel()
            => View(new JewelEditViewModel { Category = JewelCategory.Necklace });

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NewJewel(JewelEditViewModel vm, IFormFile? ImageFile)
        {
            if (!ModelState.IsValid) return View(vm);

            var imageUrl = await _images.SaveAsync(ImageFile, "jewels", HttpContext.RequestAborted);

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

            return View(new JewelEditViewModel
            {
                Id = j.Id,
                Name = j.Name,
                Category = j.Category,
                BasePrice = j.BasePrice,
                QuantityOnHand = j.QuantityOnHand,
                CurrentImageUrl = j.ImageUrl
            });
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
                j.ImageUrl = await _images.SaveAsync(ImageFile, "jewels", HttpContext.RequestAborted);

            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Jewels));
        }

        [HttpGet("/Admin/DesignOrders")]
        public async Task<IActionResult> DesignOrders()
        {
            var orders = await _db.DesignOrders
                .OrderByDescending(o => o.CreatedUtc)
                .ToListAsync();

            return View("~/Views/Admin/DesignOrders.cshtml", orders);
        }

        [HttpGet("/Admin/ReadyDesignOrders")]
        public async Task<IActionResult> ReadyDesignOrders()
        {
            var orders = await _db.DesignOrders
                .Where(o => o.Status == "Built")
                .OrderByDescending(o => o.CreatedUtc)
                .ToListAsync();

            return View("~/Views/Admin/ReadyDesignOrders.cshtml", orders);
        }

        // NEW
        [HttpGet("/Admin/ItemOrders")]
        public async Task<IActionResult> ItemOrders()
        {
            var orders = await _db.Orders
                .Include(o => o.Items)
                .Where(o => o.OrderType == "Item")
                .OrderByDescending(o => o.CreatedUtc)
                .ToListAsync();

            return View("~/Views/Admin/ItemOrders.cshtml", orders);
        }

        // NEW
        [HttpGet("/Admin/JewelryOrders")]
        public async Task<IActionResult> JewelryOrders()
        {
            var orders = await _db.Orders
                .Include(o => o.Items)
                .Where(o => o.OrderType == "Jewelry" || o.OrderType == "Mixed")
                .OrderByDescending(o => o.CreatedUtc)
                .ToListAsync();

            return View("~/Views/Admin/JewelryOrders.cshtml", orders);
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

        [HttpGet("/Admin/DesignOrder/{id:int}")]
        public async Task<IActionResult> DesignOrder(int id)
        {
            var vm = await BuildDesignOrderVmAsync(id);
            if (vm == null) return NotFound();

            await LoadCompaniesAsync();
            return View("~/Views/Admin/DesignOrderDetails.cshtml", vm);
        }

        [HttpGet("/Admin/DesignOrder/{id:int}/Protocol")]
        public async Task<IActionResult> DesignOrderProtocol(int id)
        {
            var vm = await BuildDesignOrderVmAsync(id);
            if (vm == null) return NotFound();

            await LoadCompaniesAsync();
            return View("~/Views/Admin/DesignOrderProtocol.cshtml", vm);
        }

        private async Task<DesignOrderDetailsVm?> BuildDesignOrderVmAsync(int id)
        {
            var o = await _db.DesignOrders.FirstOrDefaultAsync(x => x.Id == id);
            if (o == null) return null;

            List<PatternRowDto> rowsDto;
            try
            {
                rowsDto = JsonSerializer.Deserialize<List<PatternRowDto>>(
                    string.IsNullOrWhiteSpace(o.PatternJson) ? "[]" : o.PatternJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<PatternRowDto>();
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

            return vm;
        }

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
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<PatternRowDto>();
            }
            catch
            {
                rowsDto = new List<PatternRowDto>();
            }

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

            foreach (var r in rows)
            {
                var need = r.count * repeats * qtyToBuild;
                comps[r.componentId].QuantityOnHand -= need;
            }

            Jewel? newJewel = null;
            if (createJewel)
            {
                var previewPath = await _images.SaveDataUrlAsync(o.PreviewDataUrl, "previews", HttpContext.RequestAborted);

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
                        new NeedSource { designOrderId = designOrderId, qty = addQty, createdUtc = DateTime.UtcNow }
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
                try
                {
                    list = JsonSerializer.Deserialize<List<NeedSource>>(need.SourcesJson ?? "[]") ?? new();
                }
                catch
                {
                    list = new();
                }

                list.Add(new NeedSource { designOrderId = designOrderId, qty = addQty, createdUtc = DateTime.UtcNow });
                need.SourcesJson = JsonSerializer.Serialize(list);
            }
        }

        [HttpGet("/Admin/Dashboard")]
        public async Task<IActionResult> Dashboard()
        {
            var vm = await _dashboard.GetAsync();
            return View("~/Views/Admin/Dashboard.cshtml", vm);
        }

        [HttpGet("/Admin/SalesInvoices")]
        public async Task<IActionResult> SalesInvoices()
        {
            var list = await _db.SalesInvoices
                .OrderByDescending(i => i.IssuedOnUtc)
                .Include(i => i.Company)
                .Include(i => i.Lines)
                .ThenInclude(l => l.Article)
                .ToListAsync();

            return View("~/Views/Admin/SalesInvoices.cshtml", list);
        }

        [HttpGet("/Admin/SalesInvoice/{id:int}/Print")]
        public async Task<IActionResult> PrintSalesInvoice(int id)
        {
            var inv = await _db.SalesInvoices
                .Include(i => i.Company)
                .Include(i => i.Lines)
                .ThenInclude(l => l.Article)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (inv == null) return NotFound();
            return View("~/Views/Admin/SalesInvoicePrint.cshtml", inv);
        }

        [HttpPost("/Admin/DesignOrder/{id:int}/Sell")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SellBuiltDesign(
            int id,
            [FromForm] int qty,
            [FromForm] decimal profitPercent,
            [FromForm] int? companyId)
        {
            var o = await _db.DesignOrders.FirstOrDefaultAsync(x => x.Id == id);
            if (o == null) return NotFound();

            if (!string.Equals(o.Status, "Built", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Err"] = "Order must be in 'Built' status.";
                return RedirectToAction(nameof(DesignOrder), new { id });
            }

            var articleName = !string.IsNullOrWhiteSpace(o.DesignName) ? o.DesignName! : $"Custom #{o.Id}";
            var article = await _db.Articles.FirstOrDefaultAsync(a => a.Name == articleName);
            if (article == null)
            {
                article = new Article
                {
                    Name = articleName,
                    Category = o.Category.ToString(),
                    MaterialsCostPerPiece = o.UnitPriceEstimate ?? 0m,
                    DefaultMarkupPercent = profitPercent
                };
                _db.Articles.Add(article);
                await _db.SaveChangesAsync();
            }

            var unitPrice = decimal.Round(article.MaterialsCostPerPiece * (1 + (profitPercent / 100m)), 2);
            qty = Math.Max(1, qty);

            var inv = new SalesInvoice
            {
                Number = $"S-{DateTime.UtcNow:yyyyMMddHHmmss}",
                IssuedOnUtc = DateTime.UtcNow,
                CompanyId = companyId,
                CustomerName = o.CustomerName,
                CustomerEmail = o.CustomerEmail,
                SellerUserName = User?.Identity?.Name ?? "admin",
                ProfitPercent = profitPercent,
                SourceDesignOrderId = o.Id
            };

            inv.Lines.Add(new SalesInvoiceLine
            {
                ArticleId = article.Id,
                Quantity = qty,
                UnitPrice = unitPrice
            });

            inv.Total = unitPrice * qty;

            _db.SalesInvoices.Add(inv);

            o.SalesInvoiceId = inv.Id;
            o.SoldQty = qty;
            o.SoldOnUtc = DateTime.UtcNow;
            o.Status = "Sold";

            await _db.SaveChangesAsync();

            TempData["Ok"] = $"Sales invoice {inv.Number} created for {qty} × '{article.Name}'.";
            return RedirectToAction(nameof(PrintSalesInvoice), new { id = inv.Id });
        }

        private async Task LoadCompaniesAsync()
        {
            ViewBag.Companies = await _db.Companies
                .OrderBy(c => c.Name)
                .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name })
                .ToListAsync();
        }
    }
}