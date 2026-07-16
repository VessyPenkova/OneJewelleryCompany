using System.Text.RegularExpressions;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;
using Qase.Csharp.Commons.Attributes;

namespace OneJewelsCompany.PlaywrightTests
{
    [TestFixture]
    [Parallelizable(ParallelScope.Self)]
    public class Tests : PageTest
    {
        private const string BaseUrl = "http://localhost:5108";

        [Test]
        [Category("Smoke")]
        public async Task TC001_HomePageLoadsSuccessfully()
        {
            await Page.GotoAsync(BaseUrl);

            await Expect(Page.GetByText("Welcome to One Jewellery Company"))
                .ToBeVisibleAsync();

            await Expect(Page.GetByText("Explore Collections"))
                .ToBeVisibleAsync();
        }

        [Test]
        [Category("Collections")]
        public async Task TC002_ExploreCollectionsOpensCollectionsPage()
        {
            await Page.GotoAsync(BaseUrl);

            await Page.GetByText("Explore Collections").ClickAsync();

            await Expect(Page).ToHaveURLAsync(
                new Regex(@"/Shop/Collections"));

            await Expect(Page.GetByText("Necklaces"))
                .ToBeVisibleAsync();

            await Expect(Page.GetByText("Bracelets"))
                .ToBeVisibleAsync();
        }

        [Test]
        [Category("Collections")]
        public async Task TC003_NecklacesFilterDisplaysNecklaceProducts()
        {
            await Page.GotoAsync($"{BaseUrl}/Shop/Collections");

            await Page.GetByText("Necklaces").ClickAsync();

            await Expect(Page).ToHaveURLAsync(
                new Regex(@"category=Necklace"));

            int necklaceProducts = await Page
                .Locator("text=Category: Necklace")
                .CountAsync();

            Assert.That(
                necklaceProducts,
                Is.GreaterThan(0),
                "No necklace products were displayed.");
        }

        [Test]
        [Category("Collections")]
        public async Task TC004_BraceletsFilterDisplaysBraceletProducts()
        {
            await Page.GotoAsync($"{BaseUrl}/Shop/Collections");

            await Page.GetByText("Bracelets").ClickAsync();

            await Expect(Page).ToHaveURLAsync(
                new Regex(@"category=Bracelet"));

            int braceletProducts = await Page
                .Locator("text=Category: Bracelet")
                .CountAsync();

            Assert.That(
                braceletProducts,
                Is.GreaterThan(0),
                "No bracelet products were displayed.");
        }
    }
}