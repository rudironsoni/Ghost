using FluentAssertions;
using Ghost.Sdk.Extensions;
using Ghost.Sdk.Spider.Contracts;
using Xunit;

namespace Ghost.Sdk.Tests.Extensions;

[Trait("Category", "Unit")]
public class MaxPagesConditionTests
{
    [Fact]
    public async Task IsMetAsync_WhenRequestCountBelowMax_ReturnsFalse()
    {
        // Arrange
        var condition = new MaxPagesCondition(50);
        var context = new SpiderContext
        {
            RequestCount = 25
        };

        // Act
        var result = await condition.IsMetAsync(context);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsMetAsync_WhenRequestCountEqualsMax_ReturnsTrue()
    {
        // Arrange
        var condition = new MaxPagesCondition(50);
        var context = new SpiderContext
        {
            RequestCount = 50
        };

        // Act
        var result = await condition.IsMetAsync(context);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsMetAsync_WhenRequestCountExceedsMax_ReturnsTrue()
    {
        // Arrange
        var condition = new MaxPagesCondition(50);
        var context = new SpiderContext
        {
            RequestCount = 75
        };

        // Act
        var result = await condition.IsMetAsync(context);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithZeroMaxPages_ThrowsArgumentException()
    {
        // Act
        var act = () => new MaxPagesCondition(0);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("maxPages");
    }

    [Fact]
    public void Constructor_WithNegativeMaxPages_ThrowsArgumentException()
    {
        // Act
        var act = () => new MaxPagesCondition(-1);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("maxPages");
    }

    [Fact]
    public async Task IsMetAsync_WithNullContext_ThrowsArgumentNullException()
    {
        // Arrange
        var condition = new MaxPagesCondition(50);

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
        var condition = new MaxPagesCondition(50);

        // Act
        var name = condition.Name;

        // Assert
        name.Should().Be("MaxPages(50)");
    }

    [Fact]
    public void MaxPages_ReturnsConfiguredValue()
    {
        // Arrange
        var condition = new MaxPagesCondition(50);

        // Act
        var maxPages = condition.MaxPages;

        // Assert
        maxPages.Should().Be(50);
    }
}
