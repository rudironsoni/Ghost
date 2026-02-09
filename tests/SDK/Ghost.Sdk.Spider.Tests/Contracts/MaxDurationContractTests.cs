using FluentAssertions;
using Ghost.Sdk.Spider.Contracts;
using Xunit;

namespace Ghost.Sdk.Spider.Tests.Contracts;

/// <summary>
/// Unit tests for <see cref="MaxDurationContract"/>.
/// </summary>
public class MaxDurationContractTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Name_ReturnsExpectedValue()
    {
        // Arrange
        var contract = new MaxDurationContract();

        // Act
        var name = contract.Name;

        // Assert
        name.Should().Be("MaxDuration");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void MaxDuration_DefaultValue_IsOneHour()
    {
        // Arrange & Act
        var contract = new MaxDurationContract();

        // Assert
        contract.MaxDuration.Should().Be(TimeSpan.FromHours(1));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ValidateAsync_WithDurationBelowMax_ReturnsTrue()
    {
        // Arrange
        var contract = new MaxDurationContract { MaxDuration = TimeSpan.FromMinutes(10) };
        var context = new SpiderContext
        {
            StartTime = DateTimeOffset.UtcNow.AddMinutes(-5)
        };

        // Act
        var result = await contract.ValidateAsync(context);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ValidateAsync_WithDurationAboveMax_ReturnsFalse()
    {
        // Arrange
        var contract = new MaxDurationContract { MaxDuration = TimeSpan.FromMinutes(10) };
        var context = new SpiderContext
        {
            StartTime = DateTimeOffset.UtcNow.AddMinutes(-15)
        };

        // Act
        var result = await contract.ValidateAsync(context);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ValidateAsync_WithDurationAtMax_ReturnsFalse()
    {
        // Arrange
        var contract = new MaxDurationContract { MaxDuration = TimeSpan.FromMinutes(10) };
        var context = new SpiderContext
        {
            StartTime = DateTimeOffset.UtcNow.AddMinutes(-10)
        };

        // Act
        var result = await contract.ValidateAsync(context);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ValidateAsync_WithZeroDuration_ReturnsTrue()
    {
        // Arrange
        var contract = new MaxDurationContract { MaxDuration = TimeSpan.FromMinutes(10) };
        var context = new SpiderContext
        {
            StartTime = DateTimeOffset.UtcNow
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
        var contract = new MaxDurationContract();

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
        var contract = new MaxDurationContract { MaxDuration = TimeSpan.FromMinutes(10) };
        var context = new SpiderContext { StartTime = DateTimeOffset.UtcNow.AddMinutes(-5) };
        using var cts = new CancellationTokenSource();

        // Act
        var result = await contract.ValidateAsync(context, cts.Token);

        // Assert
        result.Should().BeTrue();
    }
}
