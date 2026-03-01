using FluentAssertions;
using Ghost.Sdk.Spider.Core.Extraction.Selectors;
using Ghost.Sdk.Spider.Tests.TestHelpers;
using Xunit;
using Ghost.Testing.Reliability;
using Xunit.Abstractions;

namespace Ghost.Sdk.Spider.Tests.Unit.Extraction;

public class JsonPathSelectorTests : ReliabilityTestBase
{
    public JsonPathSelectorTests(ITestOutputHelper output) : base(output) { }
    [Fact]
    public static void Select_WithSimplePath_ShouldReturnValue()
    {
        // Arrange
        var selector = new JsonPathSelector("$.name");
        var json = TestData.SampleJson;

        // Act
        var results = selector.SelectValues(json);

        // Assert
        results.Should().HaveCount(1);
        results[0].Should().Contain("Test Item");
    }

    [Fact]
    public static void Select_WithArrayPath_ShouldReturnArrayItems()
    {
        // Arrange
        var selector = new JsonPathSelector("$.tags[*]");
        var json = TestData.SampleJson;

        // Act
        var results = selector.SelectValues(json);

        // Assert
        results.Should().HaveCount(3);
    }

    [Fact]
    public static void Select_WithNestedPath_ShouldReturnNestedValue()
    {
        // Arrange
        var selector = new JsonPathSelector("$.metadata.created");
        var json = TestData.SampleJson;

        // Act
        var results = selector.SelectValues(json);

        // Assert
        results.Should().HaveCount(1);
        results[0].Should().Contain("2026-01-01");
    }

    [Fact]
    public static async Task Select_WithComplexNesting_ShouldWork()
    {
        // Arrange
        var selector = new JsonPathSelector("$.data.items[*].title");
        var json = await TestData.ReadFixtureAsync("test-json.json");

        // Act
        var results = selector.SelectValues(json);

        // Assert
        results.Should().HaveCount(3);
        results[0].Should().Contain("Item One");
        results[1].Should().Contain("Item Two");
        results[2].Should().Contain("Item Three");
    }

    [Fact]
    public static void SelectFirst_ShouldReturnFirstMatch()
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

    [Fact]
    public static void SelectFirst_WithNoMatches_ShouldReturnNull()
    {
        // Arrange
        var selector = new JsonPathSelector("$.nonexistent");
        var json = TestData.SampleJson;

        // Act
        var result = selector.SelectFirst(json);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public static void Select_WithEmptyContent_ShouldReturnEmptyList()
    {
        // Arrange
        var selector = new JsonPathSelector("$.test");

        // Act
        var results = selector.SelectValues(string.Empty);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public static void Select_WithInvalidJson_ShouldReturnEmptyList()
    {
        // Arrange
        var selector = new JsonPathSelector("$.test");
        var invalidJson = "{ invalid json }";

        // Act
        var results = selector.SelectValues(invalidJson);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public static void Select_WithFilterExpression_ShouldReturnFilteredResults()
    {
        // Arrange
        var selector = new JsonPathSelector("$.data.items[?(@.id > 1)]");
        var json = TestData.SampleNestedJson;

        // Act
        var results = selector.SelectValues(json);

        // Assert
        results.Should().HaveCount(2);
    }

    [Fact]
    public static async Task Select_WithDeepNesting_ShouldExtractCorrectly()
    {
        // Arrange
        var selector = new JsonPathSelector("$.user.orders[*].orderId");
        var json = await TestData.ReadFixtureAsync("nested-json.json");

        // Act
        var results = selector.SelectValues(json);

        // Assert
        results.Should().HaveCount(2);
        results[0].Should().Contain("ORD-001");
        results[1].Should().Contain("ORD-002");
    }

    [Fact]
    public static async Task Select_WithRecursiveDescent_ShouldFindAllMatches()
    {
        // Arrange
        var selector = new JsonPathSelector("$..productId");
        var json = await TestData.ReadFixtureAsync("nested-json.json");

        // Act
        var results = selector.SelectValues(json);

        // Assert
        results.Should().NotBeEmpty();
    }

    [Fact]
    public static void Validate_WithValidPath_ShouldReturnTrue()
    {
        // Arrange
        var selector = new JsonPathSelector("$.data.items[*]");

        // Act
        var isValid = selector.Validate();

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public static void Constructor_WithNullExpression_ShouldThrow()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => new JsonPathSelector(null!));
    }

    [Fact]
    public static void Constructor_WithEmptyExpression_ShouldThrow()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => new JsonPathSelector(""));
    }

    [Fact]
    public static void Expression_ShouldReturnConstructorValue()
    {
        // Arrange
        var expression = "$.data.items";
        var selector = new JsonPathSelector(expression);

        // Act & Assert
        selector.Expression.Should().Be(expression);
    }

    [Fact]
    public static void Select_WithRootPath_ShouldReturnWholeDocument()
    {
        // Arrange
        var selector = new JsonPathSelector("$");
        var json = @"{""key"": ""value""}";

        // Act
        var results = selector.SelectValues(json);

        // Assert
        results.Should().HaveCount(1);
        results[0].Should().Contain("key");
        results[0].Should().Contain("value");
    }

    [Fact]
    public static void Select_WithArrayIndex_ShouldReturnSpecificElement()
    {
        // Arrange
        var selector = new JsonPathSelector("$.tags[1]");
        var json = TestData.SampleJson;

        // Act
        var results = selector.SelectValues(json);

        // Assert
        results.Should().HaveCount(1);
        results[0].Should().Contain("tag2");
    }
}
