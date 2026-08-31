using Microsoft.EntityFrameworkCore;
using OneJevelsCompany.Web.Data;
using OneJevelsCompany.Web.Models;

namespace OneJevelsCompany.Web.Services.Product
{
    public class ProductService : IProductService
    {
        private readonly AppDbContext _db;
        public ProductService(AppDbContext db) { _db = db; }

        public async Task<List<Jewel>> GetReadyCollectionsAsync(JewelCategory? category = null)
        {
            var q = _db.Jewels
                .Include(j => j.Components)
                    .ThenInclude(jc => jc.Component)
                .AsQueryable();

            if (category.HasValue)
                q = q.Where(j => j.Category == category.Value);

            return await q.OrderBy(j => j.Name).ToListAsync();
        }

        // NOTE: 'type' is in the signature to match your interface;
        // currently unused. You can filter by 'forCategory' here if needed.
        public async Task<List<Component>> GetComponentsAsync(ComponentType? type = null, JewelCategory? forCategory = null)
        {
            var q = _db.Components
                .Include(c => c.Category)
                .AsQueryable();

            if (type.HasValue)
            {
                var typeName = type.Value.ToString();
                q = q.Where(c => c.Category != null && c.Category.Name == typeName);
            }

            // There is currently no Component -> JewelCategory relationship in the data model,
            // so forCategory cannot be applied safely without inventing business rules.

            return await q
                .OrderBy(c => c.Category == null ? 999 : c.Category.SortOrder)
                .ThenBy(c => c.Category == null ? "Other" : c.Category.Name)
                .ThenBy(c => c.Name)
                .ToListAsync();
        }

        public async Task<decimal> CalculateCustomPriceAsync(IEnumerable<int> componentIds)
        {
            var requested = componentIds.Where(id => id > 0).ToList();
            if (requested.Count == 0) return 0m;

            var counts = requested.GroupBy(id => id).ToDictionary(g => g.Key, g => g.Count());
            var components = await _db.Components
                .Where(c => counts.Keys.Contains(c.Id))
                .ToListAsync();

            if (components.Count != counts.Count)
                throw new InvalidOperationException("One or more selected components do not exist.");

            return components.Sum(c => c.Price * counts[c.Id]);
        }

        public async Task<string> DescribeComponentsAsync(IEnumerable<int> componentIds)
        {
            var requested = componentIds.Where(id => id > 0).ToList();
            if (requested.Count == 0) return string.Empty;

            var counts = requested.GroupBy(id => id).ToDictionary(g => g.Key, g => g.Count());
            var comps = await _db.Components
                .Include(c => c.Category)
                .Where(c => counts.Keys.Contains(c.Id))
                .ToListAsync();

            if (comps.Count != counts.Count)
                throw new InvalidOperationException("One or more selected components do not exist.");

            return string.Join(", ", comps
                .OrderBy(c => c.Category == null ? 999 : c.Category.SortOrder)
                .ThenBy(c => c.Name)
                .Select(c => $"{counts[c.Id]}× {(c.Category?.Name ?? "Component")}: {c.Name}"));
        }

        public Task<List<Design>> GetBestDesignsAsync(JewelCategory? category = null)
        {
            var q = _db.Designs.AsQueryable();
            if (category.HasValue)
                q = q.Where(d => d.Category == category.Value);

            return q.OrderBy(d => d.Name).ToListAsync();
        }

        public Task<Jewel?> GetJewelAsync(int id) =>
            _db.Jewels
              .Include(j => j.Components)
                .ThenInclude(jc => jc.Component)
              .FirstOrDefaultAsync(j => j.Id == id);

        // NEW: required by ShopController for Details/Configure
        public Task<Component?> GetComponentAsync(int id) =>
            _db.Components
               .Include(c => c.Category)
               // If you later add a related table for dimension options, include it here.
               .FirstOrDefaultAsync(c => c.Id == id);
    }
}
