using FluentAssertions;
using Ghost.Sdk.Spider.Adapters.WebSocket;
using Xunit;

namespace Ghost.Sdk.Spider.Tests.Unit.Adapters;

/// <summary>
/// Comprehensive tests for ReconnectionPolicy covering all configuration scenarios.
/// </summary>
public class ReconnectionPolicyTests
{
    [Fact]
    public void Constructor_DefaultValues_ShouldInitializeCorrectly()
    {
        // Arrange & Act
        var policy = new ReconnectionPolicy();

        // Assert
        policy.Enabled.Should().BeTrue();
        policy.MaxAttempts.Should().Be(5);
        policy.InitialDelay.Should().Be(TimeSpan.FromSeconds(1));
        policy.MaxDelay.Should().Be(TimeSpan.FromSeconds(30));
        policy.BackoffMultiplier.Should().Be(2.0);
        policy.UseExponentialBackoff.Should().BeTrue();
        policy.UseJitter.Should().BeTrue();
        policy.ReconnectOnNormalClose.Should().BeFalse();
        policy.ConnectionTimeout.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void Disabled_ShouldCreateDisabledPolicy()
    {
        // Arrange & Act
        var policy = ReconnectionPolicy.Disabled();

        // Assert
        policy.Enabled.Should().BeFalse();
    }

    [Fact]
    public void Default_ShouldCreateDefaultPolicy()
    {
        // Arrange & Act
        var policy = ReconnectionPolicy.Default();

        // Assert
        policy.Enabled.Should().BeTrue();
        policy.MaxAttempts.Should().Be(5);
        policy.UseExponentialBackoff.Should().BeTrue();
    }

    [Fact]
    public void Aggressive_ShouldCreateAggressivePolicy()
    {
        // Arrange & Act
        var policy = ReconnectionPolicy.Aggressive();

        // Assert
        policy.Enabled.Should().BeTrue();
        policy.MaxAttempts.Should().Be(-1); // Unlimited
        policy.InitialDelay.Should().Be(TimeSpan.FromMilliseconds(500));
        policy.MaxDelay.Should().Be(TimeSpan.FromSeconds(10));
        policy.UseExponentialBackoff.Should().BeTrue();
        policy.UseJitter.Should().BeTrue();
    }

    [Fact]
    public void CalculateDelay_WithExponentialBackoff_ShouldIncreaseExponentially()
    {
        // Arrange
        var policy = new ReconnectionPolicy
        {
            InitialDelay = TimeSpan.FromSeconds(1),
            BackoffMultiplier = 2.0,
            MaxDelay = TimeSpan.FromSeconds(60),
            UseExponentialBackoff = true,
            UseJitter = false
        };

        // Act
        var delay0 = policy.CalculateDelay(0);
        var delay1 = policy.CalculateDelay(1);
        var delay2 = policy.CalculateDelay(2);
        var delay3 = policy.CalculateDelay(3);

        // Assert
        delay0.Should().Be(TimeSpan.FromSeconds(1));
        delay1.Should().Be(TimeSpan.FromSeconds(2));
        delay2.Should().Be(TimeSpan.FromSeconds(4));
        delay3.Should().Be(TimeSpan.FromSeconds(8));
    }

    [Fact]
    public void CalculateDelay_WithMaxDelayCap_ShouldNotExceedMaxDelay()
    {
        // Arrange
        var policy = new ReconnectionPolicy
        {
            InitialDelay = TimeSpan.FromSeconds(1),
            BackoffMultiplier = 2.0,
            MaxDelay = TimeSpan.FromSeconds(10),
            UseExponentialBackoff = true,
            UseJitter = false
        };

        // Act
        var delay10 = policy.CalculateDelay(10); // Would be 1024 seconds without cap

        // Assert
        delay10.Should().Be(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void CalculateDelay_WithoutExponentialBackoff_ShouldReturnConstantDelay()
    {
        // Arrange
        var policy = new ReconnectionPolicy
        {
            InitialDelay = TimeSpan.FromSeconds(5),
            UseExponentialBackoff = false,
            UseJitter = false
        };

        // Act
        var delay0 = policy.CalculateDelay(0);
        var delay5 = policy.CalculateDelay(5);
        var delay10 = policy.CalculateDelay(10);

        // Assert
        delay0.Should().Be(TimeSpan.FromSeconds(5));
        delay5.Should().Be(TimeSpan.FromSeconds(5));
        delay10.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void CalculateDelay_WithJitter_ShouldAddVariation()
    {
        // Arrange
        var policy = new ReconnectionPolicy
        {
            InitialDelay = TimeSpan.FromSeconds(10),
            UseExponentialBackoff = false,
            UseJitter = true
        };

        // Act - Calculate multiple times to see variation
        var delays = Enumerable.Range(0, 10).Select(_ => policy.CalculateDelay(0)).ToList();

        // Assert - Should have variation (not all the same)
        var uniqueDelays = delays.Distinct().Count();
        uniqueDelays.Should().BeGreaterThan(1);

        // All delays should be within jitter range (75% - 125% of base)
        foreach (var delay in delays)
        {
            delay.TotalMilliseconds.Should().BeInRange(7500, 12500);
        }
    }

    [Fact]
    public void Validate_WithValidConfiguration_ShouldNotThrow()
    {
        // Arrange
        var policy = new ReconnectionPolicy
        {
            Enabled = true,
            MaxAttempts = 5,
            InitialDelay = TimeSpan.FromSeconds(1),
            MaxDelay = TimeSpan.FromSeconds(30),
            BackoffMultiplier = 2.0,
            ConnectionTimeout = TimeSpan.FromSeconds(30)
        };

        // Act
        Action act = () => policy.Validate();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_WithNegativeMaxAttempts_ShouldThrow()
    {
        // Arrange
        var policy = new ReconnectionPolicy
        {
            Enabled = true,
            MaxAttempts = -5 // Invalid (only -1 for unlimited is allowed)
        };

        // Act
        Action act = () => policy.Validate();

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*MaxAttempts*");
    }

    [Fact]
    public void Validate_WithZeroMaxAttempts_ShouldThrow()
    {
        // Arrange
        var policy = new ReconnectionPolicy
        {
            Enabled = true,
            MaxAttempts = 0
        };

        // Act
        Action act = () => policy.Validate();

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*MaxAttempts*");
    }

    [Fact]
    public void Validate_WithNegativeInitialDelay_ShouldThrow()
    {
        // Arrange
        var policy = new ReconnectionPolicy
        {
            Enabled = true,
            InitialDelay = TimeSpan.FromSeconds(-1)
        };

        // Act
        Action act = () => policy.Validate();

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*InitialDelay*");
    }

    [Fact]
    public void Validate_WithZeroInitialDelay_ShouldThrow()
    {
        // Arrange
        var policy = new ReconnectionPolicy
        {
            Enabled = true,
            InitialDelay = TimeSpan.Zero
        };

        // Act
        Action act = () => policy.Validate();

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*InitialDelay*");
    }

    [Fact]
    public void Validate_WithMaxDelayLessThanInitialDelay_ShouldThrow()
    {
        // Arrange
        var policy = new ReconnectionPolicy
        {
            Enabled = true,
            InitialDelay = TimeSpan.FromSeconds(10),
            MaxDelay = TimeSpan.FromSeconds(5)
        };

        // Act
        Action act = () => policy.Validate();

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*MaxDelay*");
    }

    [Fact]
    public void Validate_WithBackoffMultiplierLessThanOne_ShouldThrow()
    {
        // Arrange
        var policy = new ReconnectionPolicy
        {
            Enabled = true,
            BackoffMultiplier = 0.5
        };

        // Act
        Action act = () => policy.Validate();

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*BackoffMultiplier*");
    }

    [Fact]
    public void Validate_WithBackoffMultiplierEqualToOne_ShouldThrow()
    {
        // Arrange
        var policy = new ReconnectionPolicy
        {
            Enabled = true,
            BackoffMultiplier = 1.0
        };

        // Act
        Action act = () => policy.Validate();

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*BackoffMultiplier*");
    }

    [Fact]
    public void Validate_WithNegativeConnectionTimeout_ShouldThrow()
    {
        // Arrange
        var policy = new ReconnectionPolicy
        {
            Enabled = true,
            ConnectionTimeout = TimeSpan.FromSeconds(-1)
        };

        // Act
        Action act = () => policy.Validate();

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*ConnectionTimeout*");
    }

    [Fact]
    public void Validate_WhenDisabled_ShouldNotValidate()
    {
        // Arrange
        var policy = new ReconnectionPolicy
        {
            Enabled = false,
            MaxAttempts = 0, // Invalid if enabled
            InitialDelay = TimeSpan.Zero // Invalid if enabled
        };

        // Act
        Action act = () => policy.Validate();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_WithUnlimitedAttempts_ShouldNotThrow()
    {
        // Arrange
        var policy = new ReconnectionPolicy
        {
            Enabled = true,
            MaxAttempts = -1 // Unlimited
        };

        // Act
        Action act = () => policy.Validate();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Properties_ShouldBeSettable()
    {
        // Arrange
        var policy = new ReconnectionPolicy();

        // Act
        policy.Enabled = false;
        policy.MaxAttempts = 10;
        policy.InitialDelay = TimeSpan.FromSeconds(2);
        policy.MaxDelay = TimeSpan.FromSeconds(60);
        policy.BackoffMultiplier = 3.0;
        policy.UseExponentialBackoff = false;
        policy.UseJitter = false;
        policy.ReconnectOnNormalClose = true;
        policy.ConnectionTimeout = TimeSpan.FromSeconds(60);

        // Assert
        policy.Enabled.Should().BeFalse();
        policy.MaxAttempts.Should().Be(10);
        policy.InitialDelay.Should().Be(TimeSpan.FromSeconds(2));
        policy.MaxDelay.Should().Be(TimeSpan.FromSeconds(60));
        policy.BackoffMultiplier.Should().Be(3.0);
        policy.UseExponentialBackoff.Should().BeFalse();
        policy.UseJitter.Should().BeFalse();
        policy.ReconnectOnNormalClose.Should().BeTrue();
        policy.ConnectionTimeout.Should().Be(TimeSpan.FromSeconds(60));
    }

    [Fact]
    public void CalculateDelay_WithHighBackoffMultiplier_ShouldGrowRapidly()
    {
        // Arrange
        var policy = new ReconnectionPolicy
        {
            InitialDelay = TimeSpan.FromSeconds(1),
            BackoffMultiplier = 3.0,
            MaxDelay = TimeSpan.FromSeconds(100),
            UseExponentialBackoff = true,
            UseJitter = false
        };

        // Act
        var delay0 = policy.CalculateDelay(0);
        var delay1 = policy.CalculateDelay(1);
        var delay2 = policy.CalculateDelay(2);

        // Assert
        delay0.Should().Be(TimeSpan.FromSeconds(1));
        delay1.Should().Be(TimeSpan.FromSeconds(3));
        delay2.Should().Be(TimeSpan.FromSeconds(9));
    }
}
