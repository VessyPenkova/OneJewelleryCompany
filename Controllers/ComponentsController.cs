using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;
using OneJevelsCompany.Infrastructure.Persistence;
using OneJevelsCompany.Core.Entities;
using OneJevelsCompany.Web.Models.Admin;

namespace OneJevelsCompany.Web.Controllers
{
    //==========================================================
    //============= Lives under /Admin/Components ==============
    //==========================================================
    [Authorize]
    [Route("Admin/Components")]
    public class ComponentsController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;

        public ComponentsController(AppDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        //==========================================================
        //======================== helpers =========================
        //==========================================================
        private async Task<List<SelectListItem>> CategorySelectListAsync(int? selectedId = null)
        {
            var items = await _db.ComponentCategories
                .Where(c => c.IsActive)
                .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
                .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name })
                .ToListAsync();

            if (selectedId.HasValue)
            {
                foreach (var i in items.Where(i => i.Value == selectedId.Value.ToString()))
                    i.Selected = true;
            }

            return items;
        }

        //==========================================================
        //============ GET: /Admin/Components/New ==================
        //============ ALSO: /Admin/NewComponent ===================
        //==========================================================
        [HttpGet("New")]
        [HttpGet("/Admin/NewComponent")]
        public async Task<IActionResult> New()
        {
            ViewBag.Categories = await CategorySelectListAsync();
            return View("~/Views/Admin/NewComponent.cshtml", new AdminNewComponentVm());
        }

        //==========================================================
        //============ POST: /Admin/Components/New =================
        //============ ALSO: /Admin/NewComponent ===================
        //==========================================================
        [HttpPost("New")]
        [HttpPost("/Admin/NewComponent")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> New(AdminNewComponentVm vm, IFormFile? image)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Categories = await CategorySelectListAsync(vm.ComponentCategoryId);
                return View("~/Views/Admin/NewComponent.cshtml", vm);
            }

            var categoryExists = await _db.ComponentCategories
                .AnyAsync(c => c.Id == vm.ComponentCategoryId);

            if (!categoryExists)
            {
                ModelState.AddModelError(nameof(vm.ComponentCategoryId), "Category not found.");
                ViewBag.Categories = await CategorySelectListAsync();
                return View("~/Views/Admin/NewComponent.cshtml", vm);
            }

            // prevent duplicate name within the same category
            bool duplicate = await _db.Components.AnyAsync(c =>
                c.ComponentCategoryId == vm.ComponentCategoryId &&
                c.Name == vm.Name.Trim());

            if (duplicate)
            {
                ModelState.AddModelError(
                    nameof(vm.Name),
                    "An item with this name already exists in the selected category.");

                ViewBag.Categories = await CategorySelectListAsync(vm.ComponentCategoryId);
                return View("~/Views/Admin/NewComponent.cshtml", vm);
            }

            string? imageUrl = null;

            if (image != null && image.Length > 0)
            {
                var uploads = Path.Combine(_env.WebRootPath, "uploads", "components");
                Directory.CreateDirectory(uploads);

                var ext = Path.GetExtension(image.FileName);
                var fileName = $"{Guid.NewGuid():N}{ext}";

                using var fs = new FileStream(
                    Path.Combine(uploads, fileName),
                    FileMode.Create);

                await image.CopyToAsync(fs);

                imageUrl = $"/uploads/components/{fileName}";
            }

            var comp = new Component
            {
                Name = vm.Name.Trim(),
                ComponentCategoryId = vm.ComponentCategoryId,
                Price = vm.Price,
                Sku = vm.Sku,
                Color = vm.Color,
                SizeLabel = vm.SizeLabel,
                Dimensions = vm.Dimensions,
                ImageUrl = imageUrl,
                QuantityOnHand = 0 // receive via invoice later
            };

            _db.Components.Add(comp);
            await _db.SaveChangesAsync();

            TempData["ok"] = $"Item “{comp.Name}” created.";
            return Redirect("/Admin/NewInvoice");
        }

        //==========================================================
        //=========== GET: /Admin/Components/Edit/{id} =============
        //==========================================================
        [HttpGet("Edit/{id:int}")]
        public async Task<IActionResult> Edit(int id)
        {
            var c = await _db.Components.FirstOrDefaultAsync(x => x.Id == id);
            if (c == null) return NotFound();

            var vm = new AdminComponentEditVm
            {
                Id = c.Id,
                Name = c.Name,
                Price = c.Price,
                QuantityOnHand = c.QuantityOnHand,
                Sku = c.Sku,
                Color = c.Color,
                SizeLabel = c.SizeLabel,
                Dimensions = c.Dimensions,
                CurrentImageUrl = c.ImageUrl,
                Description = c.Description
            };

            // explicit path so Razor picks the correct view
            return View("~/Views/Components/Edit.cshtml", vm);
        }

        //==========================================================
        //=========== POST: /Admin/Components/Edit/{id} ============
        //==========================================================
        [HttpPost("Edit/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, AdminComponentEditVm vm, IFormFile? image)
        {
            if (!ModelState.IsValid)
            {
                // ensure CurrentImageUrl shows when re-rendering form
                vm.CurrentImageUrl = await _db.Components
                    .Where(x => x.Id == id)
                    .Select(x => x.ImageUrl)
                    .FirstOrDefaultAsync();

                return View("~/Views/Components/Edit.cshtml", vm);
            }

            var c = await _db.Components.FirstOrDefaultAsync(x => x.Id == id);
            if (c == null) return NotFound();

            if (image != null && image.Length > 0)
            {
                var uploads = Path.Combine(_env.WebRootPath, "uploads", "components");
                Directory.CreateDirectory(uploads);

                var ext = Path.GetExtension(image.FileName);
                var fileName = $"{Guid.NewGuid():N}{ext}";

                using var fs = new FileStream(
                    Path.Combine(uploads, fileName),
                    FileMode.Create);

                await image.CopyToAsync(fs);

                c.ImageUrl = $"/uploads/components/{fileName}";
            }

            c.Name = vm.Name.Trim();
            c.Price = vm.Price;
            c.QuantityOnHand = vm.QuantityOnHand;
            c.Sku = vm.Sku;
            c.Color = vm.Color;
            c.SizeLabel = vm.SizeLabel;
            c.Dimensions = vm.Dimensions;
            c.Description = vm.Description;

            await _db.SaveChangesAsync();

            TempData["ok"] = "Component saved.";
            return Redirect("/Admin/Components");
        }
 
    }
}
