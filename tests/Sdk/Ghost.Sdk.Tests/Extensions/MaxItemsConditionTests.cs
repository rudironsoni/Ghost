using FluentAssertions;
using Ghost.Sdk.Extensions;
using Ghost.Sdk.Spider.Contracts;
using Ghost.Testing.Reliability;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Sdk.Tests.Extensions;

[Trait("Category", "Unit")]
public class MaxItemsConditionTests : ReliabilityTestBase
{
    public MaxItemsConditionTests(ITestOutputHelper output) : base(output) { }
    [Fact]
    public async Task IsMetAsync_WhenItemCountBelowMax_ReturnsFalse()
    {
        // Arrange
        var condition = new MaxItemsCondition(100);
        var context = new SpiderContext
        {
            ItemCount = 50
        };

        // Act
        var result = await condition.IsMetAsync(context);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsMetAsync_WhenItemCountEqualsMax_ReturnsTrue()
    {
        // Arrange
        var condition = new MaxItemsCondition(100);
        var context = new SpiderContext
        {
            ItemCount = 100
        };

        // Act
        var result = await condition.IsMetAsync(context);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsMetAsync_WhenItemCountExceedsMax_ReturnsTrue()
    {
        // Arrange
        var condition = new MaxItemsCondition(100);
        var context = new SpiderContext
        {
            ItemCount = 150
        };

        // Act
        var result = await condition.IsMetAsync(context);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithZeroMaxItems_ThrowsArgumentException()
    {
        // Act
        var act = () => new MaxItemsCondition(0);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("maxItems");
    }

    [Fact]
    public void Constructor_WithNegativeMaxItems_ThrowsArgumentException()
    {
        // Act
        var act = () => new MaxItemsCondition(-1);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("maxItems");
    }

    [Fact]
    public async Task IsMetAsync_WithNullContext_ThrowsArgumentNullException()
    {
        // Arrange
        var condition = new MaxItemsCondition(100);

        // Act
        var act = async () => await condition.IsMetAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("context");
    }

    [Fact]
    public void Name_ReturnsDescriptiveName()
    {
        // Arrange
        var condition = new MaxItemsCondition(100);

        // Act
        var name = condition.Name;

        // Assert
        name.Should().Be("MaxItems(100)");
    }

    [Fact]
    public void MaxItems_ReturnsConfiguredValue()
    {
        // Arrange
        var condition = new MaxItemsCondition(100);

        // Act
        var maxItems = condition.MaxItems;

        // Assert
        maxItems.Should().Be(100);
    }
}
