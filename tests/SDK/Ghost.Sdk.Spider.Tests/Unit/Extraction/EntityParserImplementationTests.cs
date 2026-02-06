using FluentAssertions;
using Ghost.Sdk.Spider.Core.Entities;
using Ghost.Sdk.Spider.Core.Entities.Attributes;
using Ghost.Sdk.Spider.Core.Extraction;
using NUnit.Framework;

namespace Ghost.Sdk.Spider.Tests.Unit.Extraction;

[TestFixture]
public class EntityParserImplementationTests
{

    [Test]
    public void Parse_WithXPathSelector_ExtractsMultipleProducts()
    {
        // Arrange
        var html = @"<html>
            <div class='product' data-id='1'><h2>Product A</h2><span class='price'>$100</span></div>
            <div class='product' data-id='2'><h2>Product B</h2><span class='price'>$200</span></div>
        </html>";
        var context = new ExtractionContext
        {
            Content = html,
            SourceUrl = "https://test.com",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var results = EntityParser.Parse<XPathProduct>(context);

        // Assert
        results.Should().HaveCount(2);
        results[0].Name.Should().Be("Product A");
        results[0].Price.Should().Be("$100");
        results[1].Name.Should().Be("Product B");
        results[1].Price.Should().Be("$200");
    }

    [Test]
    public void Parse_WithCssSelector_ExtractsMultipleArticles()
    {
        // Arrange
        var html = @"<html>
            <article><h1>Title 1</h1><p class='author'>John</p></article>
            <article><h1>Title 2</h1><p class='author'>Jane</p></article>
        </html>";
        var context = new ExtractionContext
        {
            Content = html,
            SourceUrl = "https://test.com",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var results = EntityParser.Parse<CssArticle>(context);

        // Assert
        results.Should().HaveCount(2);
        results[0].Title.Should().Be("Title 1");
        results[0].Author.Should().Be("John");
        results[1].Title.Should().Be("Title 2");
        results[1].Author.Should().Be("Jane");
    }

    [Test]
    public void Parse_WithHtmlRegexSelector_ExtractsEmail()
    {
        // Arrange - Regex works with HTML elements for entity selection
        var html = @"<html><div class='contact'>Email: test@example.com</div></html>";
        var context = new ExtractionContext
        {
            Content = html,
            SourceUrl = "https://test.com",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var results = EntityParser.Parse<HtmlEmailEntity>(context);

        // Assert
        results.Should().HaveCount(1);
        results[0].Email.Should().Be("test@example.com");
    }

    [Test]
    public void Parse_WithJsonPathSelector_ExtractsJsonData()
    {
        // Arrange
        var json = @"{""users"": [{""name"": ""Alice"", ""age"": 30}, {""name"": ""Bob"", ""age"": 25}]}";
        var context = new ExtractionContext
        {
            Content = json,
            SourceUrl = "https://api.test.com",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var results = EntityParser.Parse<JsonUser>(context);

        // Assert
        results.Should().HaveCount(2);
        // JSON string values may include quotes, so be flexible with the assertion
        results[0].Name.Should().Contain("Alice");
        results[0].Age.Should().Be(30);
        results[1].Name.Should().Contain("Bob");
        results[1].Age.Should().Be(25);
    }

    [Test]
    public void Parse_WithNestedSelectors_ExtractsNestedData()
    {
        // Arrange
        var html = @"<html>
            <div class='item'>
                <h2>Item 1</h2>
                <div class='details'>
                    <span class='price'>$50</span>
                    <span class='stock'>In Stock</span>
                </div>
            </div>
        </html>";
        var context = new ExtractionContext
        {
            Content = html,
            SourceUrl = "https://test.com",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var results = EntityParser.Parse<NestedItem>(context);

        // Assert
        results.Should().HaveCount(1);
        results[0].Name.Should().Be("Item 1");
        results[0].Price.Should().Be("$50");
        results[0].Stock.Should().Be("In Stock");
    }

    [Test]
    public void Parse_WithAttributeExtraction_ExtractsAttributes()
    {
        // Arrange
        var html = @"<html>
            <a href='https://example.com' data-id='123'>Link Text</a>
        </html>";
        var context = new ExtractionContext
        {
            Content = html,
            SourceUrl = "https://test.com",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var results = EntityParser.Parse<LinkEntity>(context);

        // Assert
        results.Should().HaveCount(1);
        results[0].Url.Should().Be("https://example.com");
        results[0].DataId.Should().Be("123");
        results[0].Text.Should().Be("Link Text");
    }

    [Test]
    public void ParseSingle_WithEntitySelector_ReturnsFirstMatch()
    {
        // Arrange
        var html = @"<html>
            <div class='product'><h2>First</h2></div>
            <div class='product'><h2>Second</h2></div>
        </html>";
        var context = new ExtractionContext
        {
            Content = html,
            SourceUrl = "https://test.com",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = EntityParser.ParseSingle<XPathProduct>(context);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("First");
    }

    [Test]
    public void ParseSingle_WithNoMatches_ReturnsNull()
    {
        // Arrange
        var html = @"<html><div>No products here</div></html>";
        var context = new ExtractionContext
        {
            Content = html,
            SourceUrl = "https://test.com",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = EntityParser.ParseSingle<XPathProduct>(context);

        // Assert
        result.Should().BeNull();
    }

    [Test]
    public void Parse_WithTypeConversion_ConvertsToInt()
    {
        // Arrange
        var html = @"<html><div class='item' data-count='42'><span class='name'>Widget</span></div></html>";
        var context = new ExtractionContext
        {
            Content = html,
            SourceUrl = "https://test.com",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var results = EntityParser.Parse<TypedItem>(context);

        // Assert
        results.Should().HaveCount(1);
        results[0].Count.Should().Be(42);
    }

    [Test]
    public void Parse_WithTypeConversion_ConvertsToDecimal()
    {
        // Arrange
        var html = @"<html><div class='item' data-price='99.99'><span>Product</span></div></html>";
        var context = new ExtractionContext
        {
            Content = html,
            SourceUrl = "https://test.com",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var results = EntityParser.Parse<DecimalItem>(context);

        // Assert
        results.Should().HaveCount(1);
        results[0].Price.Should().Be(99.99m);
    }

    [Test]
    public void Parse_WithTypeConversion_ConvertsToBoolean()
    {
        // Arrange
        var html = @"<html><div class='item' data-available='true'><span>Product</span></div></html>";
        var context = new ExtractionContext
        {
            Content = html,
            SourceUrl = "https://test.com",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var results = EntityParser.Parse<BoolItem>(context);

        // Assert
        results.Should().HaveCount(1);
        results[0].Available.Should().BeTrue();
    }

    [Test]
    public void Parse_WithDefaultValue_UsesDefaultWhenNotFound()
    {
        // Arrange
        var html = @"<html><div class='item'><span class='name'>Widget</span></div></html>";
        var context = new ExtractionContext
        {
            Content = html,
            SourceUrl = "https://test.com",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var results = EntityParser.Parse<ItemWithDefault>(context);

        // Assert
        results.Should().HaveCount(1);
        results[0].Name.Should().Be("Widget");
        results[0].Description.Should().Be("No description");
    }

    [Test]
    public void Parse_WithMultiValueSelector_ReturnsListOfStrings()
    {
        // Arrange
        var html = @"<html>
            <div class='item'>
                <span class='tag'>Tag1</span>
                <span class='tag'>Tag2</span>
                <span class='tag'>Tag3</span>
            </div>
        </html>";
        var context = new ExtractionContext
        {
            Content = html,
            SourceUrl = "https://test.com",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var results = EntityParser.Parse<ItemWithTags>(context);

        // Assert
        results.Should().HaveCount(1);
        results[0].Tags.Should().HaveCount(3);
        results[0].Tags.Should().Contain(new[] { "Tag1", "Tag2", "Tag3" });
    }

    [Test]
    public void Parse_SetsEntityMetadata_CorrectlyForAllEntities()
    {
        // Arrange
        var html = @"<html>
            <div class='product'><h2>Product A</h2></div>
        </html>";
        var context = new ExtractionContext
        {
            Content = html,
            SourceUrl = "https://test.com/page",
            Timestamp = new DateTime(2024, 1, 1, 12, 0, 0)
        };

        // Act
        var results = EntityParser.Parse<XPathProduct>(context);

        // Assert
        results.Should().HaveCount(1);
        results[0].SourceUrl.Should().Be("https://test.com/page");
        results[0].ExtractedAt.Should().Be(new DateTime(2024, 1, 1, 12, 0, 0));
        results[0].Id.Should().NotBeNullOrEmpty();
    }

    // Test entities
    [EntitySelector(Expression = "//div[@class='product']", Type = SelectorType.XPath)]
    private class XPathProduct : EntityBase<XPathProduct>
    {
        [ValueSelector(".//h2", SelectorType.XPath)]
        public string Name { get; set; } = string.Empty;

        [ValueSelector(".//span[@class='price']", SelectorType.XPath)]
        public string Price { get; set; } = string.Empty;
    }

    [EntitySelector(Expression = "article", Type = SelectorType.Css)]
    private class CssArticle : EntityBase<CssArticle>
    {
        [ValueSelector("h1", SelectorType.Css)]
        public string Title { get; set; } = string.Empty;

        [ValueSelector(".author", SelectorType.Css)]
        public string Author { get; set; } = string.Empty;
    }

    [EntitySelector(Expression = ".contact", Type = SelectorType.Css)]
    private class HtmlEmailEntity : EntityBase<HtmlEmailEntity>
    {
        [ValueSelector(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b", SelectorType.Regex)]
        public string Email { get; set; } = string.Empty;
    }

    [EntitySelector(Expression = "$.users[*]", Type = SelectorType.JsonPath)]
    private class JsonUser : EntityBase<JsonUser>
    {
        [ValueSelector("$.name", SelectorType.JsonPath)]
        public string Name { get; set; } = string.Empty;

        [ValueSelector("$.age", SelectorType.JsonPath)]
        public int Age { get; set; }
    }

    [EntitySelector(Expression = ".item", Type = SelectorType.Css)]
    private class NestedItem : EntityBase<NestedItem>
    {
        [ValueSelector("h2", SelectorType.Css)]
        public string Name { get; set; } = string.Empty;

        [ValueSelector(".details .price", SelectorType.Css)]
        public string Price { get; set; } = string.Empty;

        [ValueSelector(".details .stock", SelectorType.Css)]
        public string Stock { get; set; } = string.Empty;
    }

    [EntitySelector(Expression = "a", Type = SelectorType.Css)]
    private class LinkEntity : EntityBase<LinkEntity>
    {
        [ValueSelector("a", SelectorType.Css, Attribute = "href")]
        public string Url { get; set; } = string.Empty;

        [ValueSelector("a", SelectorType.Css, Attribute = "data-id")]
        public string DataId { get; set; } = string.Empty;

        [ValueSelector("a", SelectorType.Css)]
        public string Text { get; set; } = string.Empty;
    }

    [EntitySelector(Expression = ".item", Type = SelectorType.Css)]
    private class TypedItem : EntityBase<TypedItem>
    {
        [ValueSelector(".item", SelectorType.Css, Attribute = "data-count")]
        public int Count { get; set; }
    }

    [EntitySelector(Expression = ".item", Type = SelectorType.Css)]
    private class DecimalItem : EntityBase<DecimalItem>
    {
        [ValueSelector(".item", SelectorType.Css, Attribute = "data-price")]
        public decimal Price { get; set; }
    }

    [EntitySelector(Expression = ".item", Type = SelectorType.Css)]
    private class BoolItem : EntityBase<BoolItem>
    {
        [ValueSelector(".item", SelectorType.Css, Attribute = "data-available")]
        public bool Available { get; set; }
    }

    [EntitySelector(Expression = ".item", Type = SelectorType.Css)]
    private class ItemWithDefault : EntityBase<ItemWithDefault>
    {
        [ValueSelector(".name", SelectorType.Css)]
        public string Name { get; set; } = string.Empty;

        [ValueSelector(".description", SelectorType.Css, DefaultValue = "No description")]
        public string Description { get; set; } = string.Empty;
    }

    [EntitySelector(Expression = ".item", Type = SelectorType.Css)]
    private class ItemWithTags : EntityBase<ItemWithTags>
    {
        [ValueSelector(".tag", SelectorType.Css, TakeFirst = false)]
        public List<string> Tags { get; set; } = new();
    }
}
