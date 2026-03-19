using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OneJevelsCompany.Web.Data;
using OneJevelsCompany.Web.Routing;
using OneJevelsCompany.Web.Models.Home;

namespace OneJevelsCompany.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _db;

        public HomeController(AppDbContext db)
        {
            _db = db;
        }

        [HttpGet("/", Name = RouteNames.Home.Index)]
        public async Task<IActionResult> Index()
        {
            var collections = await _db.ComponentCategories
                .AsNoTracking()
                .Where(c => c.IsActive)
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .Select(c => new HomeCollectionCardViewModel
                {
                    Id = c.Id,
                    Name = c.Name,
                    SortOrder = c.SortOrder,
                    ComponentsCount = c.Components.Count(x => x.QuantityOnHand > 0),
                    PreviewImageUrl = c.Components
                        .Where(x => !string.IsNullOrWhiteSpace(x.ImageUrl))
                        .OrderBy(x => x.Name)
                        .Select(x => x.ImageUrl)
                        .FirstOrDefault()
                })
                .Where(x => x.ComponentsCount > 0)
                .Take(6)
                .ToListAsync();

            var vm = new HomeIndexViewModel
            {
                FeaturedCollections = collections
            };

            return View(vm);
        }

        [HttpGet("/About", Name = RouteNames.Home.About)]
        public IActionResult About()
        {
            return View();
        }
    }
}