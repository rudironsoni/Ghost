using FluentAssertions;
using Ghost.Sdk.Spider.Core.Extraction;
using Ghost.Sdk.Spider.Tests.TestHelpers;
using NUnit.Framework;

namespace Ghost.Sdk.Spider.Tests.Unit.Entities;

[TestFixture]
public class EntityParserTests
{
    private EntityParser _parser = null!;

    [SetUp]
    public void Setup()
    {
        _parser = new EntityParser();
    }

    [Test]
    public async Task Parse_WithXPathSelector_ShouldExtractEntities()
    {
        // Arrange
        var html = await TestData.ReadFixtureAsync("test-html.html");
        var context = new ExtractionContext
        {
            Content = html,
            SourceUrl = "https://test.com",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var results = _parser.Parse<TestProduct>(context);

        // Assert
        results.Should().NotBeEmpty();
        results.Should().HaveCountGreaterThan(0);
    }

    [Test]
    public async Task Parse_WithCssSelector_ShouldExtractMultipleEntities()
    {
        // Arrange
        var html = await TestData.ReadFixtureAsync("test-html.html");
        var context = new ExtractionContext
        {
            Content = html,
            SourceUrl = "https://test.com",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var results = _parser.Parse<TestProduct>(context);

        // Assert
        results.Should().HaveCountGreaterThan(1);
        results.All(r => r.SourceUrl == "https://test.com").Should().BeTrue();
        results.All(r => r.Id != null).Should().BeTrue();
    }

    [Test]
    public async Task Parse_WithComplexHtml_ShouldExtractAllFields()
    {
        // Arrange
        var html = await TestData.ReadFixtureAsync("complex-html.html");
        var context = new ExtractionContext
        {
            Content = html,
            SourceUrl = "https://test.com",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var results = _parser.Parse<TestArticle>(context);

        // Assert
        results.Should().NotBeEmpty();
        var firstArticle = results.First();
        firstArticle.Title.Should().NotBeNullOrEmpty();
        firstArticle.AuthorId.Should().HaveValue();
    }

    [Test]
    public void Parse_WithInvalidHtml_ShouldReturnEmptyList()
    {
        // Arrange
        var context = new ExtractionContext
        {
            Content = "<html><invalid",
            SourceUrl = "https://test.com",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var results = _parser.Parse<TestProduct>(context);

        // Assert
        results.Should().BeEmpty();
    }

    [Test]
    public void Parse_WithEmptyContent_ShouldReturnEmptyList()
    {
        // Arrange
        var context = new ExtractionContext
        {
            Content = string.Empty,
            SourceUrl = "https://test.com",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var results = _parser.Parse<TestProduct>(context);

        // Assert
        results.Should().BeEmpty();
    }

    [Test]
    public void ParseSingle_ShouldExtractSingleEntity()
    {
        // Arrange
        var html = TestData.SampleHtml;
        var context = new ExtractionContext
        {
            Content = html,
            SourceUrl = "https://test.com",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = _parser.ParseSingle<TestArticle>(context);

        // Assert
        result.Should().NotBeNull();
        result!.SourceUrl.Should().Be("https://test.com");
        result.ExtractedAt.Should().NotBeNull();
        result.Id.Should().NotBeNullOrEmpty();
    }

    [Test]
    public void ParseSingle_WithNoMatches_ShouldReturnNull()
    {
        // Arrange
        var context = new ExtractionContext
        {
            Content = "<html><body><p>No matching content</p></body></html>",
            SourceUrl = "https://test.com",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = _parser.ParseSingle<TestProduct>(context);

        // Assert
        result.Should().BeNull();
    }

    [Test]
    public void Parse_ShouldSetBaseProperties()
    {
        // Arrange
        var html = TestData.SampleHtml;
        var sourceUrl = "https://example.com/test";
        var timestamp = DateTime.UtcNow;
        var context = new ExtractionContext
        {
            Content = html,
            SourceUrl = sourceUrl,
            Timestamp = timestamp
        };

        // Act
        var results = _parser.Parse<TestProduct>(context);

        // Assert
        results.Should().NotBeEmpty();
        foreach (var result in results)
        {
            result.SourceUrl.Should().Be(sourceUrl);
            result.ExtractedAt.Should().BeCloseTo(timestamp, TimeSpan.FromSeconds(1));
            result.Id.Should().NotBeNullOrEmpty();
        }
    }

    [Test]
    public async Task Parse_WithAttributeSelector_ShouldExtractAttribute()
    {
        // Arrange
        var html = await TestData.ReadFixtureAsync("test-html.html");
        var context = new ExtractionContext
        {
            Content = html,
            SourceUrl = "https://test.com",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var results = _parser.Parse<TestProduct>(context);

        // Assert
        results.Should().NotBeEmpty();
        results.First().Price.Should().NotBeNullOrEmpty();
    }

    [Test]
    public void Parse_WithTypeConversion_ShouldConvertToCorrectType()
    {
        // Arrange
        var html = TestData.SampleHtml;
        var context = new ExtractionContext
        {
            Content = html,
            SourceUrl = "https://test.com",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var results = _parser.Parse<TestProduct>(context);

        // Assert
        results.Should().NotBeEmpty();
        var firstProduct = results.First();
        firstProduct.ProductId.Should().NotBeNull();
        firstProduct.ProductId.Should().BeGreaterThan(0);
    }

    [Test]
    public async Task Parse_WithJsonContent_ShouldExtractFromJson()
    {
        // Arrange
        var json = await TestData.ReadFixtureAsync("test-json.json");
        var context = new ExtractionContext
        {
            Content = json,
            SourceUrl = "https://api.test.com",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var results = _parser.Parse<TestApiItem>(context);

        // Assert
        results.Should().NotBeEmpty();
        var firstItem = results.First();
        firstItem.Id.Should().HaveValue();
        firstItem.Title.Should().NotBeNullOrEmpty();
        firstItem.Value.Should().HaveValue();
    }

    [Test]
    public void Parse_WithValidation_ShouldOnlyReturnValidEntities()
    {
        // Arrange
        var html = TestData.SampleHtml;
        var context = new ExtractionContext
        {
            Content = html,
            SourceUrl = "https://test.com",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var results = _parser.Parse<TestProduct>(context);

        // Assert
        results.Should().NotBeEmpty();
        results.All(r => r.Validate()).Should().BeTrue();
    }
}
