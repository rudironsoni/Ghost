using FluentAssertions;
using Ghost.Sdk.Spider.Contracts;
using Xunit;

namespace Ghost.Sdk.Spider.Tests.Contracts;

/// <summary>
/// Unit tests for <see cref="ContractValidationResult"/>.
/// </summary>
public class ContractValidationResultTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Constructor_WithNullResults_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new ContractValidationResult(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Constructor_WithEmptyResults_CreatesValidInstance()
    {
        // Arrange
        var results = Enumerable.Empty<ContractResult>();

        // Act
        var validationResult = new ContractValidationResult(results);

        // Assert
        validationResult.Results.Should().BeEmpty();
        validationResult.TotalCount.Should().Be(0);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Constructor_WithResults_StoresThemAsImmutable()
    {
        // Arrange
        var results = new List<ContractResult>
        {
            new("Contract1", true),
            new("Contract2", false)
        };

        // Act
        var validationResult = new ContractValidationResult(results);

        // Assert
        validationResult.Results.Should().HaveCount(2);
        validationResult.Results.Should().BeAssignableTo<IReadOnlyList<ContractResult>>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AllPassed_WithAllPassingContracts_ReturnsTrue()
    {
        // Arrange
        var results = new List<ContractResult>
        {
            new("Contract1", true),
            new("Contract2", true),
            new("Contract3", true)
        };

        // Act
        var validationResult = new ContractValidationResult(results);

        // Assert
        validationResult.AllPassed.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AllPassed_WithSomeFailingContracts_ReturnsFalse()
    {
        // Arrange
        var results = new List<ContractResult>
        {
            new("Contract1", true),
            new("Contract2", false),
            new("Contract3", true)
        };

        // Act
        var validationResult = new ContractValidationResult(results);

        // Assert
        validationResult.AllPassed.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AllPassed_WithAllFailingContracts_ReturnsFalse()
    {
        // Arrange
        var results = new List<ContractResult>
        {
            new("Contract1", false),
            new("Contract2", false)
        };

        // Act
        var validationResult = new ContractValidationResult(results);

        // Assert
        validationResult.AllPassed.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AllPassed_WithEmptyResults_ReturnsTrue()
    {
        // Arrange
        var results = Enumerable.Empty<ContractResult>();

        // Act
        var validationResult = new ContractValidationResult(results);

        // Assert
        validationResult.AllPassed.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void PassedCount_ReturnsCorrectCount()
    {
        // Arrange
        var results = new List<ContractResult>
        {
            new("Contract1", true),
            new("Contract2", false),
            new("Contract3", true),
            new("Contract4", false)
        };

        // Act
        var validationResult = new ContractValidationResult(results);

        // Assert
        validationResult.PassedCount.Should().Be(2);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void FailedCount_ReturnsCorrectCount()
    {
        // Arrange
        var results = new List<ContractResult>
        {
            new("Contract1", true),
            new("Contract2", false),
            new("Contract3", true),
            new("Contract4", false)
        };

        // Act
        var validationResult = new ContractValidationResult(results);

        // Assert
        validationResult.FailedCount.Should().Be(2);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void TotalCount_ReturnsCorrectCount()
    {
        // Arrange
        var results = new List<ContractResult>
        {
            new("Contract1", true),
            new("Contract2", false),
            new("Contract3", true)
        };

        // Act
        var validationResult = new ContractValidationResult(results);

        // Assert
        validationResult.TotalCount.Should().Be(3);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void FailedContracts_ReturnsOnlyFailedContractNames()
    {
        // Arrange
        var results = new List<ContractResult>
        {
            new("Contract1", true),
            new("Contract2", false),
            new("Contract3", true),
            new("Contract4", false)
        };

        // Act
        var validationResult = new ContractValidationResult(results);

        // Assert
        var expectedFailures = new[] { "Contract2", "Contract4" };
        validationResult.FailedContracts.Should().BeEquivalentTo(expectedFailures);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void FailedContracts_WithAllPassing_ReturnsEmpty()
    {
        // Arrange
        var results = new List<ContractResult>
        {
            new("Contract1", true),
            new("Contract2", true)
        };

        // Act
        var validationResult = new ContractValidationResult(results);

        // Assert
        validationResult.FailedContracts.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void FailedContracts_WithEmptyResults_ReturnsEmpty()
    {
        // Arrange
        var results = Enumerable.Empty<ContractResult>();

        // Act
        var validationResult = new ContractValidationResult(results);

        // Assert
        validationResult.FailedContracts.Should().BeEmpty();
    }
}
