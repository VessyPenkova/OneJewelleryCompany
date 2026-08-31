using OneJevelsCompany.Web.Models;
using OneJevelsCompany.Web.Services.Orders;

namespace OneJewelsCompany.UnitTests;

[TestFixture]
public class OrderServiceTests
{
    [Test]
    public async Task CreateOrder_copies_all_business_fields_and_total()
    {
        await using var db=TestDb.Create(); var sut=new OrderService(db);
        var order=await sut.CreateOrderAsync("a@b.com","Street",new[]{new CartItem{Sku="C",Title="Custom",Category=JewelCategory.Bracelet,Quantity=2,UnitPrice=15m,ComponentsSummary="2x bead",ComponentIdsCsv="1,1",ReadyJewelId=3,CollectionId=4,IsCustomBuild=true,RecipeJson="{}",CustomDesignName="Mine"}});
        var i=order.Items.Single();
        Assert.Multiple(()=>{ Assert.That(order.Total,Is.EqualTo(30m)); Assert.That(order.Status,Is.EqualTo("Pending")); Assert.That(i.ReadyJewelId,Is.EqualTo(3)); Assert.That(i.CollectionId,Is.EqualTo(4)); Assert.That(i.ComponentIdsCsv,Is.EqualTo("1,1")); Assert.That(i.IsCustomBuild,Is.True); Assert.That(i.RecipeJson,Is.EqualTo("{}")); Assert.That(i.CustomDesignName,Is.EqualTo("Mine")); });
    }

    [Test]
    public async Task MarkPaid_updates_status_and_provider_id()
    {
        await using var db=TestDb.Create(); var sut=new OrderService(db); var o=await sut.CreateOrderAsync(null,null,Array.Empty<CartItem>());
        await sut.MarkPaidAsync(o.Id,"pay_123"); var saved=await sut.GetAsync(o.Id);
        Assert.Multiple(()=>{Assert.That(saved!.Status,Is.EqualTo("Paid"));Assert.That(saved.PaymentProviderId,Is.EqualTo("pay_123"));});
    }

    [Test] public async Task MarkPaid_unknown_order_is_safe(){await using var db=TestDb.Create(); Assert.DoesNotThrowAsync(async()=>await new OrderService(db).MarkPaidAsync(999,"x"));}

    [Test]
    public async Task Get_loads_order_items()
    {
        await using var db=TestDb.Create(); var sut=new OrderService(db); var o=await sut.CreateOrderAsync(null,null,new[]{new CartItem{Sku="A",Title="A",Quantity=1,UnitPrice=1m}});
        db.ChangeTracker.Clear(); var loaded=await sut.GetAsync(o.Id); Assert.That(loaded!.Items,Has.Count.EqualTo(1));
    }
}
