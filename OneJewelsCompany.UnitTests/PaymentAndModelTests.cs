using Microsoft.Extensions.Configuration;
using OneJevelsCompany.Web.Models;
using OneJevelsCompany.Web.Services.Payment;

namespace OneJewelsCompany.UnitTests;

[TestFixture]
public class PaymentAndModelTests
{
    [Test]
    public async Task Payment_intent_converts_decimal_to_cents()
    {
        var cfg=new ConfigurationBuilder().Build(); var p=await new StripePaymentService(cfg).CreateOrUpdatePaymentIntentAsync(42,12.34m,"eur");
        Assert.Multiple(()=>{Assert.That(p.AmountInCents,Is.EqualTo(1234));Assert.That(p.Currency,Is.EqualTo("eur"));Assert.That(p.Id,Does.StartWith("pi_test_42_"));Assert.That(p.ClientSecret,Is.Not.Empty);});
    }

    [Test]
    public void Cart_line_total_is_quantity_times_unit_price(){var i=new CartItem{Quantity=3,UnitPrice=2.5m}; Assert.That(i.LineTotal,Is.EqualTo(7.5m));}

    [Test]
    public void Jewel_total_price_adds_component_prices()
    {
        var j=new Jewel{BasePrice=10m,Components={new JewelComponent{Component=new Component{Price=2m}},new JewelComponent{Component=new Component{Price=3m}}}}; Assert.That(j.TotalPrice(),Is.EqualTo(15m));
    }
}
