using FluentAssertions;
using Ghost.Sdk.Spider.Strategies;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Ghost.Sdk.Spider.Tests.Unit.Strategies;

[TestFixture]
public class StrategyRouterTests
{
    private StrategyRouter _router = null!;

    [SetUp]
    public void Setup()
    {
        _router = new StrategyRouter(NullLogger<StrategyRouter>.Instance);
    }

    [Test]
    public void RegisterStrategy_WithValidStrategy_ShouldRegister()
    {
        // Arrange
        Func<StrategyContext, CancellationToken, Task<ExtractionResult>> strategy =
            (ctx, ct) => Task.FromResult(ExtractionResult.CreateSuccess(new { test = "data" }, "TestStrategy", TimeSpan.Zero));

        // Act
        _router.RegisterStrategy("TestStrategy", strategy);

        // Assert
        var metrics = _router.GetMetrics();
        metrics.Should().ContainKey("TestStrategy");
    }

    [Test]
    public void RegisterStrategy_WithNullName_ShouldThrow()
    {
        // Arrange
        Func<StrategyContext, CancellationToken, Task<ExtractionResult>> strategy =
            (ctx, ct) => Task.FromResult(ExtractionResult.CreateSuccess(new { }, "Test", TimeSpan.Zero));

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _router.RegisterStrategy(null!, strategy));
    }

    [Test]
    public void RegisterStrategy_WithNullStrategy_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _router.RegisterStrategy("Test", null!));
    }

    [Test]
    public async Task ExecuteAsync_WithSuccessfulStrategy_ShouldReturnSuccess()
    {
        // Arrange
        var expectedData = new { value = 123 };
        _router.RegisterStrategy("SuccessStrategy", (ctx, ct) =>
            Task.FromResult(ExtractionResult.CreateSuccess(expectedData, "SuccessStrategy", TimeSpan.FromMilliseconds(100))));

        var context = new StrategyContext
        {
            Url = "https://example.com",
            Content = "test content",
            ContentType = "text/html"
        };

        // Act
        var result = await _router.ExecuteAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().Be(expectedData);
    }

    [Test]
    public async Task ExecuteAsync_WithFailingStrategy_ShouldTryNextStrategy()
    {
        // Arrange
        _router.RegisterStrategy("FailStrategy", (ctx, ct) =>
            Task.FromResult(ExtractionResult.CreateFailure("Failed", "FailStrategy", TimeSpan.Zero)));

        _router.RegisterStrategy("SuccessStrategy", (ctx, ct) =>
            Task.FromResult(ExtractionResult.CreateSuccess(new { value = 42 }, "SuccessStrategy", TimeSpan.Zero)));

        var context = new StrategyContext
        {
            Url = "https://example.com",
            Content = "test content",
            ContentType = "text/html"
        };

        // Act
        var result = await _router.ExecuteAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.StrategyName.Should().Be("SuccessStrategy");
    }

    [Test]
    public async Task ExecuteAsync_WithAllStrategiesFailing_ShouldReturnFailure()
    {
        // Arrange
        _router.RegisterStrategy("Fail1", (ctx, ct) =>
            Task.FromResult(ExtractionResult.CreateFailure("Error 1", "Fail1", TimeSpan.Zero)));

        _router.RegisterStrategy("Fail2", (ctx, ct) =>
            Task.FromResult(ExtractionResult.CreateFailure("Error 2", "Fail2", TimeSpan.Zero)));

        var context = new StrategyContext
        {
            Url = "https://example.com",
            Content = "test content",
            ContentType = "text/html"
        };

        // Act
        var result = await _router.ExecuteAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("All strategies failed");
    }

    [Test]
    public async Task ExecuteStrategyAsync_WithValidName_ShouldExecuteStrategy()
    {
        // Arrange
        var expectedData = new { result = "test" };
        _router.RegisterStrategy("SpecificStrategy", (ctx, ct) =>
            Task.FromResult(ExtractionResult.CreateSuccess(expectedData, "SpecificStrategy", TimeSpan.Zero)));

        var context = new StrategyContext
        {
            Url = "https://example.com",
            Content = "test",
            ContentType = "text/html"
        };

        // Act
        var result = await _router.ExecuteStrategyAsync("SpecificStrategy", context);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().Be(expectedData);
    }

    [Test]
    public void ExecuteStrategyAsync_WithInvalidName_ShouldThrow()
    {
        // Arrange
        var context = new StrategyContext
        {
            Url = "https://example.com",
            Content = "test",
            ContentType = "text/html"
        };

        // Act & Assert
        Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _router.ExecuteStrategyAsync("NonExistent", context));
    }

    [Test]
    public async Task ExecuteChainAsync_WithSuccessfulStrategies_ShouldExecuteAll()
    {
        // Arrange
        _router.RegisterStrategy("Strategy1", (ctx, ct) =>
            Task.FromResult(ExtractionResult.CreateSuccess(new { step = 1 }, "Strategy1", TimeSpan.Zero)));

        _router.RegisterStrategy("Strategy2", (ctx, ct) =>
            Task.FromResult(ExtractionResult.CreateSuccess(new { step = 2 }, "Strategy2", TimeSpan.Zero)));

        var chain = new StrategyChain
        {
            Name = "TestChain",
            Strategies = new List<StrategyConfiguration>
            {
                new() { Name = "Strategy1" },
                new() { Name = "Strategy2" }
            },
            StopOnFailure = false
        };

        var context = new StrategyContext
        {
            Url = "https://example.com",
            Content = "test",
            ContentType = "text/html"
        };

        // Act
        var result = await _router.ExecuteChainAsync(chain, context);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().BeOfType<Dictionary<string, object>>();
        ((Dictionary<string, object>)result.Data!).Should().HaveCount(2);
    }

    [Test]
    public async Task ExecuteChainAsync_WithStopOnFailure_ShouldStopOnFirstFailure()
    {
        // Arrange
        _router.RegisterStrategy("SuccessStrategy", (ctx, ct) =>
            Task.FromResult(ExtractionResult.CreateSuccess(new { step = 1 }, "SuccessStrategy", TimeSpan.Zero)));

        _router.RegisterStrategy("FailStrategy", (ctx, ct) =>
            Task.FromResult(ExtractionResult.CreateFailure("Error", "FailStrategy", TimeSpan.Zero)));

        _router.RegisterStrategy("NeverExecuted", (ctx, ct) =>
            Task.FromResult(ExtractionResult.CreateSuccess(new { step = 3 }, "NeverExecuted", TimeSpan.Zero)));

        var chain = new StrategyChain
        {
            Name = "TestChain",
            Strategies = new List<StrategyConfiguration>
            {
                new() { Name = "SuccessStrategy" },
                new() { Name = "FailStrategy" },
                new() { Name = "NeverExecuted" }
            },
            StopOnFailure = true
        };

        var context = new StrategyContext
        {
            Url = "https://example.com",
            Content = "test",
            ContentType = "text/html"
        };

        // Act
        var result = await _router.ExecuteChainAsync(chain, context);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Metadata.Should().ContainKey("StrategiesExecuted");
        ((int)result.Metadata!["StrategiesExecuted"]).Should().Be(2); // Only first two executed
    }

    [Test]
    public void GetMetrics_ShouldReturnRegisteredStrategies()
    {
        // Arrange
        _router.RegisterStrategy("Strategy1", (ctx, ct) =>
            Task.FromResult(ExtractionResult.CreateSuccess(new { }, "Strategy1", TimeSpan.Zero)));
        _router.RegisterStrategy("Strategy2", (ctx, ct) =>
            Task.FromResult(ExtractionResult.CreateSuccess(new { }, "Strategy2", TimeSpan.Zero)));

        // Act
        var metrics = _router.GetMetrics();

        // Assert
        metrics.Should().HaveCount(2);
        metrics.Should().ContainKey("Strategy1");
        metrics.Should().ContainKey("Strategy2");
    }

    [Test]
    public async Task GetMetrics_AfterExecution_ShouldUpdateMetrics()
    {
        // Arrange
        _router.RegisterStrategy("TestStrategy", (ctx, ct) =>
            Task.FromResult(ExtractionResult.CreateSuccess(new { }, "TestStrategy", TimeSpan.FromMilliseconds(100))));

        var context = new StrategyContext
        {
            Url = "https://example.com",
            Content = "test",
            ContentType = "text/html"
        };

        // Act
        await _router.ExecuteAsync(context);
        var metrics = _router.GetMetrics();

        // Assert
        metrics["TestStrategy"].SuccessCount.Should().Be(1);
    }

    [Test]
    public void ResetMetrics_ShouldClearMetrics()
    {
        // Arrange
        _router.RegisterStrategy("TestStrategy", (ctx, ct) =>
            Task.FromResult(ExtractionResult.CreateSuccess(new { }, "TestStrategy", TimeSpan.Zero)));

        var context = new StrategyContext
        {
            Url = "https://example.com",
            Content = "test",
            ContentType = "text/html"
        };

        _router.ExecuteAsync(context).Wait();

        // Act
        _router.ResetMetrics();
        var metrics = _router.GetMetrics();

        // Assert
        metrics["TestStrategy"].SuccessCount.Should().Be(0);
    }
}
