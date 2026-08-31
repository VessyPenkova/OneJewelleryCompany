using Microsoft.EntityFrameworkCore;
using OneJevelsCompany.Web.Data;
using OneJevelsCompany.Web.Models;

namespace OneJevelsCompany.Web.Services.Inventory
{
    public class InventoryService : IInventoryService
    {
        private readonly AppDbContext _db;
        public InventoryService(AppDbContext db) { _db = db; }

        // ----- Apply inventory received via an invoice -----
        public async Task ApplyInvoiceAsync(Invoice invoice)
        {
            if (invoice.Lines == null || invoice.Lines.Count == 0)
                throw new InvalidOperationException("An invoice must contain at least one line.");

            foreach (var line in invoice.Lines)
            {
                if (line.Quantity <= 0)
                    throw new InvalidOperationException("Invoice quantities must be greater than zero.");
                if (line.UnitCost < 0)
                    throw new InvalidOperationException("Invoice unit cost cannot be negative.");
                var targets = (line.ComponentId.HasValue ? 1 : 0) + (line.JewelId.HasValue ? 1 : 0) + (line.CollectionId.HasValue ? 1 : 0);
                if (targets != 1)
                    throw new InvalidOperationException("Each invoice line must reference exactly one inventory item.");
            }

            _db.Invoices.Add(invoice);

            foreach (var line in invoice.Lines)
            {
                if (line.ComponentId.HasValue)
                {
                    var comp = await _db.Components.FirstOrDefaultAsync(c => c.Id == line.ComponentId.Value)
                        ?? throw new InvalidOperationException($"Component {line.ComponentId.Value} does not exist.");
                    comp.QuantityOnHand += line.Quantity;
                }
                else if (line.JewelId.HasValue)
                {
                    var jewel = await _db.Jewels.FirstOrDefaultAsync(j => j.Id == line.JewelId.Value)
                        ?? throw new InvalidOperationException($"Jewel {line.JewelId.Value} does not exist.");
                    jewel.QuantityOnHand += line.Quantity;
                }
                // Collections received on an invoice (optional)
                else if (line.CollectionId.HasValue)
                {
                    var collection = await _db.Collections.FirstOrDefaultAsync(c => c.Id == line.CollectionId.Value)
                        ?? throw new InvalidOperationException($"Collection {line.CollectionId.Value} does not exist.");
                    collection.QuantityOnHand += line.Quantity;
                }
            }

            await _db.SaveChangesAsync();
        }

        // ----- Validate cart has sufficient stock for each line -----
        public async Task<bool> ValidateCartAsync(IEnumerable<CartItem> items)
        {
            foreach (var i in items)
            {
                if (i.Quantity <= 0) return false;

                // Ready-made jewel
                if (i.ReadyJewelId.HasValue)
                {
                    var j = await _db.Jewels.FirstOrDefaultAsync(x => x.Id == i.ReadyJewelId.Value);
                    if (j == null || j.QuantityOnHand < i.Quantity) return false;
                    continue;
                }

                // Ready-made collection (if your CartItem has CollectionId)
                if (i.CollectionId.HasValue)
                {
                    var col = await _db.Collections.FirstOrDefaultAsync(c => c.Id == i.CollectionId.Value);
                    if (col == null || col.QuantityOnHand < i.Quantity) return false;
                    continue;
                }

                // Custom build using ComponentIdsCsv (IDs may repeat to encode per-piece quantities)
                if (!string.IsNullOrWhiteSpace(i.ComponentIdsCsv))
                {
                    // Parse and aggregate counts per component ID
                    var idList = i.ComponentIdsCsv!.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => int.TryParse(s.Trim(), out var id) ? id : 0)
                        .Where(id => id > 0)
                        .ToList();

                    if (idList.Count == 0) return false;

                    var idCounts = idList
                        .GroupBy(id => id)
                        .ToDictionary(g => g.Key, g => g.Count());

                    // Load all needed components in one query
                    var comps = await _db.Components
                        .Where(c => idCounts.Keys.Contains(c.Id))
                        .ToDictionaryAsync(c => c.Id, c => c);

                    // Ensure each component exists and has enough stock:
                    // needed = (repeats per piece) * (finished quantity)
                    foreach (var kv in idCounts)
                    {
                        if (!comps.TryGetValue(kv.Key, out var comp)) return false;
                        var needed = kv.Value * i.Quantity;
                        if (comp.QuantityOnHand < needed) return false;
                    }

                    continue;
                }

                // If a line reaches here, we don’t know how to validate it
                return false;
            }

