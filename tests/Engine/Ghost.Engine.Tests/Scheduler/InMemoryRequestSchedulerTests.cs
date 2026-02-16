using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Ghost.Engine.Abstractions.Transport;
using Ghost.Engine.Scheduler;
using Xunit;

namespace Ghost.Engine.Tests.Scheduler;

public class InMemoryRequestSchedulerTests
{
    [Fact]
    public async Task EnqueueAsync_WithPriority_DequeuesInPriorityOrderAsync()
    {
        // Arrange
        var scheduler = new InMemoryRequestScheduler();
        var request1 = new GhostRequest("http://example.com/1", "GET", new Dictionary<string, string>(), null, null);
        var request2 = new GhostRequest("http://example.com/2", "GET", new Dictionary<string, string>(), null, null);
        var request3 = new GhostRequest("http://example.com/3", "GET", new Dictionary<string, string>(), null, null);

        // Act
        await scheduler.EnqueueAsync(request2, priority: 2);
        await scheduler.EnqueueAsync(request1, priority: 1);
        await scheduler.EnqueueAsync(request3, priority: 3);

        // Assert
        GhostRequest? dequeued1 = await scheduler.DequeueAsync();
        GhostRequest? dequeued2 = await scheduler.DequeueAsync();
        GhostRequest? dequeued3 = await scheduler.DequeueAsync();

        dequeued1.Should().Be(request1, "priority 1 should be dequeued first");
        dequeued2.Should().Be(request2, "priority 2 should be dequeued second");
        dequeued3.Should().Be(request3, "priority 3 should be dequeued third");
    }

    [Fact]
    public async Task EnqueueAsync_WithDedupe_SkipsDuplicateRequestsAsync()
    {
        // Arrange
        HashSet<string> seenUrls = [];
        var options = new InMemoryRequestSchedulerOptions
        {
            ShouldSkip = req => !seenUrls.Add(req.Url)
        };
        var scheduler = new InMemoryRequestScheduler(options);
        var request1 = new GhostRequest("http://example.com/1", "GET", new Dictionary<string, string>(), null, null);
        var request2 = new GhostRequest("http://example.com/1", "GET", new Dictionary<string, string>(), null, null); // Duplicate URL
        var request3 = new GhostRequest("http://example.com/2", "GET", new Dictionary<string, string>(), null, null);

        // Act
        await scheduler.EnqueueAsync(request1);
        await scheduler.EnqueueAsync(request2);
        await scheduler.EnqueueAsync(request3);

        // Assert
        int count = await scheduler.CountAsync();
        count.Should().Be(2, "duplicate should be skipped");

        GhostRequest? dequeued1 = await scheduler.DequeueAsync();
        GhostRequest? dequeued2 = await scheduler.DequeueAsync();
        GhostRequest? dequeued3 = await scheduler.DequeueAsync();

        dequeued1.Should().Be(request1);
        dequeued2.Should().Be(request3);
        dequeued3.Should().BeNull("only 2 requests should be enqueued");
    }

    [Fact]
    public async Task DequeueAsync_WithCancellation_ThrowsOperationCanceledExceptionAsync()
    {
        // Arrange
        var scheduler = new InMemoryRequestScheduler();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            scheduler.DequeueAsync(cts.Token).AsTask());
    }

    [Fact]
    public async Task CountAsync_ReturnsCorrectCountAsync()
    {
        // Arrange
        var scheduler = new InMemoryRequestScheduler();
        var request1 = new GhostRequest("http://example.com/1", "GET", new Dictionary<string, string>(), null, null);
        var request2 = new GhostRequest("http://example.com/2", "GET", new Dictionary<string, string>(), null, null);

        // Act
        await scheduler.EnqueueAsync(request1);
        int count1 = await scheduler.CountAsync();

        await scheduler.EnqueueAsync(request2);
        int count2 = await scheduler.CountAsync();

        await scheduler.DequeueAsync();
        int count3 = await scheduler.CountAsync();

        // Assert
        count1.Should().Be(1);
        count2.Should().Be(2);
        count3.Should().Be(1);
    }

    [Fact]
    public async Task DequeueAsync_WhenEmpty_ReturnsNullAsync()
    {
        // Arrange
        var scheduler = new InMemoryRequestScheduler();

        // Act
        GhostRequest? result = await scheduler.DequeueAsync();

        // Assert
        result.Should().BeNull();
    }
}
