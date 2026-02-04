using FluentAssertions;
using Ghost.Sdk.Spider.Core.Extraction.Selectors;
using Ghost.Sdk.Spider.Tests.TestHelpers;
using NUnit.Framework;

namespace Ghost.Sdk.Spider.Tests.Unit.Extraction;

[TestFixture]
public class JsonPathSelectorTests
{
    [Test]
    public void Select_WithSimplePath_ShouldReturnValue()
    {
        // Arrange
        var selector = new JsonPathSelector("$.name");
        var json = TestData.SampleJson;

        // Act
        var results = selector.Select(json);

        // Assert
        results.Should().HaveCount(1);
        results[0].Should().Contain("Test Item");
    }

    [Test]
    public void Select_WithArrayPath_ShouldReturnArrayItems()
    {
        // Arrange
        var selector = new JsonPathSelector("$.tags[*]");
        var json = TestData.SampleJson;

        // Act
        var results = selector.Select(json);

        // Assert
        results.Should().HaveCount(3);
    }

    [Test]
    public void Select_WithNestedPath_ShouldReturnNestedValue()
    {
        // Arrange
        var selector = new JsonPathSelector("$.metadata.created");
        var json = TestData.SampleJson;

        // Act
        var results = selector.Select(json);

        // Assert
        results.Should().HaveCount(1);
        results[0].Should().Contain("2026-01-01");
    }

    [Test]
    public async Task Select_WithComplexNesting_ShouldWork()
    {
        // Arrange
        var selector = new JsonPathSelector("$.data.items[*].title");
        var json = await TestData.ReadFixtureAsync("test-json.json");

        // Act
        var results = selector.Select(json);

        // Assert
        results.Should().HaveCount(3);
        results[0].Should().Contain("Item One");
        results[1].Should().Contain("Item Two");
        results[2].Should().Contain("Item Three");
    }

    [Test]
    public void SelectFirst_ShouldReturnFirstMatch()
    {
        // Arrange
        var selector = new JsonPathSelector("$.tags[*]");
        var json = TestData.SampleJson;

        // Act
        var result = selector.SelectFirst(json);

        // Assert
        result.Should().NotBeNull();
        result.Should().Contain("tag1");
    }

    [Test]
    public void SelectFirst_WithNoMatches_ShouldReturnNull()
    {
        // Arrange
        var selector = new JsonPathSelector("$.nonexistent");
        var json = TestData.SampleJson;

        // Act
        var result = selector.SelectFirst(json);

        // Assert
        result.Should().BeNull();
    }

    [Test]
    public void Select_WithEmptyContent_ShouldReturnEmptyList()
    {
        // Arrange
        var selector = new JsonPathSelector("$.test");

        // Act
        var results = selector.Select(string.Empty);

        // Assert
        results.Should().BeEmpty();
    }

    [Test]
    public void Select_WithInvalidJson_ShouldReturnEmptyList()
    {
        // Arrange
        var selector = new JsonPathSelector("$.test");
        var invalidJson = "{ invalid json }";

        // Act
        var results = selector.Select(invalidJson);

        // Assert
        results.Should().BeEmpty();
    }

    [Test]
    public void Select_WithFilterExpression_ShouldReturnFilteredResults()
    {
        // Arrange
        var selector = new JsonPathSelector("$.data.items[?(@.id > 1)]");
        var json = TestData.SampleNestedJson;

        // Act
        var results = selector.Select(json);

        // Assert
        results.Should().HaveCount(2);
    }

    [Test]
    public async Task Select_WithDeepNesting_ShouldExtractCorrectly()
    {
        // Arrange
        var selector = new JsonPathSelector("$.user.orders[*].orderId");
        var json = await TestData.ReadFixtureAsync("nested-json.json");

        // Act
        var results = selector.Select(json);

        // Assert
        results.Should().HaveCount(2);
        results[0].Should().Contain("ORD-001");
        results[1].Should().Contain("ORD-002");
    }

    [Test]
    public async Task Select_WithRecursiveDescent_ShouldFindAllMatches()
    {
        // Arrange
        var selector = new JsonPathSelector("$..productId");
        var json = await TestData.ReadFixtureAsync("nested-json.json");

        // Act
        var results = selector.Select(json);

        // Assert
        results.Should().NotBeEmpty();
    }

    [Test]
    public void Validate_WithValidPath_ShouldReturnTrue()
    {
        // Arrange
        var selector = new JsonPathSelector("$.data.items[*]");

        // Act
        var isValid = selector.Validate();

        // Assert
        isValid.Should().BeTrue();
    }

    [Test]
    public void Constructor_WithNullExpression_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new JsonPathSelector(null!));
    }

    [Test]
    public void Constructor_WithEmptyExpression_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new JsonPathSelector(""));
    }

    [Test]
    public void Expression_ShouldReturnConstructorValue()
    {
        // Arrange
        var expression = "$.data.items";
        var selector = new JsonPathSelector(expression);

        // Act & Assert
        selector.Expression.Should().Be(expression);
    }

    [Test]
    public void Select_WithRootPath_ShouldReturnWholeDocument()
    {
        // Arrange
        var selector = new JsonPathSelector("$");
        var json = @"{""key"": ""value""}";

        // Act
        var results = selector.Select(json);

        // Assert
        results.Should().HaveCount(1);
        results[0].Should().Contain("key");
        results[0].Should().Contain("value");
    }

    [Test]
    public void Select_WithArrayIndex_ShouldReturnSpecificElement()
    {
        // Arrange
        var selector = new JsonPathSelector("$.tags[1]");
        var json = TestData.SampleJson;

        // Act
        var results = selector.Select(json);

        // Assert
        results.Should().HaveCount(1);
        results[0].Should().Contain("tag2");
    }
}
