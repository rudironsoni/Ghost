using FluentAssertions;
using Ghost.Sdk.Middleware;
using Xunit;
using Ghost.Testing.Reliability;
using Xunit.Abstractions;

namespace Ghost.Sdk.Tests.Middleware;

/// <summary>
/// Unit tests for RedirectOptions configuration.
/// </summary>
[Trait("Category", "Unit")]
public class RedirectOptionsTests : ReliabilityTestBase
{
    public RedirectOptionsTests(ITestOutputHelper output) : base(output) { }
    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        // Act
        var options = new RedirectOptions();

        // Assert
        options.MaxRedirects.Should().Be(10);
        options.AllowCrossScheme.Should().BeFalse();
    }

    [Fact]
    public void MaxRedirects_CanBeCustomized()
    {
        // Arrange
        var options = new RedirectOptions();

        // Act
        options.MaxRedirects = 5;

        // Assert
        options.MaxRedirects.Should().Be(5);
    }

    [Fact]
    public void AllowCrossScheme_CanBeCustomized()
    {
        // Arrange
        var options = new RedirectOptions();

        // Act
        options.AllowCrossScheme = true;

        // Assert
        options.AllowCrossScheme.Should().BeTrue();
    }

    [Fact]
    public void ObjectInitializer_WorksCorrectly()
    {
        // Act
        var options = new RedirectOptions
        {
            MaxRedirects = 20,
            AllowCrossScheme = true
        };

        // Assert
        options.MaxRedirects.Should().Be(20);
        options.AllowCrossScheme.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(50)]
    public void MaxRedirects_AcceptsValidValues(int maxRedirects)
    {
        // Arrange
        var options = new RedirectOptions();

        // Act
        options.MaxRedirects = maxRedirects;

        // Assert
        options.MaxRedirects.Should().Be(maxRedirects);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AllowCrossScheme_AcceptsValidValues(bool allowCrossScheme)
    {
        // Arrange
        var options = new RedirectOptions();

        // Act
        options.AllowCrossScheme = allowCrossScheme;

        // Assert
        options.AllowCrossScheme.Should().Be(allowCrossScheme);
    }
}
