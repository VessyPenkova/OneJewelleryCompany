using Microsoft.EntityFrameworkCore;
using OneJevelsCompany.Web.Models;
// Avoid clash with System.ComponentModel.Component:
using Component = OneJevelsCompany.Web.Models.Component;

namespace OneJevelsCompany.Web.Data
{
    public static class SeedData
    {
        public static async Task ApplyAsync(AppDbContext db)
        {
            // ----- 1) Categories -----
            if (!await db.ComponentCategories.AnyAsync())
            {
                db.ComponentCategories.AddRange(
                    new ComponentCategory { Name = "Chain", SortOrder = 10, IsActive = true },
                    new ComponentCategory { Name = "Clasp", SortOrder = 20, IsActive = true },
                    new ComponentCategory { Name = "Pendant", SortOrder = 30, IsActive = true },
                    new ComponentCategory { Name = "Bead", SortOrder = 40, IsActive = true }
                );
                await db.SaveChangesAsync();
            }

            // Fetch categories (have IDs now)
            var catChain = await db.ComponentCategories.FirstAsync(c => c.Name == "Chain");
            var catClasp = await db.ComponentCategories.FirstAsync(c => c.Name == "Clasp");
            var catPendant = await db.ComponentCategories.FirstAsync(c => c.Name == "Pendant");
            var catBead = await db.ComponentCategories.FirstAsync(c => c.Name == "Bead");

            // ----- 2) Components -----
            if (!await db.Components.AnyAsync())
            {
                var components = new List<Component>
                   {
                       new Component { Name = "Gold Chain (45cm)",      ComponentCategoryId = catChain.Id,   Price = 120m, Sku = "CHN-G-45",   ImageUrl="/Images/Seed/chain-gold-45.jpg",   Dimensions="45cm",        QuantityOnHand = 20 },
                       new Component { Name = "Silver Chain (45cm)",    ComponentCategoryId = catChain.Id,   Price =  40m, Sku = "CHN-S-45",   ImageUrl="/Images/Seed/chain-silver-45.jpg", Dimensions="45cm",        QuantityOnHand = 30 },
                       new Component { Name = "Leather Cord (Black)",   ComponentCategoryId = catChain.Id,   Price =  20m, Sku = "CORD-BLK",   ImageUrl="/Images/Seed/cord-black.jpg",      Dimensions="Adjustable",  QuantityOnHand = 40 },
                   
                       new Component { Name = "Clasp – Gold",           ComponentCategoryId = catClasp.Id,   Price =  25m, Sku = "CLASP-G",     ImageUrl="/Images/Seed/clasp-gold.jpg",      Dimensions="Std",         QuantityOnHand = 50 },
                       new Component { Name = "Clasp – Silver",         ComponentCategoryId = catClasp.Id,   Price =  10m, Sku = "CLASP-S",     ImageUrl="/Images/Seed/clasp-silver.jpg",    Dimensions="Std",         QuantityOnHand = 60 },
                   
                       new Component { Name = "Pendant – Amethyst",     ComponentCategoryId = catPendant.Id, Price =  60m, Sku = "PEND-AMETH",  ImageUrl="/Images/Seed/pendant-amethyst.jpg", Dimensions="Oval 18mm",  QuantityOnHand = 15, Color="Purple" },
                       new Component { Name = "Pendant – Emerald",      ComponentCategoryId = catPendant.Id, Price = 240m, Sku = "PEND-EMRLD",  ImageUrl="/Images/Seed/pendant-emerald.jpg",  Dimensions="Pear 14mm",  QuantityOnHand =  8, Color="Green"  },
                       new Component { Name = "Pendant – Pearl",        ComponentCategoryId = catPendant.Id, Price =  85m, Sku = "PEND-PEARL",   ImageUrl="/Images/Seed/pendant-pearl.jpg",    Dimensions="9mm",        QuantityOnHand = 18, Color="White"  },
                   
                       new Component { Name = "Bead – Onyx (pack)",     ComponentCategoryId = catBead.Id,    Price =  15m, Sku = "BEAD-ONX",    ImageUrl="/Images/Seed/bead-onyx.jpg",        Dimensions="6mm (pack)", QuantityOnHand = 40, Color="Black", SizeLabel="6mm" },
                       new Component { Name = "Bead – Rose Quartz (pack)", ComponentCategoryId = catBead.Id,  Price =  18m, Sku = "BEAD-RQ",      ImageUrl="/Images/Seed/bead-rose-quartz.jpg", Dimensions="6mm (pack)", QuantityOnHand = 35, Color="Pink",  SizeLabel="6mm" },
                       new Component { Name = "Bead – Lapis (pack)",    ComponentCategoryId = catBead.Id,    Price =  22m, Sku = "BEAD-LAPIS",  ImageUrl="/Images/Seed/bead-lapis.jpg",       Dimensions="6mm (pack)", QuantityOnHand = 32, Color="Blue",  SizeLabel="6mm" }
                   };

                db.Components.AddRange(components);
                await db.SaveChangesAsync();
            }

            // We'll need these names when linking jewels below
            var compsByName = await db.Components.ToDictionaryAsync(c => c.Name, c => c);

            // ----- 3) Ready-made Jewels -----
            if (!await db.Jewels.AnyAsync())
            {
                var ready = new List<Jewel>
                {
                    new Jewel
                    {
                        Name = "Classic Gold Pearl Necklace",
                        Category = JewelCategory.Necklace,
                        BasePrice = 0m,
                        ImageUrl = "/images/jewel-gold-pearl-necklace.jpg",
                        QuantityOnHand = 5,
                        Components = new List<JewelComponent>()
                    },
                    new Jewel
                    {
                        Name = "Onyx Serenity Bracelet",
                        Category = JewelCategory.Bracelet,
                        BasePrice = 0m,
                        ImageUrl = "/images/jewel-onyx-serenity-bracelet.jpg",
                        QuantityOnHand = 7,
                        Components = new List<JewelComponent>()
                    }
                };

                db.Jewels.AddRange(ready);
                await db.SaveChangesAsync();
            }

            // Reload jewels (now have IDs)
            var necklace = await db.Jewels.FirstAsync(j => j.Name == "Classic Gold Pearl Necklace");
            var bracelet = await db.Jewels.FirstAsync(j => j.Name == "Onyx Serenity Bracelet");

            // ----- 4) Link components to ready-made jewels (if not already linked) -----
            if (!await db.JewelComponents.AnyAsync())
            {
                void Link(Jewel j, string compName)
                {
                    var c = compsByName[compName];
                    db.JewelComponents.Add(new JewelComponent { JewelId = j.Id, ComponentId = c.Id });
                }

                Link(necklace, "Gold Chain (45cm)");
                Link(necklace, "Clasp – Gold");
                Link(necklace, "Pendant – Pearl");

                Link(bracelet, "Leather Cord (Black)");
                Link(bracelet, "Clasp – Silver");
                Link(bracelet, "Bead – Onyx (pack)");

                await db.SaveChangesAsync();
            }

            // ----- 5) Showcase designs -----
            if (!await db.Designs.AnyAsync())
            {
                db.Designs.AddRange(
                    new Design
                    {
                        Name = "Emerald Minimalist",
                        Category = JewelCategory.Necklace,
                        Description = "Elegant gold chain with emerald pendant.",
                        ImageUrl = "/images/design-emerald-minimalist.jpg"
                    },
                    new Design
                    {
                        Name = "Ocean Lapis",
                        Category = JewelCategory.Bracelet,
                        Description = "Lapis beads on leather with silver clasp.",
                        ImageUrl = "/images/design-ocean-lapis.jpg"
                    }
                );

                await db.SaveChangesAsync();
            }
        }
    }
}