            return true;
        }

        // ----- Decrement inventory when an order is paid -----
        public async Task DecrementOnPaidOrderAsync(Order order)
        {
            foreach (var item in order.Items)
            {
                // Ready-made jewel
                if (item.ReadyJewelId.HasValue)
                {
                    var jewel = await _db.Jewels.FirstOrDefaultAsync(j => j.Id == item.ReadyJewelId.Value)
                        ?? throw new InvalidOperationException($"Jewel {item.ReadyJewelId.Value} does not exist.");
                    if (item.Quantity <= 0 || jewel.QuantityOnHand < item.Quantity)
                        throw new InvalidOperationException($"Insufficient stock for {jewel.Name}.");
                    jewel.QuantityOnHand -= item.Quantity;
                    continue;
                }

                // Ready-made collection (if your OrderItem has CollectionId)
                if (item.CollectionId.HasValue)
                {
                    var collection = await _db.Collections.FirstOrDefaultAsync(c => c.Id == item.CollectionId.Value)
                        ?? throw new InvalidOperationException($"Collection {item.CollectionId.Value} does not exist.");
                    if (item.Quantity <= 0 || collection.QuantityOnHand < item.Quantity)
                        throw new InvalidOperationException($"Insufficient stock for {collection.Name}.");
                    collection.QuantityOnHand -= item.Quantity;
                    continue;
                }

                // Custom build using ComponentIdsCsv
                if (!string.IsNullOrWhiteSpace(item.ComponentIdsCsv))
                {
                    var idList = item.ComponentIdsCsv!.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => int.TryParse(s.Trim(), out var id) ? id : 0)
                        .Where(id => id > 0)
                        .ToList();

                    if (idList.Count == 0) continue;

                    var idCounts = idList
                        .GroupBy(id => id)
                        .ToDictionary(g => g.Key, g => g.Count());

                    var comps = await _db.Components
                        .Where(c => idCounts.Keys.Contains(c.Id))
                        .ToListAsync();

                    if (comps.Count != idCounts.Count)
                        throw new InvalidOperationException("One or more components no longer exist.");

                    foreach (var comp in comps)
                    {
                        var repeatsPerPiece = idCounts[comp.Id]; // how many times this comp appears per piece
                        var totalToDecrement = repeatsPerPiece * item.Quantity; // per-piece repeats * finished qty
                        if (item.Quantity <= 0 || comp.QuantityOnHand < totalToDecrement)
                            throw new InvalidOperationException($"Insufficient stock for component {comp.Name}.");
                        comp.QuantityOnHand -= totalToDecrement;
                    }

                    continue;
                }
            }

            await _db.SaveChangesAsync();
        }

        // Optional helper
        public async Task AdjustCollectionStockAsync(int collectionId, int delta)
        {
            var collection = await _db.Collections.FirstOrDefaultAsync(c => c.Id == collectionId);
            if (collection == null)
                throw new InvalidOperationException($"Collection {collectionId} does not exist.");

            var newQuantity = collection.QuantityOnHand + delta;
            if (newQuantity < 0)
                throw new InvalidOperationException("Collection stock cannot be negative.");

            collection.QuantityOnHand = newQuantity;
            await _db.SaveChangesAsync();
        }
    }
}
