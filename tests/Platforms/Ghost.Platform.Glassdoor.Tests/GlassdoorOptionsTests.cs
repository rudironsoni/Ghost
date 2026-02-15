using FluentAssertions;
using Ghost.Models;
using Xunit;

namespace Ghost.Plugin.Glassdoor.Tests;

/// <summary>
/// Unit tests for GlassdoorOptions covering configuration validation and defaults.
/// </summary>
public class GlassdoorOptionsTests
{
    [Fact]
    public void Defaults_AreReasonable()
    {
        // Arrange & Act
        var opts = new GlassdoorOptions();

        // Assert
        opts.Enabled.Should().BeTrue();
        opts.ProxyEnabled.Should().BeFalse();
        opts.Country.Should().Be(CountryCode.US);
        opts.DelayMinMs.Should().Be(500);
        opts.Strategy.Should().Be(JobSearchStrategy.BrowserFirst);
        opts.MaxRetries.Should().Be(4);
        opts.EnableRetryWithJitter.Should().BeTrue();
        opts.RetryBaseDelayMs.Should().Be(1000);
        opts.RetryMaxDelayMs.Should().Be(30000);
        opts.DebugMode.Should().BeFalse();
        opts.RequestTimeoutMs.Should().Be(30000);
        opts.EnableStructuredErrors.Should().BeTrue();
    }

    [Fact]
    public void Enabled_CanBeSet()
    {
        // Arrange
        var opts = new GlassdoorOptions();

        // Act
        opts.Enabled = false;

        // Assert
        opts.Enabled.Should().BeFalse();
    }

    [Fact]
    public void ProxyEnabled_CanBeSet()
    {
        // Arrange
        var opts = new GlassdoorOptions();

        // Act
        opts.ProxyEnabled = true;

        // Assert
        opts.ProxyEnabled.Should().BeTrue();
    }

    [Fact]
    public void Country_CanBeSet()
    {
        // Arrange
        var opts = new GlassdoorOptions();

        // Act
        opts.Country = CountryCode.UK;

        // Assert
        opts.Country.Should().Be(CountryCode.UK);
    }

    [Fact]
    public void DelayMinMs_CanBeSet()
    {
        // Arrange
        var opts = new GlassdoorOptions();

        // Act
        opts.DelayMinMs = 1000;

        // Assert
        opts.DelayMinMs.Should().Be(1000);
    }

    [Fact]
    public void Strategy_CanBeSetToHttpFirst()
    {
        // Arrange
        var opts = new GlassdoorOptions();

        // Act
        opts.Strategy = JobSearchStrategy.HttpFirst;

        // Assert
        opts.Strategy.Should().Be(JobSearchStrategy.HttpFirst);
    }

    [Fact]
    public void Strategy_CanBeSetToBrowserFirst()
    {
        // Arrange
        var opts = new GlassdoorOptions();

        // Act
        opts.Strategy = JobSearchStrategy.BrowserFirst;

        // Assert
        opts.Strategy.Should().Be(JobSearchStrategy.BrowserFirst);
    }

    [Fact]
    public void Strategy_CanBeSetToHttpOnly()
    {
        // Arrange
        var opts = new GlassdoorOptions();

        // Act
        opts.Strategy = JobSearchStrategy.HttpOnly;

        // Assert
        opts.Strategy.Should().Be(JobSearchStrategy.HttpOnly);
    }

    [Fact]
    public void Strategy_CanBeSetToBrowserOnly()
    {
        // Arrange
        var opts = new GlassdoorOptions();

        // Act
        opts.Strategy = JobSearchStrategy.BrowserOnly;

        // Assert
        opts.Strategy.Should().Be(JobSearchStrategy.BrowserOnly);
    }

    [Fact]
    public void MaxRetries_CanBeSet()
    {
        // Arrange
        var opts = new GlassdoorOptions();

        // Act
        opts.MaxRetries = 10;

        // Assert
        opts.MaxRetries.Should().Be(10);
    }

    [Fact]
    public void EnableRetryWithJitter_CanBeSet()
    {
        // Arrange
        var opts = new GlassdoorOptions();

        // Act
        opts.EnableRetryWithJitter = false;

        // Assert
        opts.EnableRetryWithJitter.Should().BeFalse();
    }

    [Fact]
    public void RetryBaseDelayMs_CanBeSet()
    {
        // Arrange
        var opts = new GlassdoorOptions();

        // Act
        opts.RetryBaseDelayMs = 2000;

        // Assert
        opts.RetryBaseDelayMs.Should().Be(2000);
    }

    [Fact]
    public void RetryMaxDelayMs_CanBeSet()
    {
        // Arrange
        var opts = new GlassdoorOptions();

        // Act
        opts.RetryMaxDelayMs = 60000;

        // Assert
        opts.RetryMaxDelayMs.Should().Be(60000);
    }

    [Fact]
    public void DebugMode_CanBeSet()
    {
        // Arrange
        var opts = new GlassdoorOptions();

        // Act
        opts.DebugMode = true;

        // Assert
        opts.DebugMode.Should().BeTrue();
    }

