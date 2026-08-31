using OneJevelsCompany.Web.Models;
using OneJevelsCompany.Web.Services.Inventory;

namespace OneJewelsCompany.UnitTests;

[TestFixture]
public class InventoryServiceTests
{
    [Test]
    public async Task ValidateCart_ready_jewel_checks_stock()
    {
        await using var db=TestDb.Create(); var j=new Jewel{Name="J",QuantityOnHand=2}; db.Add(j); await db.SaveChangesAsync(); var sut=new InventoryService(db);
        Assert.Multiple(()=>{Assert.That(sut.ValidateCartAsync(new[]{new CartItem{ReadyJewelId=j.Id,Quantity=2}}).Result,Is.True);Assert.That(sut.ValidateCartAsync(new[]{new CartItem{ReadyJewelId=j.Id,Quantity=3}}).Result,Is.False);});
    }

    [Test]
    public async Task ValidateCart_collection_checks_stock()
    {
        await using var db=TestDb.Create(); var c=new Collection{Name="Set",QuantityOnHand=1}; db.Add(c); await db.SaveChangesAsync(); var sut=new InventoryService(db);
        Assert.That(await sut.ValidateCartAsync(new[]{new CartItem{CollectionId=c.Id,Quantity=1}}),Is.True); Assert.That(await sut.ValidateCartAsync(new[]{new CartItem{CollectionId=c.Id,Quantity=2}}),Is.False);
    }

    [Test]
    public async Task ValidateCart_custom_counts_repeated_components_times_quantity()
    {
        await using var db=TestDb.Create(); var cat=new ComponentCategory{Name="Bead"}; db.Add(cat); await db.SaveChangesAsync(); var c=new Component{Name="Pearl",ComponentCategoryId=cat.Id,QuantityOnHand=4}; db.Add(c); await db.SaveChangesAsync(); var sut=new InventoryService(db);
        Assert.That(await sut.ValidateCartAsync(new[]{new CartItem{ComponentIdsCsv=$"{c.Id},{c.Id}",Quantity=2}}),Is.True); Assert.That(await sut.ValidateCartAsync(new[]{new CartItem{ComponentIdsCsv=$"{c.Id},{c.Id}",Quantity=3}}),Is.False);
    }

    [TestCase(0)] [TestCase(-1)] public async Task ValidateCart_rejects_non_positive_quantity(int qty){await using var db=TestDb.Create(); Assert.That(await new InventoryService(db).ValidateCartAsync(new[]{new CartItem{ReadyJewelId=1,Quantity=qty}}),Is.False);}
    [Test] public async Task ValidateCart_rejects_unknown_line(){await using var db=TestDb.Create(); Assert.That(await new InventoryService(db).ValidateCartAsync(new[]{new CartItem{Quantity=1}}),Is.False);}

    [Test]
    public async Task Decrement_paid_order_decrements_ready_jewel()
    {
        await using var db=TestDb.Create(); var j=new Jewel{Name="J",QuantityOnHand=5}; db.Add(j); await db.SaveChangesAsync(); await new InventoryService(db).DecrementOnPaidOrderAsync(new Order{Items={new OrderItem{ReadyJewelId=j.Id,Quantity=2}}}); Assert.That(j.QuantityOnHand,Is.EqualTo(3));
    }

    [Test]
    public async Task Decrement_paid_order_decrements_repeated_custom_components()
    {
        await using var db=TestDb.Create(); var cat=new ComponentCategory{Name="Bead"}; db.Add(cat); await db.SaveChangesAsync(); var c=new Component{Name="P",ComponentCategoryId=cat.Id,QuantityOnHand=10}; db.Add(c); await db.SaveChangesAsync(); await new InventoryService(db).DecrementOnPaidOrderAsync(new Order{Items={new OrderItem{ComponentIdsCsv=$"{c.Id},{c.Id}",Quantity=3}}}); Assert.That(c.QuantityOnHand,Is.EqualTo(4));
    }

    [Test]
    public async Task Decrement_rejects_insufficient_stock_without_negative_quantity()
    {
        await using var db=TestDb.Create(); var j=new Jewel{Name="J",QuantityOnHand=1}; db.Add(j); await db.SaveChangesAsync(); Assert.ThrowsAsync<InvalidOperationException>(async()=>await new InventoryService(db).DecrementOnPaidOrderAsync(new Order{Items={new OrderItem{ReadyJewelId=j.Id,Quantity=2}}})); Assert.That(j.QuantityOnHand,Is.EqualTo(1));
    }

    [Test]
    public async Task ApplyInvoice_increases_component_stock()
    {
        await using var db=TestDb.Create(); var cat=new ComponentCategory{Name="Bead"}; db.Add(cat); await db.SaveChangesAsync(); var c=new Component{Name="P",ComponentCategoryId=cat.Id,QuantityOnHand=2}; db.Add(c); await db.SaveChangesAsync(); var inv=new Invoice(); inv.Lines.Add(new InvoiceLine{ComponentId=c.Id,Quantity=5,UnitCost=1m}); await new InventoryService(db).ApplyInvoiceAsync(inv); Assert.That(c.QuantityOnHand,Is.EqualTo(7));
    }

    [Test]
    public async Task ApplyInvoice_rejects_empty_invoice(){await using var db=TestDb.Create(); Assert.ThrowsAsync<InvalidOperationException>(async()=>await new InventoryService(db).ApplyInvoiceAsync(new Invoice()));}

    [Test]
    public async Task ApplyInvoice_rejects_line_with_multiple_targets(){await using var db=TestDb.Create(); var inv=new Invoice(); inv.Lines.Add(new InvoiceLine{ComponentId=1,JewelId=2,Quantity=1}); Assert.ThrowsAsync<InvalidOperationException>(async()=>await new InventoryService(db).ApplyInvoiceAsync(inv));}

    [Test]
    public async Task AdjustCollectionStock_prevents_negative_stock()
    {
        await using var db=TestDb.Create(); var c=new Collection{Name="Set",QuantityOnHand=2}; db.Add(c); await db.SaveChangesAsync(); var sut=new InventoryService(db); await sut.AdjustCollectionStockAsync(c.Id,-2); Assert.That(c.QuantityOnHand,Is.Zero); Assert.ThrowsAsync<InvalidOperationException>(async()=>await sut.AdjustCollectionStockAsync(c.Id,-1));
    }
}
