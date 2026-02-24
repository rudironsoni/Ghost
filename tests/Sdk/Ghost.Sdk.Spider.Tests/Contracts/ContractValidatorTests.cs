using FluentAssertions;
using Ghost.Sdk.Spider.Contracts;
using Xunit;

namespace Ghost.Sdk.Spider.Tests.Contracts;

/// <summary>
/// Unit tests for <see cref="ContractValidator"/>.
/// </summary>
public class ContractValidatorTests : ReliabilityTestBase
{
    public ContractValidatorTests(ITestOutputHelper output) : base(output) { }
    [Fact]
    [Trait("Category", "Unit")]
    public void AddContract_WithValidContract_IncreasesContractCount()
    {
        // Arrange
        var validator = new ContractValidator();
        var contract = new MaxRequestsContract();

        // Act
        validator.AddContract(contract);

        // Assert
        validator.ContractCount.Should().Be(1);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AddContract_WithNullContract_ThrowsArgumentNullException()
    {
        // Arrange
        var validator = new ContractValidator();

        // Act
        var act = () => validator.AddContract(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("contract");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AddContract_MultipleContracts_IncreasesContractCount()
    {
        // Arrange
        var validator = new ContractValidator();

        // Act
        validator.AddContract(new MaxRequestsContract());
        validator.AddContract(new MaxDurationContract());
        validator.AddContract(new MinItemsContract());

        // Assert
        validator.ContractCount.Should().Be(3);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ValidateAllAsync_WithNoContracts_ReturnsEmptyResult()
    {
        // Arrange
        var validator = new ContractValidator();
        var context = new SpiderContext();

        // Act
        var result = await validator.ValidateAllAsync(context);

        // Assert
        result.TotalCount.Should().Be(0);
        result.AllPassed.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ValidateAllAsync_WithAllPassingContracts_ReturnsAllPassed()
    {
        // Arrange
        var validator = new ContractValidator();
        validator.AddContract(new MaxRequestsContract { MaxRequests = 100 });
        validator.AddContract(new MaxDurationContract { MaxDuration = TimeSpan.FromMinutes(10) });
        validator.AddContract(new MinItemsContract { MinItems = 5 });

        var context = new SpiderContext
        {
            RequestCount = 50,
            StartTime = DateTimeOffset.UtcNow.AddMinutes(-5),
            ItemCount = 10
        };

        // Act
        var result = await validator.ValidateAllAsync(context);

        // Assert
        result.AllPassed.Should().BeTrue();
        result.PassedCount.Should().Be(3);
        result.FailedCount.Should().Be(0);
        result.TotalCount.Should().Be(3);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ValidateAllAsync_WithSomeFailingContracts_ReturnsCorrectCounts()
    {
        // Arrange
        var validator = new ContractValidator();
        validator.AddContract(new MaxRequestsContract { MaxRequests = 100 });
        validator.AddContract(new MaxDurationContract { MaxDuration = TimeSpan.FromMinutes(10) });
        validator.AddContract(new MinItemsContract { MinItems = 50 });

        var context = new SpiderContext
        {
            RequestCount = 150, // Fails MaxRequests
            StartTime = DateTimeOffset.UtcNow.AddMinutes(-5),
            ItemCount = 10 // Fails MinItems
        };

        // Act
        var result = await validator.ValidateAllAsync(context);

        // Assert
        result.AllPassed.Should().BeFalse();
        result.PassedCount.Should().Be(1);
        result.FailedCount.Should().Be(2);
        result.TotalCount.Should().Be(3);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ValidateAllAsync_WithFailingContracts_ReturnsFailedContractNames()
    {
        // Arrange
        var validator = new ContractValidator();
        validator.AddContract(new MaxRequestsContract { MaxRequests = 100 });
        validator.AddContract(new MinItemsContract { MinItems = 50 });

        var context = new SpiderContext
        {
            RequestCount = 150,
            ItemCount = 10
        };

        // Act
        var result = await validator.ValidateAllAsync(context);

        // Assert
        result.FailedContracts.Should().Contain("MaxRequests");
        result.FailedContracts.Should().Contain("MinItems");
        result.FailedContracts.Should().HaveCount(2);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ValidateAllAsync_WithNullContext_ThrowsArgumentNullException()
    {
        // Arrange
        var validator = new ContractValidator();
        validator.AddContract(new MaxRequestsContract());

        // Act
        var act = async () => await validator.ValidateAllAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("context");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ValidateAllAsync_WithCancellationToken_CompletesSuccessfully()
    {
        // Arrange
        var validator = new ContractValidator();
        validator.AddContract(new MaxRequestsContract { MaxRequests = 100 });
        var context = new SpiderContext { RequestCount = 50 };
        using var cts = new CancellationTokenSource();
using Ghost.Testing.Reliability;
using Xunit.Abstractions;

        // Act
        var result = await validator.ValidateAllAsync(context, cts.Token);

        // Assert
        result.AllPassed.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ValidateAllAsync_ReturnsResultsInOrderAdded()
    {
        // Arrange
        var validator = new ContractValidator();
        validator.AddContract(new MaxRequestsContract { MaxRequests = 100 });
        validator.AddContract(new MaxDurationContract { MaxDuration = TimeSpan.FromMinutes(10) });
        validator.AddContract(new MinItemsContract { MinItems = 5 });

        var context = new SpiderContext
        {
            RequestCount = 50,
            StartTime = DateTimeOffset.UtcNow.AddMinutes(-5),
            ItemCount = 10
        };

        // Act
        var result = await validator.ValidateAllAsync(context);

        // Assert
        result.Results[0].ContractName.Should().Be("MaxRequests");
        result.Results[1].ContractName.Should().Be("MaxDuration");
        result.Results[2].ContractName.Should().Be("MinItems");
    }
}
