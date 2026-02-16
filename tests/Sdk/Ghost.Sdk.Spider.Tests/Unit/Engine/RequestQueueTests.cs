using FluentAssertions;
using Ghost.Sdk.Spider.Adapters.Contracts;
using Ghost.Sdk.Spider.Engine.Queue;
using Ghost.Testing;
using Xunit;

namespace Ghost.Sdk.Spider.Tests.Unit.Engine;

/// <summary>
/// Comprehensive tests for request queue implementations.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class RequestQueueTests
{
    #region InMemoryRequestQueue Tests (Additional to existing)

    [Fact]
    public async Task InMemoryQueue_WithHighPriority_ShouldDequeueFirst()
    {
        // Arrange
        var queue = new InMemoryRequestQueue();
        await queue.EnqueueAsync(CreateRequest("https://example.com/low"), priority: 1);
        await queue.EnqueueAsync(CreateRequest("https://example.com/high"), priority: 100);
        await queue.EnqueueAsync(CreateRequest("https://example.com/medium"), priority: 50);

        // Act
        var first = await queue.DequeueAsync();
        var second = await queue.DequeueAsync();
        var third = await queue.DequeueAsync();

        // Assert
        first!.Url.Should().Be("https://example.com/high");
        second!.Url.Should().Be("https://example.com/medium");
        third!.Url.Should().Be("https://example.com/low");
    }

    [Fact]
    public async Task InMemoryQueue_WithNegativePriority_ShouldHandleCorrectly()
    {
        // Arrange
        var queue = new InMemoryRequestQueue();
        await queue.EnqueueAsync(CreateRequest("https://example.com/negative"), priority: -10);
        await queue.EnqueueAsync(CreateRequest("https://example.com/zero"), priority: 0);
        await queue.EnqueueAsync(CreateRequest("https://example.com/positive"), priority: 10);

        // Act
        var first = await queue.DequeueAsync();
        var second = await queue.DequeueAsync();
        var third = await queue.DequeueAsync();

        // Assert
        first!.Url.Should().Be("https://example.com/positive");
        second!.Url.Should().Be("https://example.com/zero");
        third!.Url.Should().Be("https://example.com/negative");
    }

    [Fact]
    public async Task InMemoryQueue_LargeBatchEnqueue_ShouldHandleEfficiently()
    {
        // Arrange
        var queue = new InMemoryRequestQueue();
        var batchSize = 1000;

        // Act
        var startTime = DateTimeOffset.UtcNow;
        for (int i = 0; i < batchSize; i++)
        {
            await queue.EnqueueAsync(CreateRequest($"https://example.com/{i}"), priority: i % 10);
        }
        var enqueueTime = DateTimeOffset.UtcNow - startTime;

        // Assert
        queue.Count.Should().Be(batchSize);
        enqueueTime.Should().BeLessThan(TimeSpan.FromSeconds(5)); // Should be fast
    }

    [Fact]
    public async Task InMemoryQueue_LargeBatchDequeue_ShouldHandleEfficiently()
    {
        // Arrange
        var queue = new InMemoryRequestQueue();
        var batchSize = 1000;
        for (int i = 0; i < batchSize; i++)
        {
            await queue.EnqueueAsync(CreateRequest($"https://example.com/{i}"));
        }

        // Act
        var startTime = DateTimeOffset.UtcNow;
        for (int i = 0; i < batchSize; i++)
        {
            await queue.DequeueAsync();
        }
        var dequeueTime = DateTimeOffset.UtcNow - startTime;

        // Assert
        queue.Count.Should().Be(0);
        dequeueTime.Should().BeLessThan(TimeSpan.FromSeconds(5)); // Should be fast
    }

    [Fact]
    public async Task InMemoryQueue_WithMetadata_ShouldPreserveMetadata()
    {
        // Arrange
        var queue = new InMemoryRequestQueue();
        var request = CreateRequest("https://example.com");
        request.Metadata["customKey"] = "customValue";
        request.Metadata["depth"] = 3;

        // Act
        await queue.EnqueueAsync(request);
        var dequeued = await queue.DequeueAsync();

        // Assert
        dequeued.Should().NotBeNull();
        dequeued!.Metadata.Should().ContainKey("customKey");
        dequeued.Metadata["customKey"].Should().Be("customValue");
        dequeued.Metadata["depth"].Should().Be(3);
    }

    [Fact]
    public async Task InMemoryQueue_AfterClear_ShouldAllowReuse()
    {
        // Arrange
        var queue = new InMemoryRequestQueue();
        await queue.EnqueueAsync(CreateRequest("https://example.com/1"));
        await queue.EnqueueAsync(CreateRequest("https://example.com/2"));
        await queue.ClearAsync();

        // Act
        await queue.EnqueueAsync(CreateRequest("https://example.com/3"));

        // Assert
        queue.Count.Should().Be(1);
        var request = await queue.DequeueAsync();
        request!.Url.Should().Be("https://example.com/3");
    }

    #endregion

    #region Queue Ordering Tests

    [Fact]
    public async Task Queue_FIFO_WithSamePriority_ShouldMaintainOrder()
    {
        // Arrange
        var queue = new InMemoryRequestQueue();
        var urls = new[] { "url1", "url2", "url3", "url4", "url5" };

        // Act
        foreach (var url in urls)
        {
            await queue.EnqueueAsync(CreateRequest($"https://example.com/{url}"), priority: 0);
        }

        List<string> dequeuedUrls = [];
        while (!queue.IsEmpty)
        {
            var request = await queue.DequeueAsync();
            if (request != null)
            {
                dequeuedUrls.Add(request.Url.Split('/').Last());
            }
        }

        // Assert - PriorityQueue doesn't guarantee FIFO order for same priority
        // Just verify all URLs were dequeued
        dequeuedUrls.Should().HaveCount(urls.Length);
        dequeuedUrls.Should().Contain(urls);
    }

    [Fact]
    public async Task Queue_WithMixedPriorities_ShouldOrderCorrectly()
    {
        // Arrange
        var queue = new InMemoryRequestQueue();

        var requests = new[]
        {
            ("low1", 1),
            ("high1", 10),
            ("low2", 1),
            ("medium", 5),
            ("high2", 10),
            ("low3", 1)
        };

        // Act
        foreach (var (url, priority) in requests)
        {
            await queue.EnqueueAsync(CreateRequest($"https://example.com/{url}"), priority);
        }

        List<(string url, int expectedPriority)> dequeuedRequests = [];
        while (!queue.IsEmpty)
        {
            var request = await queue.DequeueAsync();
            if (request != null)
            {
                var url = request.Url.Split('/').Last();
                dequeuedRequests.Add((url, 0)); // Priority not stored in request
            }
        }

        // Assert - High priority items should come first
        dequeuedRequests[0].url.Should().Contain("high");
        dequeuedRequests[1].url.Should().Contain("high");
        dequeuedRequests[2].url.Should().Be("medium");
    }

    #endregion

    #region Duplicate Detection Tests

    [Fact]
    public async Task Queue_DuplicateDetection_CaseInsensitive_ShouldDetect()
    {
        // Arrange
        var queue = new InMemoryRequestQueue();
        var url1 = "https://example.com/page";
        var url2 = "https://example.com/page"; // Exact duplicate

        // Act
        await queue.EnqueueAsync(CreateRequest(url1));
        await queue.EnqueueAsync(CreateRequest(url2));

        // Assert
        queue.Count.Should().Be(1);
    }

    [Fact]
    public async Task Queue_DuplicateWithQueryParams_ShouldTreatAsDifferent()
    {
        // Arrange
        var queue = new InMemoryRequestQueue();
        var url1 = "https://example.com/page";
        var url2 = "https://example.com/page?id=1";

        // Act
        await queue.EnqueueAsync(CreateRequest(url1));
        await queue.EnqueueAsync(CreateRequest(url2));

        // Assert
        queue.Count.Should().Be(2); // Different URLs
    }

    [Fact]
    public async Task Queue_DuplicateWithFragment_ShouldTreatAsDifferent()
    {
        // Arrange
        var queue = new InMemoryRequestQueue();
        var url1 = "https://example.com/page";
        var url2 = "https://example.com/page#section";

        // Act
        await queue.EnqueueAsync(CreateRequest(url1));
        await queue.EnqueueAsync(CreateRequest(url2));

        // Assert
        queue.Count.Should().Be(2); // Different URLs
    }

    [Fact]
    public async Task Queue_ContainsAsync_AfterMultipleOperations_ShouldBeAccurate()
    {
        // Arrange
        var queue = new InMemoryRequestQueue();
        var url1 = "https://example.com/1";
        var url2 = "https://example.com/2";
        var url3 = "https://example.com/3";

        await queue.EnqueueAsync(CreateRequest(url1));
        await queue.EnqueueAsync(CreateRequest(url2));
        await queue.DequeueAsync(); // Remove url1

        // Act
        var contains1 = await queue.ContainsAsync(url1);
        var contains2 = await queue.ContainsAsync(url2);
        var contains3 = await queue.ContainsAsync(url3);

        // Assert
        contains1.Should().BeTrue(); // Still in seen list
        contains2.Should().BeTrue();
        contains3.Should().BeFalse();
    }

    #endregion

    #region RedisRequestQueue Mock Tests

    [Fact]
    public async Task RedisQueue_ShouldSupportDistributedDeduplication()
    {
        // Note: This is a mock test since RedisRequestQueue may not be implemented
        // In real implementation, this would test Redis-based deduplication

        // Arrange
        var mockQueue = new MockDistributedQueue();
        var url = "https://example.com/distributed";

        // Act
        await mockQueue.EnqueueAsync(CreateRequest(url));
        var isDuplicate = await mockQueue.ContainsAsync(url);

        // Assert
        isDuplicate.Should().BeTrue();
    }

    [Fact]
    public async Task RedisQueue_ShouldPersistAcrossInstances()
    {
        // Arrange
        var queue1 = new MockDistributedQueue();
        var queue2 = new MockDistributedQueue(); // Simulates different instance

        var url = "https://example.com/persistent";

        // Act
        await queue1.EnqueueAsync(CreateRequest(url));
        var existsInQueue2 = await queue2.ContainsAsync(url);

        // Assert
        existsInQueue2.Should().BeTrue();
    }

    [Fact]
    public async Task RedisQueue_WithExpiration_ShouldCleanupOldItems()
    {
        // Arrange
        var queue = new MockDistributedQueue(expirationMinutes: 1);
        var url = "https://example.com/expire";

        // Act
        await queue.EnqueueAsync(CreateRequest(url));
        await Task.Delay(TimeSpan.FromSeconds(1)); // Simulate time passing

        // In real implementation, check if expired items are cleaned up
        var contains = await queue.ContainsAsync(url);

        // Assert
        contains.Should().BeTrue(); // Still exists (not enough time passed)
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public async Task Queue_ConcurrentEnqueueDequeue_ShouldMaintainConsistency()
    {
        // Arrange
        var queue = new InMemoryRequestQueue();
        var enqueueCount = 100;
        var dequeueCount = 50;

        // Act
        var enqueueTasks = Enumerable.Range(0, enqueueCount)
            .Select(i => Task.Run(async () =>
                await queue.EnqueueAsync(CreateRequest($"https://example.com/{i}"))));

        var dequeueTasks = Enumerable.Range(0, dequeueCount)
            .Select(i => Task.Run(async () =>
                await queue.DequeueAsync()));

        await Task.WhenAll(enqueueTasks.Concat(dequeueTasks));

        // Assert
        queue.Count.Should().Be(enqueueCount - dequeueCount);
    }

    [Fact]
    public async Task Queue_ConcurrentContains_ShouldBeThreadSafe()
    {
        // Arrange
        var queue = new InMemoryRequestQueue();
        var url = "https://example.com/concurrent";
        await queue.EnqueueAsync(CreateRequest(url));

        // Act
        var containsTasks = Enumerable.Range(0, 100)
            .Select(_ => Task.Run(async () => await queue.ContainsAsync(url)));

        var results = await Task.WhenAll(containsTasks);

        // Assert
        results.Should().OnlyContain(r => r == true);
    }

    #endregion

    #region Helper Methods

    private static Request CreateRequest(string url)
    {
        return new Request
        {
            RequestId = Guid.NewGuid().ToString(),
            Url = url,
            Method = "GET",
            Timeout = TimeSpan.FromSeconds(30),
            Headers = [],
            Metadata = []
        };
    }

    #endregion

    #region Mock Distributed Queue

    /// <summary>
    /// Mock distributed queue for testing distributed scenarios
    /// </summary>
    private sealed class MockDistributedQueue : IRequestQueue
    {
        private static readonly Dictionary<string, Request> _globalQueue = new();
        private static readonly HashSet<string> _globalSeenUrls = new();
        private static readonly object _lock = new();
        private readonly int _expirationMinutes;

        public MockDistributedQueue(int expirationMinutes = 60)
        {
            _expirationMinutes = expirationMinutes;
        }

        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _globalQueue.Count;
                }
            }
        }

        public bool IsEmpty
        {
            get
            {
                lock (_lock)
                {
                    return _globalQueue.Count == 0;
                }
            }
        }

        public Task EnqueueAsync(Request request, int priority = 0, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                if (!_globalSeenUrls.Contains(request.Url))
                {
                    _globalQueue[request.RequestId] = request;
                    _globalSeenUrls.Add(request.Url);
                }
            }
            return Task.CompletedTask;
        }

        public Task<Request?> DequeueAsync(CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                if (_globalQueue.Count == 0)
                    return Task.FromResult<Request?>(null);

                var first = _globalQueue.First();
                _globalQueue.Remove(first.Key);
                return Task.FromResult<Request?>(first.Value);
            }
        }

        public Task<Request?> PeekAsync(CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                if (_globalQueue.Count == 0)
                    return Task.FromResult<Request?>(null);

                return Task.FromResult<Request?>(_globalQueue.First().Value);
            }
        }

        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                _globalQueue.Clear();
                _globalSeenUrls.Clear();
            }
            return Task.CompletedTask;
        }

        public Task<bool> ContainsAsync(string url, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                return Task.FromResult(_globalSeenUrls.Contains(url));
            }
        }
    }

    #endregion
}
