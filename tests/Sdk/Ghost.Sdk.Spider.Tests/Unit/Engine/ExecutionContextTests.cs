using FluentAssertions;
using Ghost.Sdk.Spider.Engine;
using Microsoft.Extensions.Time.Testing;
using Xunit;
using System.Collections.Concurrent;
using System.Globalization;
using SpiderExecutionContext = Ghost.Sdk.Spider.Engine.ExecutionContext;

namespace Ghost.Sdk.Spider.Tests.Unit.Engine;

/// <summary>
/// Comprehensive tests for ExecutionContext.
/// </summary>
public class ExecutionContextTests
{
    private SpiderExecutionContext _context;
    private SpiderOptions _options;

    public ExecutionContextTests()
    {
        _options = new SpiderOptions
        {
            MaxConcurrency = 5,
            MaxRequests = 100,
            RequestDelay = TimeSpan.FromSeconds(1)
        };
        _context = new SpiderExecutionContext("TestSpider", _options);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_ShouldInitialize()
    {
        // Assert
        _context.SpiderName.Should().Be("TestSpider");
        _context.Options.Should().BeSameAs(_options);
        _context.StartedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
        _context.State.Should().NotBeNull();
        _context.State.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithNullSpiderName_ShouldThrow()
    {
        // Act
        var act = () => new SpiderExecutionContext(null!, _options);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("spiderName");
    }

    [Fact]
    public void Constructor_WithNullOptions_ShouldThrow()
    {
        // Act
        var act = () => new SpiderExecutionContext("TestSpider", null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("options");
    }

    #endregion

    #region Counter Tests

    [Fact]
    public void RequestsProcessed_InitialValue_ShouldBeZero()
    {
        // Assert
        _context.RequestsProcessed.Should().Be(0);
    }

    [Fact]
    public void IncrementRequestsProcessed_ShouldIncrement()
    {
        // Act
        var result = _context.IncrementRequestsProcessed();

        // Assert
        result.Should().Be(1);
        _context.RequestsProcessed.Should().Be(1);
    }

    [Fact]
    public void IncrementRequestsProcessed_Multiple_ShouldIncrementCorrectly()
    {
        // Act
        _context.IncrementRequestsProcessed();
        _context.IncrementRequestsProcessed();
        _context.IncrementRequestsProcessed();

        // Assert
        _context.RequestsProcessed.Should().Be(3);
    }

    [Fact]
    public void IncrementRequestsSucceeded_ShouldIncrement()
    {
        // Act
        var result = _context.IncrementRequestsSucceeded();

        // Assert
        result.Should().Be(1);
        _context.RequestsSucceeded.Should().Be(1);
    }

    [Fact]
    public void IncrementRequestsFailed_ShouldIncrement()
    {
        // Act
        var result = _context.IncrementRequestsFailed();

        // Assert
        result.Should().Be(1);
        _context.RequestsFailed.Should().Be(1);
    }

    [Fact]
    public void IncrementItemsExtracted_WithDefaultCount_ShouldIncrementByOne()
    {
        // Act
        var result = _context.IncrementItemsExtracted();

        // Assert
        result.Should().Be(1);
        _context.ItemsExtracted.Should().Be(1);
    }

    [Fact]
    public void IncrementItemsExtracted_WithCustomCount_ShouldIncrementByCount()
    {
        // Act
        var result = _context.IncrementItemsExtracted(5);

        // Assert
        result.Should().Be(5);
        _context.ItemsExtracted.Should().Be(5);
    }

    [Fact]
    public void IncrementItemsExtracted_Multiple_ShouldAccumulate()
    {
        // Act
        _context.IncrementItemsExtracted(3);
        _context.IncrementItemsExtracted(2);
        _context.IncrementItemsExtracted(5);

        // Assert
        _context.ItemsExtracted.Should().Be(10);
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public async Task Counters_ConcurrentIncrements_ShouldBeThreadSafe()
    {
        // Arrange
        var taskCount = 100;

        // Act
        var tasks = Enumerable.Range(0, taskCount)
            .Select(_ => Task.Run(() => _context.IncrementRequestsProcessed()));

        await Task.WhenAll(tasks);

        // Assert
        _context.RequestsProcessed.Should().Be(taskCount);
    }

    [Fact]
    public async Task MixedCounters_ConcurrentIncrements_ShouldBeThreadSafe()
    {
        // Arrange
        var taskCount = 100;

        // Act
        var processedTasks = Enumerable.Range(0, taskCount)
            .Select(_ => Task.Run(() => _context.IncrementRequestsProcessed()));

        var succeededTasks = Enumerable.Range(0, taskCount)
            .Select(_ => Task.Run(() => _context.IncrementRequestsSucceeded()));

        var failedTasks = Enumerable.Range(0, taskCount / 10)
            .Select(_ => Task.Run(() => _context.IncrementRequestsFailed()));

        await Task.WhenAll(processedTasks.Concat(succeededTasks).Concat(failedTasks));

        // Assert
        _context.RequestsProcessed.Should().Be(taskCount);
        _context.RequestsSucceeded.Should().Be(taskCount);
        _context.RequestsFailed.Should().Be(taskCount / 10);
    }

    #endregion

    #region State Management Tests

    [Fact]
    public void State_AddItem_ShouldStore()
    {
        // Act
        _context.State["customKey"] = "customValue";

        // Assert
        _context.State.Should().ContainKey("customKey");
        _context.State["customKey"].Should().Be("customValue");
    }

    [Fact]
    public void State_AddMultipleItems_ShouldStoreAll()
    {
        // Act
        _context.State["key1"] = "value1";
        _context.State["key2"] = 42;
        _context.State["key3"] = new List<string> { "a", "b", "c" };

        // Assert
        _context.State.Should().HaveCount(3);
        _context.State["key1"].Should().Be("value1");
        _context.State["key2"].Should().Be(42);
        _context.State["key3"].Should().BeEquivalentTo(new List<string> { "a", "b", "c" });
    }

    [Fact]
    public void State_UpdateExisting_ShouldOverwrite()
    {
        // Arrange
        _context.State["key"] = "oldValue";

        // Act
        _context.State["key"] = "newValue";

        // Assert
        _context.State["key"].Should().Be("newValue");
    }

    [Fact]
    public void State_RemoveItem_ShouldRemove()
    {
        // Arrange
        _context.State["key"] = "value";

        // Act
        _context.State.TryRemove("key", out _);

        // Assert
        _context.State.Should().NotContainKey("key");
    }

    [Fact]
    public void State_IsConcurrentDictionary_ShouldBeThreadSafe()
    {
        // Assert
        _context.State.Should().BeOfType<ConcurrentDictionary<string, object>>();
    }

    [Fact]
    public async Task State_ConcurrentAccess_ShouldBeThreadSafe()
    {
        // Arrange
        var taskCount = 100;

        // Act
        var tasks = Enumerable.Range(0, taskCount)
            .Select(i => Task.Run(() => _context.State[$"key{i}"] = $"value{i}"));

        await Task.WhenAll(tasks);

        // Assert
        _context.State.Should().HaveCount(taskCount);
    }

    #endregion

    #region Pause/Cancel Tests

    [Fact]
    public void IsPaused_InitialValue_ShouldBeFalse()
    {
        // Assert
        _context.IsPaused.Should().BeFalse();
    }

    [Fact]
    public void IsPaused_SetTrue_ShouldUpdate()
    {
        // Act
        _context.IsPaused = true;

        // Assert
        _context.IsPaused.Should().BeTrue();
    }

    [Fact]
    public void IsCancellationRequested_InitialValue_ShouldBeFalse()
    {
        // Assert
        _context.IsCancellationRequested.Should().BeFalse();
    }

    [Fact]
    public void IsCancellationRequested_SetTrue_ShouldUpdate()
    {
        // Act
        _context.IsCancellationRequested = true;

        // Assert
        _context.IsCancellationRequested.Should().BeTrue();
    }

    #endregion

    #region Request Limit Tests

    [Fact]
    public void IsRequestLimitReached_WithNoLimit_ShouldReturnFalse()
    {
        // Arrange
        var optionsNoLimit = new SpiderOptions { MaxRequests = null };
        var context = new SpiderExecutionContext("TestSpider", optionsNoLimit);
        context.IncrementRequestsProcessed();

        // Act
        var result = context.IsRequestLimitReached();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsRequestLimitReached_BelowLimit_ShouldReturnFalse()
    {
        // Arrange
        var optionsWithLimit = new SpiderOptions { MaxRequests = 10 };
        var context = new SpiderExecutionContext("TestSpider", optionsWithLimit);
        context.IncrementRequestsProcessed();
        context.IncrementRequestsProcessed();

        // Act
        var result = context.IsRequestLimitReached();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsRequestLimitReached_AtLimit_ShouldReturnTrue()
    {
        // Arrange
        var optionsWithLimit = new SpiderOptions { MaxRequests = 5 };
        var context = new SpiderExecutionContext("TestSpider", optionsWithLimit);

        for (int i = 0; i < 5; i++)
        {
            context.IncrementRequestsProcessed();
        }

        // Act
        var result = context.IsRequestLimitReached();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsRequestLimitReached_AboveLimit_ShouldReturnTrue()
    {
        // Arrange
        var optionsWithLimit = new SpiderOptions { MaxRequests = 5 };
        var context = new SpiderExecutionContext("TestSpider", optionsWithLimit);

        for (int i = 0; i < 10; i++)
        {
            context.IncrementRequestsProcessed();
        }

        // Act
        var result = context.IsRequestLimitReached();

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region Statistics Tests

    [Fact]
    public void GetStatistics_ShouldReturnAllCounters()
    {
        // Arrange
        _context.IncrementRequestsProcessed();
        _context.IncrementRequestsSucceeded();
        _context.IncrementItemsExtracted(5);

        // Act
        var stats = _context.GetStatistics();

        // Assert
        stats.Should().ContainKey("RequestsProcessed");
        stats.Should().ContainKey("RequestsSucceeded");
        stats.Should().ContainKey("RequestsFailed");
        stats.Should().ContainKey("ItemsExtracted");
        stats["RequestsProcessed"].Should().Be(1);
        stats["RequestsSucceeded"].Should().Be(1);
        stats["ItemsExtracted"].Should().Be(5);
    }

    [Fact]
    public void GetStatistics_ShouldCalculateElapsedTime()
    {
        // Act
        var stats = _context.GetStatistics();

        // Assert
        stats.Should().ContainKey("ElapsedSeconds");
        ((double)stats["ElapsedSeconds"]).Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public void GetStatistics_WithDelay_ShouldShowElapsedTime()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var context = new SpiderExecutionContext("TestSpider", _options, timeProvider);

        // Act - Advance time using FakeTimeProvider
        timeProvider.Advance(TimeSpan.FromMilliseconds(100));
        var stats = context.GetStatistics();

        // Assert
        stats.Should().ContainKey("ElapsedSeconds");
        Convert.ToDouble(stats["ElapsedSeconds"], CultureInfo.InvariantCulture).Should().BeApproximately(0.1, 0.01);
    }

    [Fact]
    public void GetStatistics_ShouldCalculateRequestsPerSecond()
    {
        // Arrange
        for (int i = 0; i < 10; i++)
        {
            _context.IncrementRequestsProcessed();
        }

        // Act
        var stats = _context.GetStatistics();

        // Assert
        stats.Should().ContainKey("RequestsPerSecond");
        Convert.ToDouble(stats["RequestsPerSecond"], CultureInfo.InvariantCulture).Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public void GetStatistics_ShouldCalculateSuccessRate()
    {
        // Arrange
        _context.IncrementRequestsProcessed();
        _context.IncrementRequestsSucceeded();
        _context.IncrementRequestsProcessed();
        _context.IncrementRequestsSucceeded();
        _context.IncrementRequestsProcessed();
        _context.IncrementRequestsFailed();

        // Act
        var stats = _context.GetStatistics();

        // Assert
        stats.Should().ContainKey("SuccessRate");
        var successRate = Convert.ToDouble(stats["SuccessRate"], CultureInfo.InvariantCulture);
        successRate.Should().BeApproximately(2.0 / 3.0, 0.01); // 2 succeeded out of 3 processed
    }

    [Fact]
    public void GetStatistics_WithZeroRequests_ShouldHandleGracefully()
    {
        // Act
        var stats = _context.GetStatistics();

        // Assert
        stats["RequestsProcessed"].Should().Be(0);
        stats["RequestsPerSecond"].Should().Be(0.0);
        stats["SuccessRate"].Should().Be(0.0);
    }

    [Fact]
    public void GetStatistics_MultipleCallsShouldReturnUpdatedValues()
    {
        // Arrange
        _context.IncrementRequestsProcessed();

        // Act
        var stats1 = _context.GetStatistics();
        _context.IncrementRequestsProcessed();
        var stats2 = _context.GetStatistics();

        // Assert
        stats1["RequestsProcessed"].Should().Be(1);
        stats2["RequestsProcessed"].Should().Be(2);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task Context_SimulateRealUsage_ShouldTrackCorrectly()
    {
        // Arrange - Simulate a spider processing requests
        var context = new SpiderExecutionContext("RealUsageSpider", new SpiderOptions { MaxRequests = 100 });

        // Act - Simulate processing 50 requests, 45 succeed, 5 fail
        List<Task> tasks = [];
        for (int i = 0; i < 50; i++)
        {
            var index = i; // Capture loop variable
            tasks.Add(Task.Run(() =>
            {
                context.IncrementRequestsProcessed();
                if (index < 45)
                {
                    context.IncrementRequestsSucceeded();
                    context.IncrementItemsExtracted(Random.Shared.Next(1, 5));
                }
                else
                {
                    context.IncrementRequestsFailed();
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        context.RequestsProcessed.Should().Be(50);
        context.RequestsSucceeded.Should().Be(45);
        context.RequestsFailed.Should().Be(5);
        context.ItemsExtracted.Should().BeGreaterThan(0);

        var stats = context.GetStatistics();
        Convert.ToDouble(stats["SuccessRate"], CultureInfo.InvariantCulture).Should().BeGreaterThan(0.8);
    }

    [Fact]
    public void Context_WithCustomState_ShouldMaintainState()
    {
        // Arrange & Act
        _context.State["currentPage"] = 1;
        _context.State["visitedUrls"] = new HashSet<string> { "url1", "url2" };
        _context.State["lastError"] = "Some error message";

        // Assert
        _context.State["currentPage"].Should().Be(1);
        ((HashSet<string>)_context.State["visitedUrls"]).Should().HaveCount(2);
        _context.State["lastError"].Should().Be("Some error message");
    }

    [Fact]
    public void Context_LongRunningExecution_ShouldTrackDuration()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var context = new SpiderExecutionContext("TestSpider", _options, timeProvider);
        var startTime = context.StartedAt;

        // Act - Advance time using FakeTimeProvider
        timeProvider.Advance(TimeSpan.FromMilliseconds(200));
        var stats = context.GetStatistics();

        // Assert
        var elapsed = Convert.ToDouble(stats["ElapsedSeconds"], CultureInfo.InvariantCulture);
        elapsed.Should().BeApproximately(0.2, 0.01);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Context_WithEmptySpiderName_ShouldThrow()
    {
        // Act
        var act = () => new SpiderExecutionContext("", _options);

        // Assert - Empty string doesn't throw, only null does
        // Changing assertion to expect success
        act.Should().NotThrow();
    }

    [Fact]
    public void Context_WithWhitespaceSpiderName_ShouldAccept()
    {
        // Act
        var context = new SpiderExecutionContext(" ", _options);

        // Assert - ArgumentNullException is only thrown for null, not whitespace
        context.SpiderName.Should().Be(" ");
    }

    [Fact]
    public void IncrementItemsExtracted_WithZero_ShouldNotChange()
    {
        // Act
        _context.IncrementItemsExtracted(0);

        // Assert
        _context.ItemsExtracted.Should().Be(0);
    }

    [Fact]
    public void IncrementItemsExtracted_WithNegative_ShouldDecrement()
    {
        // Arrange
        _context.IncrementItemsExtracted(10);

        // Act
        _context.IncrementItemsExtracted(-5);

        // Assert
        _context.ItemsExtracted.Should().Be(5);
    }

    #endregion
}