    [Fact]
    public void RequestTimeoutMs_CanBeSet()
    {
        // Arrange
        var opts = new GlassdoorOptions();

        // Act
        opts.RequestTimeoutMs = 60000;

        // Assert
        opts.RequestTimeoutMs.Should().Be(60000);
    }

    [Fact]
    public void EnableStructuredErrors_CanBeSet()
    {
        // Arrange
        var opts = new GlassdoorOptions();

        // Act
        opts.EnableStructuredErrors = false;

        // Assert
        opts.EnableStructuredErrors.Should().BeFalse();
    }

    [Fact]
    public void AllProperties_CanBeSetTogether()
    {
        // Arrange
        var opts = new GlassdoorOptions();

        // Act
        opts.Enabled = false;
        opts.ProxyEnabled = true;
        opts.Country = CountryCode.ES;
        opts.DelayMinMs = 2000;
        opts.Strategy = JobSearchStrategy.HttpOnly;
        opts.MaxRetries = 8;
        opts.EnableRetryWithJitter = false;
        opts.RetryBaseDelayMs = 500;
        opts.RetryMaxDelayMs = 15000;
        opts.DebugMode = true;
        opts.RequestTimeoutMs = 45000;
        opts.EnableStructuredErrors = false;

        // Assert
        opts.Enabled.Should().BeFalse();
        opts.ProxyEnabled.Should().BeTrue();
        opts.Country.Should().Be(CountryCode.ES);
        opts.DelayMinMs.Should().Be(2000);
        opts.Strategy.Should().Be(JobSearchStrategy.HttpOnly);
        opts.MaxRetries.Should().Be(8);
        opts.EnableRetryWithJitter.Should().BeFalse();
        opts.RetryBaseDelayMs.Should().Be(500);
        opts.RetryMaxDelayMs.Should().Be(15000);
        opts.DebugMode.Should().BeTrue();
        opts.RequestTimeoutMs.Should().Be(45000);
        opts.EnableStructuredErrors.Should().BeFalse();
    }

    [Fact]
    public void DelayMinMs_DefaultIsReasonable()
    {
        // Arrange & Act
        var opts = new GlassdoorOptions();

        // Assert
        opts.DelayMinMs.Should().BeGreaterOrEqualTo(100);
        opts.DelayMinMs.Should().BeLessOrEqualTo(5000);
    }

    [Fact]
    public void MaxRetries_DefaultIsReasonable()
    {
        // Arrange & Act
        var opts = new GlassdoorOptions();

        // Assert
        opts.MaxRetries.Should().BeGreaterOrEqualTo(1);
        opts.MaxRetries.Should().BeLessOrEqualTo(10);
    }

    [Fact]
    public void RetryBaseDelayMs_DefaultIsReasonable()
    {
        // Arrange & Act
        var opts = new GlassdoorOptions();

        // Assert
        opts.RetryBaseDelayMs.Should().BeGreaterOrEqualTo(100);
        opts.RetryBaseDelayMs.Should().BeLessOrEqualTo(5000);
    }

    [Fact]
    public void RetryMaxDelayMs_DefaultIsReasonable()
    {
        // Arrange & Act
        var opts = new GlassdoorOptions();

        // Assert
        opts.RetryMaxDelayMs.Should().BeGreaterOrEqualTo(5000);
        opts.RetryMaxDelayMs.Should().BeLessOrEqualTo(120000);
    }

    [Fact]
    public void RequestTimeoutMs_DefaultIsReasonable()
    {
        // Arrange & Act
        var opts = new GlassdoorOptions();

        // Assert
        opts.RequestTimeoutMs.Should().BeGreaterOrEqualTo(5000);
        opts.RequestTimeoutMs.Should().BeLessOrEqualTo(120000);
    }

    [Fact]
    public void Strategy_DefaultIsBrowserFirst()
    {
        // Arrange & Act
        var opts = new GlassdoorOptions();

        // Assert
        opts.Strategy.Should().Be(JobSearchStrategy.BrowserFirst);
    }

    [Fact]
    public void EnableRetryWithJitter_DefaultIsTrue()
    {
        // Arrange & Act
        var opts = new GlassdoorOptions();

        // Assert
        opts.EnableRetryWithJitter.Should().BeTrue();
    }

    [Fact]
    public void EnableStructuredErrors_DefaultIsTrue()
    {
        // Arrange & Act
        var opts = new GlassdoorOptions();

        // Assert
        opts.EnableStructuredErrors.Should().BeTrue();
    }

    [Fact]
    public void DebugMode_DefaultIsFalse()
    {
        // Arrange & Act
        var opts = new GlassdoorOptions();

        // Assert
        opts.DebugMode.Should().BeFalse();
    }

    [Fact]
    public void ProxyEnabled_DefaultIsFalse()
    {
        // Arrange & Act
        var opts = new GlassdoorOptions();

        // Assert
        opts.ProxyEnabled.Should().BeFalse();
    }

    [Fact]
    public void Enabled_DefaultIsTrue()
    {
        // Arrange & Act
        var opts = new GlassdoorOptions();

        // Assert
        opts.Enabled.Should().BeTrue();
    }

    [Fact]
    public void Country_DefaultIsUS()
    {
        // Arrange & Act
        var opts = new GlassdoorOptions();

        // Assert
        opts.Country.Should().Be(CountryCode.US);
    }
}
