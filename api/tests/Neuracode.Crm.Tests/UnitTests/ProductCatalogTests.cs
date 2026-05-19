using System.Text.Json;
using FluentAssertions;
using Neuracode.Crm.Api.Domain;

namespace Neuracode.Crm.Tests.UnitTests;

public class ProductCatalogTests
{
    [Theory]
    [InlineData("BOSS", "Brazalete BOSS", 279, 389)]
    [InlineData("boss", "Brazalete BOSS", 279, 389)]
    [InlineData("METROPOLE", "Brazalete METROPOLE", 149, 249)]
    [InlineData("ANGEL_EYES", "Pulsera ANGEL EYES", 149, 249)]
    [InlineData("COMBOS_LOVE", "Combo Love (brazalete + pulsera)", 249, 449)]
    public void Find_KnownSku_ReturnsProduct(string sku, string expectedName, int minPrice, int maxPrice)
    {
        var product = ProductCatalog.Find(ProductCatalog.Default, sku);

        product.Should().NotBeNull();
        product!.Name.Should().Be(expectedName);
        product.PriceMin.Should().Be(minPrice);
        product.PriceMax.Should().Be(maxPrice);
    }

    [Fact]
    public void Find_UnknownSku_ReturnsNull()
    {
        ProductCatalog.Find(ProductCatalog.Default, "ANILLO").Should().BeNull();
    }

    [Fact]
    public void Parse_ValidJson_ReturnsEntries()
    {
        var json = """
            [
              {"sku":"TEST","name":"Test Product","price_min":100,"price_max":200,"currency":"PEN","category":"caballero","in_stock":true}
            ]
            """;

        var catalog = ProductCatalog.Parse(json);

        catalog.Should().HaveCount(1);
        catalog[0].Sku.Should().Be("TEST");
        catalog[0].PriceMin.Should().Be(100);
    }

    [Fact]
    public void Parse_InvalidJson_FallsBackToDefault()
    {
        var catalog = ProductCatalog.Parse("not json");
        catalog.Should().BeEquivalentTo(ProductCatalog.Default);
    }

    [Fact]
    public void ExecuteToolCall_GetProductPrice_ReturnsJson()
    {
        var input = JsonDocument.Parse("""{"sku":"BOSS"}""").RootElement;

        var result = ProductCatalog.ExecuteToolCall("get_product_price", input, ProductCatalog.Default);

        result.Should().Contain("\"sku\":\"BOSS\"");
        result.Should().Contain("\"price_min\":279");
        result.Should().Contain("\"price_max\":389");
        result.Should().Contain("\"in_stock\":true");
    }

    [Fact]
    public void ExecuteToolCall_CheckStock_ReturnsJson()
    {
        var input = JsonDocument.Parse("""{"sku":"METROPOLE"}""").RootElement;

        var result = ProductCatalog.ExecuteToolCall("check_stock", input, ProductCatalog.Default);

        result.Should().Contain("\"sku\":\"METROPOLE\"");
        result.Should().Contain("\"in_stock\":true");
    }

    [Fact]
    public void ExecuteToolCall_UnknownSku_ReturnsError()
    {
        var input = JsonDocument.Parse("""{"sku":"ANILLO"}""").RootElement;

        var result = ProductCatalog.ExecuteToolCall("get_product_price", input, ProductCatalog.Default);

        result.Should().Contain("error");
        result.Should().Contain("ANILLO");
    }

    [Fact]
    public void ExecuteToolCall_UnknownTool_ReturnsError()
    {
        var input = JsonDocument.Parse("""{"sku":"BOSS"}""").RootElement;

        var result = ProductCatalog.ExecuteToolCall("unknown_tool", input, ProductCatalog.Default);

        result.Should().Contain("error");
    }

    [Fact]
    public void Default_HasSixProducts()
    {
        ProductCatalog.Default.Should().HaveCount(6);
    }

    [Theory]
    [InlineData("METROPOLE")]
    [InlineData("BOSS")]
    [InlineData("E.ARMANI")]
    [InlineData("CROCODILE")]
    [InlineData("ANGEL_EYES")]
    [InlineData("COMBOS_LOVE")]
    public void Default_AllProducts_HaveValidPrices(string sku)
    {
        var p = ProductCatalog.Find(ProductCatalog.Default, sku);
        p.Should().NotBeNull();
        p!.PriceMin.Should().BeGreaterThan(0);
        p.PriceMax.Should().BeGreaterThanOrEqualTo(p.PriceMin);
    }
}
