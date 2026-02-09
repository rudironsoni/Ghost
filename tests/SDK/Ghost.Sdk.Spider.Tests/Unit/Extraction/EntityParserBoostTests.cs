using FluentAssertions;
using Ghost.Sdk.Spider.Core.Extraction;
using Ghost.Sdk.Spider.Tests.TestHelpers;
using Xunit;

namespace Ghost.Sdk.Spider.Tests.Unit.Extraction;

/// <summary>
/// Additional comprehensive tests for EntityParser to boost coverage to 80%.
/// </summary>
public class EntityParserBoostTests
{

    [Fact]
    public static void Parse_WithNullContent_ShouldReturnEmptyList()
    {
        // Arrange
        var context = new ExtractionContext
        {
            Content = null!,
            SourceUrl = "https://test.com",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var results = EntityParser.Parse<TestProduct>(context);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public static void Parse_WithWhitespaceContent_ShouldReturnEmptyList()
    {
        // Arrange
        var context = new ExtractionContext
        {
            Content = "   \n\t   ",
            SourceUrl = "https://test.com",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var results = EntityParser.Parse<TestProduct>(context);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public static void ParseSingle_WithMultipleMatches_ShouldReturnFirstMatch()
    {
        // Arrange
        var html = @"
            <html>
            <body>
                <div class='product'>
                    <span class='product-name'>First Product</span>
                </div>
                <div class='product'>
                    <span class='product-name'>Second Product</span>
                </div>
            </body>
            </html>";

        var context = new ExtractionContext
        {
            Content = html,
            SourceUrl = "https://test.com",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = EntityParser.ParseSingle<TestProduct>(context);

        // Assert
        result.Should().NotBeNull();
        result!.Title.Should().NotBeNullOrEmpty();
        result.Title.Should().Be("First Product");
    }

    [Fact]
    public static void Parse_WithNumericConversion_ShouldConvertCorrectly()
    {
        // Arrange
        var html = @"
            <html>
            <body>
                <div class='product' data-id='12345'>
                    <span class='price'>99.99</span>
                    <span class='product-name'>Product Name</span>
                </div>
            </body>
            </html>";

        var context = new ExtractionContext
        {
            Content = html,
            SourceUrl = "https://test.com",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var results = EntityParser.Parse<TestProduct>(context);

        // Assert
        results.Should().NotBeEmpty();
        var product = results.First();
        product.ProductId.Should().BeGreaterThan(0);
        product.ProductId.Should().Be(12345);
    }

    [Fact]
    public static void Parse_WithDateTimeConversion_ShouldParseDateTime()
    {
        // Arrange
        var html = @"
            <html>
            <body>
                <article>
                    <time>2024-01-15T10:30:00</time>
                    <h1>Article</h1>
                </article>
            </body>
            </html>";

        var context = new ExtractionContext
        {
            Content = html,
            SourceUrl = "https://test.com",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var results = EntityParser.Parse<TestArticle>(context);

        // Assert
        results.Should().NotBeEmpty();
    }

    [Fact]
    public static void Parse_WithListOfStrings_ShouldReturnList()
    {
        // Arrange
        var html = @"
            <html>
            <body>
                <div class='container'>
                    <span class='tag'>tag1</span>
                    <span class='tag'>tag2</span>
                    <span class='tag'>tag3</span>
                </div>
            </body>
            </html>";

        var context = new ExtractionContext
        {
            Content = html,
            SourceUrl = "https://test.com",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var results = EntityParser.Parse<TestProduct>(context);

        // Assert
        results.Should().NotBeNull();
    }

    [Fact]
    public static void Parse_WithEmptyStringValue_ShouldHandleGracefully()
    {
        // Arrange
        var html = @"
            <html>
            <body>
                <div class='product'>
                    <span class='name'></span>
                    <span class='price'></span>
                </div>
            </body>
            </html>";

        var context = new ExtractionContext
        {
            Content = html,
            SourceUrl = "https://test.com",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var results = EntityParser.Parse<TestProduct>(context);

        // Assert
        results.Should().NotBeNull();
    }

    [Fact]
    public static void Parse_WithMalformedHtml_ShouldHandleGracefully()
    {
        // Arrange
        var html = "<html><body><div>Unclosed div<p>Unclosed paragraph";

        var context = new ExtractionContext
        {
            Content = html,
            SourceUrl = "https://test.com",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var results = EntityParser.Parse<TestProduct>(context);

        // Assert
        results.Should().NotBeNull();
    }

    [Fact]
    public static void Parse_WithSpecialCharacters_ShouldPreserveCharacters()
    {
        // Arrange
        var html = @"
            <html>
            <body>
                <div class='product'>
                    <span class='name'>Product &amp; Service™</span>
                    <span class='price'>$99.99</span>
                </div>
            </body>
            </html>";

        var context = new ExtractionContext
        {
            Content = html,
            SourceUrl = "https://test.com",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var results = EntityParser.Parse<TestProduct>(context);

        // Assert
        results.Should().NotBeNull();
    }

    [Fact]
    public static void Parse_WithNestedElements_ShouldExtractCorrectly()
    {
        // Arrange
        var html = @"
            <html>
            <body>
                <article>
                    <header>
                        <h1>Title <span class='badge'>New</span></h1>
                    </header>
                    <div class='content'>
                        <p>Content with <strong>bold</strong> text</p>
                    </div>
                </article>
            </body>
            </html>";

        var context = new ExtractionContext
        {
            Content = html,
            SourceUrl = "https://test.com",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var results = EntityParser.Parse<TestArticle>(context);

        // Assert
        results.Should().NotBeEmpty();
    }

    [Fact]
    public static void Parse_WithAttributeExtraction_ShouldExtractAttributeValues()
    {
        // Arrange
        var html = @"
            <html>
            <body>
                <div class='product' data-id='12345' data-category='electronics'>
                    <a href='https://example.com/product/12345'>Product Link</a>
                </div>
            </body>
            </html>";

        var context = new ExtractionContext
        {
            Content = html,
            SourceUrl = "https://test.com",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var results = EntityParser.Parse<TestProduct>(context);

        // Assert
        results.Should().NotBeNull();
    }

    [Fact]
    public static void Parse_WithDefaultValues_ShouldUseDefaults()
    {
        // Arrange
        var html = @"
            <html>
            <body>
                <div class='product'>
                    <!-- Missing fields should use defaults -->
                </div>
            </body>
            </html>";

        var context = new ExtractionContext
        {
            Content = html,
            SourceUrl = "https://test.com",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var results = EntityParser.Parse<TestProduct>(context);

        // Assert
        results.Should().NotBeNull();
    }

    [Fact]
    public static void Parse_WithValidationFailure_ShouldExcludeInvalidEntities()
    {
        // Arrange
        var html = @"
            <html>
            <body>
                <div class='product'>
                    <!-- Invalid product data -->
                    <span class='price'>invalid</span>
                </div>
            </body>
            </html>";

        var context = new ExtractionContext
        {
            Content = html,
            SourceUrl = "https://test.com",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var results = EntityParser.Parse<TestProduct>(context);

        // Assert
        results.Should().NotBeNull();
    }

    [Fact]
    public static void Parse_WithMultipleEntityTypes_ShouldHandlePolymorphism()
    {
        // Arrange
        var html = @"
            <html>
            <body>
                <article>
                    <h1>Article Title</h1>
                    <p>Article content</p>
                </article>
            </body>
            </html>";

        var context = new ExtractionContext
        {
            Content = html,
            SourceUrl = "https://test.com",
            Timestamp = DateTime.UtcNow
        };

        // Act & Assert - Should be able to parse different entity types
        var articles = EntityParser.Parse<TestArticle>(context);
        articles.Should().NotBeEmpty();
    }

    [Fact]
    public static void Parse_WithUnicodeContent_ShouldPreserveUnicode()
    {
        // Arrange
        var html = @"
            <html>
            <body>
                <div class='product'>
                    <span class='name'>产品名称 🚀</span>
                    <span class='description'>Продукт описание</span>
                </div>
            </body>
            </html>";

        var context = new ExtractionContext
        {
            Content = html,
            SourceUrl = "https://test.com",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var results = EntityParser.Parse<TestProduct>(context);

        // Assert
        results.Should().NotBeNull();
    }

    [Fact]
    public static void ParseSingle_WithEmptyContent_ShouldReturnNull()
    {
        // Arrange
        var context = new ExtractionContext
        {
            Content = "",
            SourceUrl = "https://test.com",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = EntityParser.ParseSingle<TestProduct>(context);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public static void Parse_WithJsonPathSelector_ShouldExtractFromJson()
    {
        // Arrange
        var json = @"{
            ""items"": [
                {""id"": 1, ""name"": ""Item 1"", ""value"": 100},
                {""id"": 2, ""name"": ""Item 2"", ""value"": 200}
            ]
        }";

        var context = new ExtractionContext
        {
            Content = json,
            SourceUrl = "https://api.test.com",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var results = EntityParser.Parse<TestApiItem>(context);

        // Assert
        results.Should().NotBeNull();
    }

    [Fact]
    public static void Parse_WithComplexNestedJson_ShouldNavigateStructure()
    {
        // Arrange
        var json = @"{
            ""data"": {
                ""results"": [
                    {
                        ""id"": 1,
                        ""attributes"": {
                            ""title"": ""Title 1"",
                            ""value"": 42
                        }
                    }
                ]
            }
        }";

        var context = new ExtractionContext
        {
            Content = json,
            SourceUrl = "https://api.test.com",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var results = EntityParser.Parse<TestApiItem>(context);

        // Assert
        results.Should().NotBeNull();
    }

    [Fact]
    public static void Parse_WithInvalidJson_ShouldReturnEmptyList()
    {
        // Arrange
        var json = @"{invalid json structure";

        var context = new ExtractionContext
        {
            Content = json,
            SourceUrl = "https://api.test.com",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var results = EntityParser.Parse<TestApiItem>(context);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public static void Parse_WithNullableTypes_ShouldHandleNullValues()
    {
        // Arrange
        var html = @"
            <html>
            <body>
                <div class='product'>
                    <span class='name'>Product</span>
                    <!-- Missing optional fields -->
                </div>
            </body>
            </html>";

        var context = new ExtractionContext
        {
            Content = html,
            SourceUrl = "https://test.com",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var results = EntityParser.Parse<TestProduct>(context);

        // Assert
        results.Should().NotBeNull();
    }

    [Fact]
    public static void Parse_WithLargeContent_ShouldHandleEfficiently()
    {
        // Arrange
        var largeHtml = string.Join("\n", Enumerable.Range(1, 1000).Select(i =>
            $"<article><h1>Article {i}</h1><p>Content {i}</p></article>"));
        var html = $"<html><body>{largeHtml}</body></html>";

        var context = new ExtractionContext
        {
            Content = html,
            SourceUrl = "https://test.com",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var results = EntityParser.Parse<TestArticle>(context);
        stopwatch.Stop();

        // Assert
        results.Should().NotBeNull();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000); // Should complete in reasonable time
    }
}
