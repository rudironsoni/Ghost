using FluentAssertions;
using Ghost.Sdk.Loaders;
using Xunit;

namespace Ghost.Sdk.Tests.Loaders;

public sealed class ItemLoaderIntegrationTests
{
    private sealed class Product
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Price { get; set; }
        public string? Category { get; set; }
        public string? Sku { get; set; }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void LoadItem_WithRealWorldHtml_ExtractsAndTransformsData()
    {
        // Arrange
        var html = @"
<!DOCTYPE html>
<html>
<head><title>Product Page</title></head>
<body>
    <div class='product'>
        <h1 id='product-name'>  Gaming Laptop - High Performance  </h1>
        <p class='description'>
            <strong>Amazing laptop</strong> with great specs.
            Perfect for gaming and productivity.
        </p>
        <div class='price-container'>
            <span class='price'>$1299.99</span>
        </div>
        <div class='details'>
            <span class='sku'>SKU: LAP-2024-001</span>
        </div>
    </div>
</body>
</html>";

        // Act
        var product = new ItemLoader<Product>()
            .AddCss("Name", "#product-name")
            .AddProcessor("Name", ItemLoaderProcessors.Strip())
            .AddProcessor("Name", ItemLoaderProcessors.NormalizeWhitespace())

            .AddCss("Description", ".description")
            .AddProcessor("Description", ItemLoaderProcessors.StripHtml())
            .AddProcessor("Description", ItemLoaderProcessors.NormalizeWhitespace())
            .AddProcessor("Description", ItemLoaderProcessors.Strip())

            .AddCss("Price", ".price")
            .AddProcessor("Price", ItemLoaderProcessors.Strip())
            .AddProcessor("Price", ItemLoaderProcessors.RegexExtract(@"\d+\.\d+"))

            .AddValue("Category", "Electronics")

            .AddCss("Sku", ".sku")
            .AddProcessor("Sku", ItemLoaderProcessors.RegexExtract(@"SKU:\s*(.+)"))
            .AddProcessor("Sku", ItemLoaderProcessors.Strip())

            .LoadItem(html);

        // Assert
        product.Should().NotBeNull();
        product.Name.Should().Be("Gaming Laptop - High Performance");
        product.Description.Should().Be("Amazing laptop with great specs. Perfect for gaming and productivity.");
        product.Price.Should().Be("1299.99");
        product.Category.Should().Be("Electronics");
        product.Sku.Should().NotBeNullOrEmpty();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void LoadItem_WithMissingFields_UsesDefaults()
    {
        // Arrange
        var html = @"
<!DOCTYPE html>
<html>
<body>
    <h1>Incomplete Product</h1>
</body>
</html>";

        // Act
        var product = new ItemLoader<Product>()
            .AddXPath("Name", "//h1")
            .AddXPath("Price", "//span[@class='price']")
            .AddProcessor("Price", ItemLoaderProcessors.DefaultIfEmpty("Price not available"))
            .AddValue("Category", "General")
            .LoadItem(html);

        // Assert
        product.Should().NotBeNull();
        product.Name.Should().Be("Incomplete Product");
        product.Price.Should().Be("Price not available");
        product.Category.Should().Be("General");
        product.Description.Should().BeNullOrEmpty();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void LoadItem_WithComplexProcessing_ProducesCleanData()
    {
        // Arrange
        var html = @"
<!DOCTYPE html>
<html>
<body>
    <div class='product'>
        <h1>   WIRELESS    HEADPHONES   </h1>
        <div class='price'>Price: USD $89.99 (including tax)</div>
    </div>
</body>
</html>";

        // Act
        var product = new ItemLoader<Product>()
            .AddXPath("Name", "//h1")
            .AddProcessor("Name", ItemLoaderProcessors.NormalizeWhitespace())
            .AddProcessor("Name", ItemLoaderProcessors.ToLower())
            .AddProcessor("Name", s => System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(s))

            .AddCss("Price", ".price")
            .AddProcessor("Price", ItemLoaderProcessors.RegexExtract(@"\$[\d.]+"))
            .AddProcessor("Price", ItemLoaderProcessors.Replace("$", ""))

            .LoadItem(html);

        // Assert
        product.Should().NotBeNull();
        product.Name.Should().Be("Wireless Headphones");
        product.Price.Should().Be("89.99");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void LoadItem_WithMultipleSources_AggregatesData()
    {
        // Arrange
        var html = @"
<!DOCTYPE html>
<html>
<body>
    <div class='product'>
        <h1>Smartphone</h1>
        <div class='category-tags'>
            <span>Electronics</span>
            <span>Mobile Devices</span>
            <span>Communication</span>
        </div>
    </div>
</body>
</html>";

        // Act
        var product = new ItemLoader<Product>()
            .AddXPath("Name", "//h1")
            .AddXPath("Category", "//div[@class='category-tags']/span[1]")
            .AddXPath("Category", "//div[@class='category-tags']/span[2]")
            .AddProcessor("Category", ItemLoaderProcessors.Join(" > "))
            .LoadItem(html);

        // Assert
        product.Should().NotBeNull();
        product.Name.Should().Be("Smartphone");
        product.Category.Should().Be("Electronics > Mobile Devices");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void LoadItem_WithNestedElements_ExtractsCorrectly()
    {
        // Arrange
        var html = @"
<!DOCTYPE html>
<html>
<body>
    <article>
        <header>
            <h1>Premium Coffee Maker</h1>
        </header>
        <section class='details'>
            <div class='pricing'>
                <span class='original-price' style='text-decoration: line-through;'>$299.99</span>
                <span class='sale-price'>$199.99</span>
            </div>
            <p class='long-description'>
                This amazing coffee maker will brew the perfect cup every time.
                Features include programmable timer, auto-shutoff, and a thermal carafe.
                Perfect for coffee enthusiasts who demand quality.
            </p>
        </section>
    </article>
</body>
</html>";

        // Act
        var product = new ItemLoader<Product>()
            .AddXPath("Name", "//article/header/h1")

            .AddCss("Price", ".sale-price")
            .AddProcessor("Price", ItemLoaderProcessors.Strip())
            .AddProcessor("Price", ItemLoaderProcessors.Replace("$", ""))

            .AddCss("Description", ".long-description")
            .AddProcessor("Description", ItemLoaderProcessors.NormalizeWhitespace())
            .AddProcessor("Description", ItemLoaderProcessors.Take(100))

            .LoadItem(html);

        // Assert
        product.Should().NotBeNull();
        product.Name.Should().Be("Premium Coffee Maker");
        product.Price.Should().Be("199.99");
        product.Description.Should().HaveLength(100);
        product.Description.Should().StartWith("This amazing coffee maker");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void LoadItems_WithSimpleHtml_ReturnsListOfItems()
    {
        // Arrange
        var html = @"
<!DOCTYPE html>
<html>
<body>
    <div class='product'>
        <h1>Test Product</h1>
        <span class='price'>$99.99</span>
    </div>
</body>
</html>";

        // Act
        var products = new ItemLoader<Product>()
            .AddXPath("Name", "//h1")
            .AddCss("Price", ".price")
            .LoadItems(html);

        // Assert
        products.Should().NotBeNull();
        products.Should().HaveCount(1);
        products[0].Name.Should().Be("Test Product");
        products[0].Price.Should().Be("$99.99");
    }
}
