using Microsoft.AspNetCore.Http;
using OneJevelsCompany.Core.Entities;
using OneJevelsCompany.Web.Services.Cart;

namespace OneJewelsCompany.UnitTests;

[TestFixture]
public class CartServiceTests
{
    private static DefaultHttpContext Http()
    {
        var http = new DefaultHttpContext();
        http.Session = new TestSession();
        return http;
    }

    [Test] public void Empty_session_returns_empty_cart() => Assert.That(new CartService().GetCart(Http()), Is.Empty);

    [Test]
    public void Add_new_item_preserves_item()
    {
        var http = Http(); var sut = new CartService();
        sut.AddToCart(http, new CartItem { Sku="J1", Title="Ring", Quantity=2, UnitPrice=12.5m, ReadyJewelId=7 });
        var item = sut.GetCart(http).Single();
        Assert.Multiple(() => { Assert.That(item.Sku, Is.EqualTo("J1")); Assert.That(item.Quantity, Is.EqualTo(2)); Assert.That(item.ReadyJewelId, Is.EqualTo(7)); });
    }

    [Test]
    public void Add_same_sku_merges_quantities()
    {
        var http=Http(); var sut=new CartService();
        sut.AddToCart(http,new CartItem{Sku="A",Quantity=2}); sut.AddToCart(http,new CartItem{Sku="A",Quantity=3});
        Assert.That(sut.GetCart(http).Single().Quantity, Is.EqualTo(5));
    }

    [Test]
    public void Update_quantity_clamps_to_one()
    {
        var http=Http(); var sut=new CartService(); sut.AddToCart(http,new CartItem{Sku="A",Quantity=2});
        sut.UpdateQuantity(http,"A",0);
        Assert.That(sut.GetCart(http).Single().Quantity, Is.EqualTo(1));
    }

    [Test]
    public void Remove_deletes_matching_sku()
    {
        var http=Http(); var sut=new CartService(); sut.AddToCart(http,new CartItem{Sku="A"}); sut.AddToCart(http,new CartItem{Sku="B"});
        sut.Remove(http,"A"); Assert.That(sut.GetCart(http).Select(x=>x.Sku), Is.EqualTo(new[]{"B"}));
    }

    [Test]
    public void Clear_empties_cart()
    {
        var http=Http(); var sut=new CartService(); sut.AddToCart(http,new CartItem{Sku="A"}); sut.Clear(http);
        Assert.That(sut.GetCart(http), Is.Empty);
    }

    [Test]
    public void Total_sums_line_totals()
    {
        var http=Http(); var sut=new CartService();
        sut.AddToCart(http,new CartItem{Sku="A",Quantity=2,UnitPrice=10m}); sut.AddToCart(http,new CartItem{Sku="B",Quantity=3,UnitPrice=5m});
        Assert.That(sut.Total(http), Is.EqualTo(35m));
    }
}
