using FluentAssertions;
using Ghost.Sdk.Spider.Contracts;
using Xunit;

namespace Ghost.Sdk.Spider.Tests.Contracts;

/// <summary>
/// Unit tests for <see cref="MaxRequestsContract"/>.
/// </summary>
public class MaxRequestsContractTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Name_ReturnsExpectedValue()
    {
        // Arrange
        var contract = new MaxRequestsContract();

        // Act
        var name = contract.Name;

        // Assert
        name.Should().Be("MaxRequests");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void MaxRequests_DefaultValue_Is1000()
    {
        // Arrange & Act
        var contract = new MaxRequestsContract();

        // Assert
        contract.MaxRequests.Should().Be(1000);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ValidateAsync_WithRequestCountBelowMax_ReturnsTrue()
    {
        // Arrange
        var contract = new MaxRequestsContract { MaxRequests = 100 };
        var context = new SpiderContext
        {
            RequestCount = 50
        };

        // Act
        var result = await contract.ValidateAsync(context);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ValidateAsync_WithRequestCountAtMax_ReturnsFalse()
    {
        // Arrange
        var contract = new MaxRequestsContract { MaxRequests = 100 };
        var context = new SpiderContext
        {
            RequestCount = 100
        };

        // Act
        var result = await contract.ValidateAsync(context);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ValidateAsync_WithRequestCountAboveMax_ReturnsFalse()
    {
        // Arrange
        var contract = new MaxRequestsContract { MaxRequests = 100 };
        var context = new SpiderContext
        {
            RequestCount = 150
        };

        // Act
        var result = await contract.ValidateAsync(context);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ValidateAsync_WithZeroRequests_ReturnsTrue()
    {
        // Arrange
        var contract = new MaxRequestsContract { MaxRequests = 100 };
        var context = new SpiderContext
        {
            RequestCount = 0
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
        var contract = new MaxRequestsContract();

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
        var contract = new MaxRequestsContract { MaxRequests = 100 };
        var context = new SpiderContext { RequestCount = 50 };
        using var cts = new CancellationTokenSource();

        // Act
        var result = await contract.ValidateAsync(context, cts.Token);

        // Assert
        result.Should().BeTrue();
    }
}
