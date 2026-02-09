using FluentAssertions;
using Ghost.Sdk.Loaders;
using Xunit;

namespace Ghost.Sdk.Tests.Loaders;

public sealed class ItemLoaderTests
{
    private sealed class TestItem
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Price { get; set; }
        public string? Category { get; set; }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void LoadItem_WithXPathExtractor_ExtractsValue()
    {
        // Arrange
        var html = "<html><body><h1>Test Title</h1></body></html>";
        var loader = new ItemLoader<TestItem>()
            .AddXPath("Title", "//h1");

        // Act
        var item = loader.LoadItem(html);

        // Assert
        item.Should().NotBeNull();
        item.Title.Should().Be("Test Title");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void LoadItem_WithCssSelector_ExtractsValue()
    {
        // Arrange
        var html = "<html><body><div class='description'>Test Description</div></body></html>";
        var loader = new ItemLoader<TestItem>()
            .AddCss("Description", ".description");

        // Act
        var item = loader.LoadItem(html);

        // Assert
        item.Should().NotBeNull();
        item.Description.Should().Be("Test Description");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void LoadItem_WithStaticValue_SetsValue()
    {
        // Arrange
        var html = "<html><body></body></html>";
        var loader = new ItemLoader<TestItem>()
            .AddValue("Category", "Electronics");

        // Act
        var item = loader.LoadItem(html);

        // Assert
        item.Should().NotBeNull();
        item.Category.Should().Be("Electronics");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void LoadItem_WithMultipleExtractors_CombinesValues()
    {
        // Arrange
        var html = "<html><body><div class='price'>$100</div></body></html>";
        var loader = new ItemLoader<TestItem>()
            .AddCss("Price", ".price")
            .AddValue("Price", "USD");

        // Act
        var item = loader.LoadItem(html);

        // Assert
        item.Should().NotBeNull();
        item.Price.Should().Be("$100, USD");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void LoadItem_WithProcessor_TransformsValue()
    {
        // Arrange
        var html = "<html><body><h1>  Test Title  </h1></body></html>";
        var loader = new ItemLoader<TestItem>()
            .AddXPath("Title", "//h1")
            .AddProcessor("Title", ItemLoaderProcessors.Strip());

        // Act
        var item = loader.LoadItem(html);

        // Assert
        item.Should().NotBeNull();
        item.Title.Should().Be("Test Title");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void LoadItem_WithChainedProcessors_AppliesInOrder()
    {
        // Arrange
        var html = "<html><body><h1>  TEST TITLE  </h1></body></html>";
        var loader = new ItemLoader<TestItem>()
            .AddXPath("Title", "//h1")
            .AddProcessor("Title", ItemLoaderProcessors.Strip())
            .AddProcessor("Title", ItemLoaderProcessors.ToLower());

        // Act
        var item = loader.LoadItem(html);

        // Assert
        item.Should().NotBeNull();
        item.Title.Should().Be("test title");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void LoadItem_WithNonExistentSelector_SetsEmptyString()
    {
        // Arrange
        var html = "<html><body></body></html>";
        var loader = new ItemLoader<TestItem>()
            .AddXPath("Title", "//h1");

        // Act
        var item = loader.LoadItem(html);

        // Assert
        item.Should().NotBeNull();
        item.Title.Should().Be(string.Empty);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void LoadItem_WithInvalidPropertyName_DoesNotThrow()
    {
        // Arrange
        var html = "<html><body><h1>Test</h1></body></html>";
        var loader = new ItemLoader<TestItem>()
            .AddXPath("NonExistentProperty", "//h1");

        // Act
        var act = () => loader.LoadItem(html);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void LoadItems_ReturnsListWithSingleItem()
    {
        // Arrange
        var html = "<html><body><h1>Test Title</h1></body></html>";
        var loader = new ItemLoader<TestItem>()
            .AddXPath("Title", "//h1");

        // Act
        var items = loader.LoadItems(html);

        // Assert
        items.Should().NotBeNull();
        items.Should().HaveCount(1);
        items[0].Title.Should().Be("Test Title");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AddXPath_WithNullField_ThrowsArgumentNullException()
    {
        // Arrange
        var loader = new ItemLoader<TestItem>();

        // Act
        var act = () => loader.AddXPath(null!, "//h1");

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AddXPath_WithNullXPath_ThrowsArgumentNullException()
    {
        // Arrange
        var loader = new ItemLoader<TestItem>();

        // Act
        var act = () => loader.AddXPath("Title", null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AddCss_WithNullField_ThrowsArgumentNullException()
    {
        // Arrange
        var loader = new ItemLoader<TestItem>();

        // Act
        var act = () => loader.AddCss(null!, ".title");

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AddCss_WithNullSelector_ThrowsArgumentNullException()
    {
        // Arrange
        var loader = new ItemLoader<TestItem>();

        // Act
        var act = () => loader.AddCss("Title", null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AddValue_WithNullField_ThrowsArgumentNullException()
    {
        // Arrange
        var loader = new ItemLoader<TestItem>();

        // Act
        var act = () => loader.AddValue(null!, "value");

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AddValue_WithNullValue_ThrowsArgumentNullException()
    {
        // Arrange
        var loader = new ItemLoader<TestItem>();

        // Act
        var act = () => loader.AddValue("Title", null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AddProcessor_WithNullField_ThrowsArgumentNullException()
    {
        // Arrange
        var loader = new ItemLoader<TestItem>();

        // Act
        var act = () => loader.AddProcessor(null!, s => s);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AddProcessor_WithNullProcessor_ThrowsArgumentNullException()
    {
        // Arrange
        var loader = new ItemLoader<TestItem>();

        // Act
        var act = () => loader.AddProcessor("Title", null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void LoadItem_WithNullHtml_ThrowsArgumentNullException()
    {
        // Arrange
        var loader = new ItemLoader<TestItem>();

        // Act
        var act = () => loader.LoadItem(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void LoadItems_WithNullHtml_ThrowsArgumentNullException()
    {
        // Arrange
        var loader = new ItemLoader<TestItem>();

        // Act
        var act = () => loader.LoadItems(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void LoadItem_SupportsFluentChaining()
    {
        // Arrange
        var html = @"
            <html>
                <body>
                    <h1>Product Title</h1>
                    <p class='description'>Great product</p>
                    <span class='price'>$99.99</span>
                </body>
            </html>";

        // Act
        var item = new ItemLoader<TestItem>()
            .AddXPath("Title", "//h1")
            .AddCss("Description", ".description")
            .AddCss("Price", ".price")
            .AddValue("Category", "Electronics")
            .AddProcessor("Title", ItemLoaderProcessors.Strip())
            .AddProcessor("Price", ItemLoaderProcessors.Strip())
            .LoadItem(html);

        // Assert
        item.Should().NotBeNull();
        item.Title.Should().Be("Product Title");
        item.Description.Should().Be("Great product");
        item.Price.Should().Be("$99.99");
        item.Category.Should().Be("Electronics");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void LoadItem_WithIdSelector_ExtractsValue()
    {
        // Arrange
        var html = "<html><body><div id='title'>Test Title</div></body></html>";
        var loader = new ItemLoader<TestItem>()
            .AddCss("Title", "#title");

        // Act
        var item = loader.LoadItem(html);

        // Assert
        item.Should().NotBeNull();
        item.Title.Should().Be("Test Title");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void LoadItem_WithElementSelector_ExtractsValue()
    {
        // Arrange
        var html = "<html><body><p>Test Description</p></body></html>";
        var loader = new ItemLoader<TestItem>()
            .AddCss("Description", "p");

        // Act
        var item = loader.LoadItem(html);

        // Assert
        item.Should().NotBeNull();
        item.Description.Should().Be("Test Description");
    }
}
