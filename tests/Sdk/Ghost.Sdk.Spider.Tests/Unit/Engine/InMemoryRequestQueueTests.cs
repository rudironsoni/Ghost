using FluentAssertions;
using Ghost.Sdk.Spider.Adapters.Contracts;
using Ghost.Sdk.Spider.Engine.Queue;
using Xunit;

namespace Ghost.Sdk.Spider.Tests.Unit.Engine;

public class InMemoryRequestQueueTests
{
    private InMemoryRequestQueue _queue;

    public InMemoryRequestQueueTests()
    {
        _queue = new InMemoryRequestQueue();
    }

    [Fact]
    public void Count_WhenEmpty_ShouldReturnZero()
    {
        // Act & Assert
        _queue.Count.Should().Be(0);
    }

    [Fact]
    public void IsEmpty_WhenEmpty_ShouldReturnTrue()
    {
        // Act & Assert
        _queue.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public async Task EnqueueAsync_WithNullRequest_ShouldThrow()
    {
        // Act
        var act = async () => await _queue.EnqueueAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task EnqueueAsync_WithValidRequest_ShouldIncreaseCount()
    {
        // Arrange
        var request = CreateRequest("https://example.com");

        // Act
        await _queue.EnqueueAsync(request);

        // Assert
        _queue.Count.Should().Be(1);
        _queue.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public async Task EnqueueAsync_WithDuplicateUrl_ShouldNotAddAgain()
    {
        // Arrange
        var request1 = CreateRequest("https://example.com");
        var request2 = CreateRequest("https://example.com");

        // Act
        await _queue.EnqueueAsync(request1);
        await _queue.EnqueueAsync(request2);

        // Assert
        _queue.Count.Should().Be(1);
    }

    [Fact]
    public async Task DequeueAsync_WhenEmpty_ShouldReturnNull()
    {
        // Act
        var result = await _queue.DequeueAsync();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task DequeueAsync_WithOneItem_ShouldReturnAndRemove()
    {
        // Arrange
        var request = CreateRequest("https://example.com");
        await _queue.EnqueueAsync(request);

        // Act
        var result = await _queue.DequeueAsync();

        // Assert
        result.Should().NotBeNull();
        result!.Url.Should().Be("https://example.com");
        _queue.Count.Should().Be(0);
        _queue.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public async Task DequeueAsync_WithMultipleItems_ShouldReturnItemsBasedOnPriority()
    {
        // Arrange - All with same priority (default 0), so order may vary based on internal implementation
        await _queue.EnqueueAsync(CreateRequest("https://example.com/1"), priority: 0);
        await _queue.EnqueueAsync(CreateRequest("https://example.com/2"), priority: 0);
        await _queue.EnqueueAsync(CreateRequest("https://example.com/3"), priority: 0);

        // Act
        var result1 = await _queue.DequeueAsync();
        var result2 = await _queue.DequeueAsync();
        var result3 = await _queue.DequeueAsync();

        // Assert - Just verify all three were dequeued
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        result3.Should().NotBeNull();
        _queue.Count.Should().Be(0);
    }

    [Fact]
    public async Task EnqueueAsync_WithPriority_ShouldRespectPriority()
    {
        // Arrange
        await _queue.EnqueueAsync(CreateRequest("https://example.com/low"), priority: 1);
        await _queue.EnqueueAsync(CreateRequest("https://example.com/high"), priority: 10);
        await _queue.EnqueueAsync(CreateRequest("https://example.com/medium"), priority: 5);

        // Act
        var result1 = await _queue.DequeueAsync();
        var result2 = await _queue.DequeueAsync();
        var result3 = await _queue.DequeueAsync();

        // Assert - Higher priority should come first
        result1!.Url.Should().Be("https://example.com/high");
        result2!.Url.Should().Be("https://example.com/medium");
        result3!.Url.Should().Be("https://example.com/low");
    }

    [Fact]
    public async Task PeekAsync_WhenEmpty_ShouldReturnNull()
    {
        // Act
        var result = await _queue.PeekAsync();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task PeekAsync_ShouldReturnWithoutRemoving()
    {
        // Arrange
        var request = CreateRequest("https://example.com");
        await _queue.EnqueueAsync(request);

        // Act
        var result1 = await _queue.PeekAsync();
        var result2 = await _queue.PeekAsync();

        // Assert
        result1.Should().NotBeNull();
        result1!.Url.Should().Be("https://example.com");
        result2.Should().NotBeNull();
        result2!.Url.Should().Be("https://example.com");
        _queue.Count.Should().Be(1);
    }

    [Fact]
    public async Task ClearAsync_ShouldRemoveAllItems()
    {
        // Arrange
        await _queue.EnqueueAsync(CreateRequest("https://example.com/1"));
        await _queue.EnqueueAsync(CreateRequest("https://example.com/2"));
        await _queue.EnqueueAsync(CreateRequest("https://example.com/3"));

        // Act
        await _queue.ClearAsync();

        // Assert
        _queue.Count.Should().Be(0);
        _queue.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public async Task ClearAsync_ShouldClearSeenUrls()
    {
        // Arrange
        var url = "https://example.com";
        await _queue.EnqueueAsync(CreateRequest(url));
        await _queue.ClearAsync();

        // Act - Should be able to add the same URL again
        await _queue.EnqueueAsync(CreateRequest(url));

        // Assert
        _queue.Count.Should().Be(1);
    }

    [Fact]
    public async Task ContainsAsync_WithNullUrl_ShouldThrow()
    {
        // Act
        var act = async () => await _queue.ContainsAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ContainsAsync_WithSeenUrl_ShouldReturnTrue()
    {
        // Arrange
        var url = "https://example.com";
        await _queue.EnqueueAsync(CreateRequest(url));

        // Act
        var contains = await _queue.ContainsAsync(url);

        // Assert
        contains.Should().BeTrue();
    }

    [Fact]
    public async Task ContainsAsync_WithUnseenUrl_ShouldReturnFalse()
    {
        // Act
        var contains = await _queue.ContainsAsync("https://example.com");

        // Assert
        contains.Should().BeFalse();
    }

    [Fact]
    public async Task ContainsAsync_AfterDequeue_ShouldStillReturnTrue()
    {
        // Arrange
        var url = "https://example.com";
        await _queue.EnqueueAsync(CreateRequest(url));
        await _queue.DequeueAsync();

        // Act
        var contains = await _queue.ContainsAsync(url);

        // Assert
        contains.Should().BeTrue(); // URL stays in seen list
    }

    [Fact]
    public async Task ThreadSafety_ConcurrentEnqueue_ShouldHandleCorrectly()
    {
        // Arrange
        List<Task> tasks = [];
        var baseUrl = "https://example.com/";

        // Act - Enqueue 100 unique URLs concurrently
        for (int i = 0; i < 100; i++)
        {
            var url = baseUrl + i;
            tasks.Add(Task.Run(async () => await _queue.EnqueueAsync(CreateRequest(url))));
        }

        await Task.WhenAll(tasks);

        // Assert
        _queue.Count.Should().Be(100);
    }

    [Fact]
    public async Task ThreadSafety_ConcurrentDequeue_ShouldHandleCorrectly()
    {
        // Arrange
        for (int i = 0; i < 100; i++)
        {
            await _queue.EnqueueAsync(CreateRequest($"https://example.com/{i}"));
        }

        // Act - Dequeue concurrently
        var tasks = new List<Task<Request?>>();
        for (int i = 0; i < 100; i++)
        {
            tasks.Add(Task.Run(async () => await _queue.DequeueAsync()));
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().HaveCount(100);
        results.Where(r => r != null).Should().HaveCount(100);
        _queue.Count.Should().Be(0);
    }

    private static Request CreateRequest(string url)
    {
        return new Request
        {
            RequestId = Guid.NewGuid().ToString(),
            Url = url,
            Method = "GET",
            Timeout = TimeSpan.FromSeconds(30)
        };
    }
}
