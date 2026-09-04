using OneJevelsCompany.Core.Entities;
using OneJevelsCompany.Core.Enums;
using OneJevelsCompany.Infrastructure.Products;

namespace OneJewelsCompany.UnitTests;

[TestFixture]
public class ProductServiceTests
{
    [Test]
    public async Task Ready_collections_filter_by_jewel_category_and_sort()
    {
        await using var db = TestDb.Create();

        db.Jewels.AddRange(
            new Jewel
            {
                Name = "Z",
                Category = JewelCategory.Bracelet
            },
            new Jewel
            {
                Name = "A",
                Category = JewelCategory.Bracelet
            },
            new Jewel
            {
                Name = "N",
                Category = JewelCategory.Necklace
            });

        await db.SaveChangesAsync();

        var result = await new ProductService(db)
            .GetReadyCollectionsAsync(JewelCategory.Bracelet);

        Assert.That(
            result.Select(x => x.Name),
            Is.EqualTo(new[] { "A", "Z" }));
    }

    [Test]
    public async Task Custom_price_counts_repeated_components()
    {
        await using var db = TestDb.Create();

        var cat = new ComponentCategory
        {
            Name = "Bead"
        };

        db.ComponentCategories.Add(cat);
        await db.SaveChangesAsync();

        db.Components.AddRange(
            new Component
            {
                Name = "A",
                Price = 2m,
                ComponentCategoryId = cat.Id
            },
            new Component
            {
                Name = "B",
                Price = 5m,
                ComponentCategoryId = cat.Id
            });

        await db.SaveChangesAsync();

        var ids = db.Components
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToArray();

        var result = await new ProductService(db)
            .CalculateCustomPriceAsync(
                new[] { ids[0], ids[0], ids[1] });

        Assert.That(result, Is.EqualTo(9m));
    }

    [Test]
    public async Task Custom_price_rejects_missing_component()
    {
        await using var db = TestDb.Create();

        Assert.ThrowsAsync<InvalidOperationException>(
            async () =>
                await new ProductService(db)
                    .CalculateCustomPriceAsync(new[] { 999 }));
    }

    [Test]
    public async Task Description_includes_component_quantity()
    {
        await using var db = TestDb.Create();

        var cat = new ComponentCategory
        {
            Name = "Bead",
            SortOrder = 1
        };

        db.Add(cat);
        await db.SaveChangesAsync();

        var c = new Component
        {
            Name = "Pearl",
            Price = 1m,
            ComponentCategoryId = cat.Id
        };

        db.Add(c);
        await db.SaveChangesAsync();

        var text = await new ProductService(db)
            .DescribeComponentsAsync(new[] { c.Id, c.Id });

        Assert.That(
            text,
            Does.Contain("2×").And.Contain("Pearl"));
    }

    [Test]
    public async Task GetComponents_filters_component_type_by_category_name()
    {
        await using var db = TestDb.Create();

        var chain = new ComponentCategory
        {
            Name = "Chain",
            SortOrder = 1
        };

        var bead = new ComponentCategory
        {
            Name = "Bead",
            SortOrder = 2
        };

        db.AddRange(chain, bead);
        await db.SaveChangesAsync();

        db.AddRange(
            new Component
            {
                Name = "Gold chain",
                ComponentCategoryId = chain.Id
            },
            new Component
            {
                Name = "Pearl",
                ComponentCategoryId = bead.Id
            });

        await db.SaveChangesAsync();

        var result = await new ProductService(db)
            .GetComponentsAsync(ComponentType.Chain);

        Assert.That(
            result.Select(x => x.Name),
            Is.EqualTo(new[] { "Gold chain" }));
    }

    [Test]
    public async Task Best_designs_filter_and_sort()
    {
        await using var db = TestDb.Create();

        db.Designs.AddRange(
            new Design
            {
                Name = "Z",
                Category = JewelCategory.Necklace
            },
            new Design
            {
                Name = "A",
                Category = JewelCategory.Necklace
            },
            new Design
            {
                Name = "B",
                Category = JewelCategory.Bracelet
            });

        await db.SaveChangesAsync();

        var result = await new ProductService(db)
            .GetBestDesignsAsync(JewelCategory.Necklace);

        Assert.That(
            result.Select(x => x.Name),
            Is.EqualTo(new[] { "A", "Z" }));
    }
}