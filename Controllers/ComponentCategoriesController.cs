using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OneJevelsCompany.Core.Entities;
using OneJevelsCompany.Infrastructure.Persistence;
using OneJevelsCompany.Web.Models.Admin;

namespace OneJevelsCompany.Web.Controllers
{
    // Routes:
    //   /Admin/ComponentCategories
    //   /Admin/ComponentCategories/Create
    //   /Admin/ComponentCategories/Edit/{id}
    //   /Admin/ComponentCategories/Delete/{id}
    //   /Admin/NewCategory
    [Authorize]
    [Route("Admin/ComponentCategories")]
    public class ComponentCategoriesController : Controller
    {
        private readonly AppDbContext _db;

        public ComponentCategoriesController(AppDbContext db)
        {
            _db = db;
        }

        private async Task<int> NextSortAsync()
            => (await _db.ComponentCategories.MaxAsync(x => (int?)x.SortOrder)) ?? 0;

        private Task<bool> NameExistsAsync(string name, int? excludeId = null)
        {
            name = name.Trim();

            return _db.ComponentCategories.AnyAsync(
                x => x.Name == name &&
                     (!excludeId.HasValue || x.Id != excludeId.Value));
        }

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var rows = await _db.ComponentCategories
                .Include(c => c.Components)
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .Select(c => new CategoryRowViewModel
                {
                    Id = c.Id,
                    Name = c.Name,
                    SortOrder = c.SortOrder,
                    Components = c.Components.Count
                })
                .ToListAsync();

            return View("~/Views/ComponentCategories/Index.cshtml", rows);
        }

        [HttpGet("Create")]
        public async Task<IActionResult> Create()
        {
            var next = await NextSortAsync();

            var vm = new CategoryEditViewModel
            {
                SortOrder = next + 10
            };

            return View("~/Views/ComponentCategories/Create.cshtml", vm);
        }

        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CategoryEditViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                return View("~/Views/ComponentCategories/Create.cshtml", vm);
            }

            if (await NameExistsAsync(vm.Name))
            {
                ModelState.AddModelError(
                    nameof(vm.Name),
                    "A category with this name already exists.");

                return View("~/Views/ComponentCategories/Create.cshtml", vm);
            }

            _db.ComponentCategories.Add(new ComponentCategory
            {
                Name = vm.Name.Trim(),
                SortOrder = vm.SortOrder
            });

            await _db.SaveChangesAsync();

            TempData["ok"] = "Category created.";

            return RedirectToAction(nameof(Index));
        }

        [HttpGet("Edit/{id:int}")]
        public async Task<IActionResult> Edit(int id)
        {
            var cat = await _db.ComponentCategories
                .FirstOrDefaultAsync(x => x.Id == id);

            if (cat == null)
            {
                return NotFound();
            }

            var vm = new CategoryEditViewModel
            {
                Id = cat.Id,
                Name = cat.Name,
                SortOrder = cat.SortOrder
            };

            return View("~/Views/ComponentCategories/Edit.cshtml", vm);
        }

        [HttpPost("Edit/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, CategoryEditViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                return View("~/Views/ComponentCategories/Edit.cshtml", vm);
            }

            var cat = await _db.ComponentCategories
                .FirstOrDefaultAsync(x => x.Id == id);

            if (cat == null)
            {
                return NotFound();
            }

            if (await NameExistsAsync(vm.Name, excludeId: id))
            {
                ModelState.AddModelError(
                    nameof(vm.Name),
                    "A category with this name already exists.");

                return View("~/Views/ComponentCategories/Edit.cshtml", vm);
            }

            cat.Name = vm.Name.Trim();
            cat.SortOrder = vm.SortOrder;

            await _db.SaveChangesAsync();

            TempData["ok"] = "Category saved.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost("Delete/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var cat = await _db.ComponentCategories
                .Include(c => c.Components)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (cat == null)
            {
                return NotFound();
            }

            if (cat.Components.Any())
            {
                TempData["err"] =
                    "Cannot delete a category that has components.";

                return RedirectToAction(nameof(Index));
            }

            _db.ComponentCategories.Remove(cat);

            await _db.SaveChangesAsync();

            TempData["ok"] = "Category deleted.";

            return RedirectToAction(nameof(Index));
        }

        [HttpGet("/Admin/NewCategory")]
        public async Task<IActionResult> NewCategory()
        {
            var next = await NextSortAsync();

            var vm = new CategoryEditViewModel
            {
                SortOrder = next + 10
            };

            return View("~/Views/Admin/NewCategory.cshtml", vm);
        }

        [HttpPost("/Admin/NewCategory")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NewCategory(CategoryEditViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                return View("~/Views/Admin/NewCategory.cshtml", vm);
            }

            if (await NameExistsAsync(vm.Name))
            {
                ModelState.AddModelError(
                    nameof(vm.Name),
                    "A category with this name already exists.");

                return View("~/Views/Admin/NewCategory.cshtml", vm);
            }

            _db.ComponentCategories.Add(new ComponentCategory
            {
                Name = vm.Name.Trim(),
                SortOrder = vm.SortOrder
            });

            await _db.SaveChangesAsync();

            TempData["ok"] =
                $"Category “{vm.Name.Trim()}” created.";

            return Redirect("/Admin/NewInvoice");
        }
    }
}
