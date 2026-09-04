using Microsoft.AspNetCore.Mvc;
using OneJevelsCompany.Core.Enums;
using OneJevelsCompany.Core.Interfaces;

namespace OneJevelsCompany.Web.Controllers
{
    public class DesignsController : Controller
    {
        private readonly IProductService _products;

        public DesignsController(IProductService products)
        {
            _products = products;
        }

        public async Task<IActionResult> Index(JewelCategory? category)
        {
            var designs = await _products.GetBestDesignsAsync(category);

            return View(designs);
        }
    }
}