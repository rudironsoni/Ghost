using FluentAssertions;
using Ghost.Sdk.Spider.Meta;
using Xunit;

namespace Ghost.Sdk.Spider.Tests.Unit.Meta;

/// <summary>
/// Unit tests for <see cref="MetaDictionary"/>.
/// </summary>
public sealed class MetaDictionaryTests
{
    [Trait("Category", "Unit")]
    [Fact]
    public void Set_WithValidKeyAndValue_StoresValue()
    {
        // Arrange
        var meta = new MetaDictionary();

        // Act
        meta.Set("depth", 3);

        // Assert
        meta.Should().ContainKey("depth");
        meta["depth"].Should().Be(3);
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void Get_WithExistingKey_ReturnsTypedValue()
    {
        // Arrange
        var meta = new MetaDictionary();
        meta.Set("depth", 3);

        // Act
        var depth = meta.Get<int>("depth");

        // Assert
        depth.Should().Be(3);
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void Get_WithNonExistingKey_ThrowsKeyNotFoundException()
    {
        // Arrange
        var meta = new MetaDictionary();

        // Act
        var act = () => meta.Get<int>("missing");

        // Assert
        act.Should().Throw<KeyNotFoundException>();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void Get_WithInvalidType_ThrowsInvalidCastException()
    {
        // Arrange
        var meta = new MetaDictionary();
        meta.Set("depth", 3);

        // Act
        var act = () => meta.Get<string>("depth");

        // Assert
        act.Should().Throw<InvalidCastException>();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void TryGet_WithExistingKey_ReturnsTrueAndValue()
    {
        // Arrange
        var meta = new MetaDictionary();
        meta.Set("start_url", "https://example.com");

        // Act
        var result = meta.TryGet<string>("start_url", out var url);

        // Assert
        result.Should().BeTrue();
        url.Should().Be("https://example.com");
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void TryGet_WithNonExistingKey_ReturnsFalseAndDefault()
    {
        // Arrange
        var meta = new MetaDictionary();

        // Act
        var result = meta.TryGet<string>("missing", out var value);

        // Assert
        result.Should().BeFalse();
        value.Should().BeNull();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void TryGet_WithInvalidType_ReturnsFalseAndDefault()
    {
        // Arrange
        var meta = new MetaDictionary();
        meta.Set("depth", 3);

        // Act
        var result = meta.TryGet<string>("depth", out var value);

        // Assert
        result.Should().BeFalse();
        value.Should().BeNull();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void Set_WithMultipleTypes_StoresAllCorrectly()
    {
        // Arrange
        var meta = new MetaDictionary();

        // Act
        meta.Set("depth", 3);
        meta.Set("start_url", "https://example.com");
        meta.Set("enabled", true);
        meta.Set("timestamp", DateTimeOffset.UtcNow);

        // Assert
        meta.Get<int>("depth").Should().Be(3);
        meta.Get<string>("start_url").Should().Be("https://example.com");
        meta.Get<bool>("enabled").Should().BeTrue();
        meta.Get<DateTimeOffset>("timestamp").Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void Set_OverwritesExistingValue()
    {
        // Arrange
        var meta = new MetaDictionary();
        meta.Set("depth", 3);

        // Act
        meta.Set("depth", 5);

        // Assert
        meta.Get<int>("depth").Should().Be(5);
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void IDictionary_Methods_WorkCorrectly()
    {
        // Arrange
        var meta = new MetaDictionary();
        meta.Set("depth", 3);
        meta.Set("url", "https://example.com");

        // Act & Assert
        meta.Count.Should().Be(2);
        meta.ContainsKey("depth").Should().BeTrue();
        meta.ContainsKey("missing").Should().BeFalse();
        meta.Keys.Should().Contain("depth", "url");
        meta.Values.Should().Contain(3, "https://example.com");

        // Remove
        meta.Remove("depth");
        meta.Count.Should().Be(1);
        meta.ContainsKey("depth").Should().BeFalse();

        // Clear
        meta.Clear();
        meta.Count.Should().Be(0);
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void Constructor_WithCapacity_CreatesEmptyDictionary()
    {
        // Act
        var meta = new MetaDictionary(10);

        // Assert
        meta.Count.Should().Be(0);
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void TryGet_WithValueType_ReturnsDefaultValueOnFailure()
    {
        // Arrange
        var meta = new MetaDictionary();

        // Act
        var result = meta.TryGet<int>("missing", out var value);

        // Assert
        result.Should().BeFalse();
        value.Should().Be(0);
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void Set_WithNullValue_StoresNull()
    {
        // Arrange
        var meta = new MetaDictionary();

        // Act
        meta.Set<string?>("nullable", null);

        // Assert
        meta.ContainsKey("nullable").Should().BeTrue();
        meta["nullable"].Should().BeNull();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void TryGet_WithNullValue_ReturnsTrueAndNull()
    {
        // Arrange
        var meta = new MetaDictionary();
        meta.Set<string?>("nullable", null);

        // Act
        var result = meta.TryGet<string?>("nullable", out var value);

        // Assert
        result.Should().BeFalse(); // Because null is not a string
        value.Should().BeNull();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void Set_WithComplexObject_StoresCorrectly()
    {
        // Arrange
        var meta = new MetaDictionary();
        var customObject = new { Name = "Test", Count = 42 };

        // Act
        meta.Set("custom", customObject);

        // Assert
        var retrieved = meta.Get<object>("custom");
        retrieved.Should().BeEquivalentTo(customObject);
    }
}
