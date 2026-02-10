using FluentAssertions;
using Ghost.Sdk.Spider.Contracts;
using Xunit;

namespace Ghost.Sdk.Spider.Tests.Contracts;

/// <summary>
/// Unit tests for <see cref="ContractResult"/>.
/// </summary>
public class ContractResultTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Constructor_SetsContractNameAndPassed()
    {
        // Arrange & Act
        var result = new ContractResult("TestContract", true);

        // Assert
        result.ContractName.Should().Be("TestContract");
        result.Passed.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Constructor_WithFailedValidation_SetsPassed()
    {
        // Arrange & Act
        var result = new ContractResult("TestContract", false);

        // Assert
        result.ContractName.Should().Be("TestContract");
        result.Passed.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Record_SupportsEqualityComparison()
    {
        // Arrange
        var result1 = new ContractResult("TestContract", true);
        var result2 = new ContractResult("TestContract", true);
        var result3 = new ContractResult("TestContract", false);

        // Act & Assert
        result1.Should().Be(result2);
        result1.Should().NotBe(result3);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Record_SupportsDeconstruction()
    {
        // Arrange
        var result = new ContractResult("TestContract", true);

        // Act
        var (contractName, passed) = result;

        // Assert
        contractName.Should().Be("TestContract");
        passed.Should().BeTrue();
    }
}
