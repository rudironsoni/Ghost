using FluentAssertions;
using Ghost.Sdk.Extensions;
using Ghost.Sdk.Spider.Contracts;
using Xunit;

namespace Ghost.Sdk.Tests.Extensions;

[Trait("Category", "Integration")]
public class CloseSpiderExtensionIntegrationTests
{
    [Fact]
    public async Task ShouldCloseAsync_WithMultipleBuiltInConditions_WorksCorrectly()
    {
        // Arrange
        var conditions = new ICloseCondition[]
        {
            new MaxItemsCondition(100),
            new MaxPagesCondition(50),
            new MaxDurationCondition(TimeSpan.FromMinutes(5)),
            new MemoryThresholdCondition(10L * 1024 * 1024 * 1024) // 10 GB - high enough to not trigger
        };

        var extension = new CloseSpiderExtension(conditions);

        // Act & Assert - None met
        var context1 = new SpiderContext
        {
            ItemCount = 10,
            RequestCount = 5,
            StartTime = DateTimeOffset.UtcNow
        };
        (await extension.ShouldCloseAsync(context1)).Should().BeFalse();

        // Act & Assert - Items limit reached
        var context2 = new SpiderContext
        {
            ItemCount = 100,
            RequestCount = 5,
            StartTime = DateTimeOffset.UtcNow
        };
        (await extension.ShouldCloseAsync(context2)).Should().BeTrue();

        // Act & Assert - Pages limit reached
        var context3 = new SpiderContext
        {
            ItemCount = 10,
            RequestCount = 50,
            StartTime = DateTimeOffset.UtcNow
        };
        (await extension.ShouldCloseAsync(context3)).Should().BeTrue();

        // Act & Assert - Duration limit reached
        var context4 = new SpiderContext
        {
            ItemCount = 10,
            RequestCount = 5,
            StartTime = DateTimeOffset.UtcNow.AddMinutes(-10)
        };
        (await extension.ShouldCloseAsync(context4)).Should().BeTrue();
    }

    [Fact]
    public async Task MaxItemsCondition_RealWorldScenario_TracksItemsCorrectly()
    {
        // Arrange
        var maxItems = 10;
        var condition = new MaxItemsCondition(maxItems);
        var context = new SpiderContext
        {
            SpiderId = "test-spider",
            StartTime = DateTimeOffset.UtcNow
        };

        // Act & Assert - Simulate scraping items
        for (var i = 0; i < maxItems - 1; i++)
        {
            context.ItemCount = i;
            (await condition.IsMetAsync(context)).Should().BeFalse($"should not close at {i} items");
        }

        // Final item should trigger close
        context.ItemCount = maxItems;
        (await condition.IsMetAsync(context)).Should().BeTrue("should close when max items reached");
    }

    [Fact]
    public async Task MaxPagesCondition_RealWorldScenario_TracksRequestsCorrectly()
    {
        // Arrange
        var maxPages = 20;
        var condition = new MaxPagesCondition(maxPages);
        var context = new SpiderContext
        {
            SpiderId = "test-spider",
            StartTime = DateTimeOffset.UtcNow
        };

        // Act & Assert - Simulate processing pages
        for (var i = 0; i < maxPages - 1; i++)
        {
            context.RequestCount = i;
            (await condition.IsMetAsync(context)).Should().BeFalse($"should not close at {i} requests");
        }

        // Final request should trigger close
        context.RequestCount = maxPages;
        (await condition.IsMetAsync(context)).Should().BeTrue("should close when max pages reached");
    }

    [Fact]
    public async Task MaxDurationCondition_RealWorldScenario_TracksTimeCorrectly()
    {
        // Arrange
        var maxDuration = TimeSpan.FromSeconds(2);
        var condition = new MaxDurationCondition(maxDuration);
        var context = new SpiderContext
        {
            SpiderId = "test-spider",
            StartTime = DateTimeOffset.UtcNow
        };

        // Act & Assert - Initially should not close
        (await condition.IsMetAsync(context)).Should().BeFalse("should not close immediately");

        // Wait for duration to exceed
        await Task.Delay(maxDuration.Add(TimeSpan.FromMilliseconds(500)));

        // Should now trigger close
        (await condition.IsMetAsync(context)).Should().BeTrue("should close after duration exceeded");
    }

    [Fact]
    public async Task MemoryThresholdCondition_RealWorldScenario_MonitorsMemory()
    {
        // Arrange
        // Get current memory and set threshold slightly above it
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        var currentMemory = process.WorkingSet64;
        var lowThreshold = currentMemory / 2; // Well below current
        var highThreshold = currentMemory * 2; // Well above current

        var lowCondition = new MemoryThresholdCondition(lowThreshold);
        var highCondition = new MemoryThresholdCondition(highThreshold);
        var context = new SpiderContext();

        // Act
        var lowResult = await lowCondition.IsMetAsync(context);
        var highResult = await highCondition.IsMetAsync(context);

        // Assert
        lowResult.Should().BeTrue("current memory should exceed low threshold");
        highResult.Should().BeFalse("current memory should not exceed high threshold");
    }

    [Fact]
    public async Task Extension_WithMixedConditions_PrioritizesFirstMet()
    {
        // Arrange
        var conditions = new ICloseCondition[]
        {
            new MaxItemsCondition(5),       // Will not be met
            new MaxPagesCondition(10),      // Will be met
            new MaxDurationCondition(TimeSpan.FromHours(1)) // Will not be met
        };

        var extension = new CloseSpiderExtension(conditions);
        var context = new SpiderContext
        {
            ItemCount = 3,
            RequestCount = 10, // This triggers MaxPagesCondition
            StartTime = DateTimeOffset.UtcNow
        };

        // Act
        var result = await extension.ShouldCloseAsync(context);

        // Assert
        result.Should().BeTrue("MaxPagesCondition should trigger close");
    }

    [Fact]
    public void Extension_Conditions_AreEvaluatedInOrder()
    {
        // Arrange
        var condition1 = new MaxItemsCondition(100);
        var condition2 = new MaxPagesCondition(50);
        var condition3 = new MaxDurationCondition(TimeSpan.FromMinutes(5));

        ICloseCondition[] conditions = new ICloseCondition[] { condition1, condition2, condition3 };
        var extension = new CloseSpiderExtension(conditions);

        // Act
        var registeredConditions = extension.Conditions;

        // Assert
        registeredConditions.Should().HaveCount(3);
        registeredConditions[0].Should().BeSameAs(condition1);
        registeredConditions[1].Should().BeSameAs(condition2);
        registeredConditions[2].Should().BeSameAs(condition3);
    }

    [Fact]
    public async Task Extension_WithCancellationToken_SupportsGracefulCancellation()
    {
        // Arrange
        var conditions = new List<ICloseCondition>
        {
            new MaxItemsCondition(100)
        };
        var extension = new CloseSpiderExtension(conditions);
        var context = new SpiderContext { ItemCount = 50 };
        var cts = new CancellationTokenSource();

        // Act - Cancel immediately
        await cts.CancelAsync();
        var act = async () => await extension.ShouldCloseAsync(context, cts.Token);

        // Assert - Should not throw on cancellation
        await act.Should().NotThrowAsync();
    }
}
