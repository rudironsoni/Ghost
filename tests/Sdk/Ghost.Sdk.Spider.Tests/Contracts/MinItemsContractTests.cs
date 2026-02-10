using FluentAssertions;
using Ghost.Sdk.Spider.Contracts;
using Xunit;

namespace Ghost.Sdk.Spider.Tests.Contracts;

/// <summary>
/// Unit tests for <see cref="MinItemsContract"/>.
/// </summary>
public class MinItemsContractTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Name_ReturnsExpectedValue()
    {
        // Arrange
        var contract = new MinItemsContract();

        // Act
        var name = contract.Name;

        // Assert
        name.Should().Be("MinItems");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void MinItems_DefaultValue_Is1()
    {
        // Arrange & Act
        var contract = new MinItemsContract();

        // Assert
        contract.MinItems.Should().Be(1);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ValidateAsync_WithItemCountAboveMin_ReturnsTrue()
    {
        // Arrange
        var contract = new MinItemsContract { MinItems = 10 };
        var context = new SpiderContext
        {
            ItemCount = 15
        };

        // Act
        var result = await contract.ValidateAsync(context);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ValidateAsync_WithItemCountAtMin_ReturnsTrue()
    {
        // Arrange
        var contract = new MinItemsContract { MinItems = 10 };
        var context = new SpiderContext
        {
            ItemCount = 10
        };

        // Act
        var result = await contract.ValidateAsync(context);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ValidateAsync_WithItemCountBelowMin_ReturnsFalse()
    {
        // Arrange
        var contract = new MinItemsContract { MinItems = 10 };
        var context = new SpiderContext
        {
            ItemCount = 5
        };

        // Act
        var result = await contract.ValidateAsync(context);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ValidateAsync_WithZeroItems_ReturnsFalse()
    {
        // Arrange
        var contract = new MinItemsContract { MinItems = 10 };
        var context = new SpiderContext
        {
            ItemCount = 0
        };

        // Act
        var result = await contract.ValidateAsync(context);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ValidateAsync_WithMinItemsSetToZero_ReturnsTrue()
    {
        // Arrange
        var contract = new MinItemsContract { MinItems = 0 };
        var context = new SpiderContext
        {
            ItemCount = 0
        };

        // Act
        var result = await contract.ValidateAsync(context);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ValidateAsync_WithNullContext_ThrowsArgumentNullException()
    {
        // Arrange
        var contract = new MinItemsContract();

        // Act
        var act = async () => await contract.ValidateAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("context");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ValidateAsync_WithCancellationToken_CompletesSuccessfully()
    {
        // Arrange
        var contract = new MinItemsContract { MinItems = 10 };
        var context = new SpiderContext { ItemCount = 15 };
        using var cts = new CancellationTokenSource();

        // Act
        var result = await contract.ValidateAsync(context, cts.Token);

        // Assert
        result.Should().BeTrue();
    }
}
