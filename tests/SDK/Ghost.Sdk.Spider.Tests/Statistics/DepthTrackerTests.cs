using Ghost.Sdk.Spider.Adapters.Contracts;
using Ghost.Sdk.Spider.Statistics;
using Xunit;

namespace Ghost.Sdk.Spider.Tests.Statistics;

/// <summary>
/// Unit tests for <see cref="DepthTracker"/>.
/// </summary>
[Trait("Category", "Unit")]
public class DepthTrackerTests
{
    private const string StartUrl = "https://example.com";

    [Fact]
    public void Constructor_WithNullUrl_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new DepthTracker(null!));
    }

    [Fact]
    public void GetDepth_WithUntrackedRequest_ReturnsZero()
    {
        // Arrange
        var tracker = new DepthTracker(StartUrl);
        var request = new Request("https://example.com/page1");

        // Act
        var depth = tracker.GetDepth(request);

        // Assert
        Assert.Equal(0, depth);
    }

    [Fact]
    public void SetDepth_WithValidRequest_StoresDepth()
    {
        // Arrange
        var tracker = new DepthTracker(StartUrl);
        var request = new Request("https://example.com/page1");

        // Act
        tracker.SetDepth(request, 1);

        // Assert
        Assert.Equal(1, tracker.GetDepth(request));
    }

    [Fact]
    public void SetDepth_WithMultipleRequests_TracksSeparately()
    {
        // Arrange
        var tracker = new DepthTracker(StartUrl);
        var request1 = new Request("https://example.com/page1");
        var request2 = new Request("https://example.com/page2");

        // Act
        tracker.SetDepth(request1, 1);
        tracker.SetDepth(request2, 2);

        // Assert
        Assert.Equal(1, tracker.GetDepth(request1));
        Assert.Equal(2, tracker.GetDepth(request2));
    }

    [Fact]
    public void SetDepth_UpdatesExistingDepth()
    {
        // Arrange
        var tracker = new DepthTracker(StartUrl);
        var request = new Request("https://example.com/page1");

        // Act
        tracker.SetDepth(request, 1);
        tracker.SetDepth(request, 3);

        // Assert
        Assert.Equal(3, tracker.GetDepth(request));
    }

    [Fact]
    public void GetStatistics_WithNoTrackedUrls_ReturnsEmptyStatistics()
    {
        // Arrange
        var tracker = new DepthTracker(StartUrl);

        // Act
        var stats = tracker.GetStatistics();

        // Assert
        Assert.Equal(0, stats.MaxDepth);
        Assert.Equal(0, stats.AverageDepth);
        Assert.Equal(0, stats.TotalUrls);
        Assert.Empty(stats.Distribution);
    }

    [Fact]
    public void GetStatistics_WithSingleUrl_ReturnsCorrectStatistics()
    {
        // Arrange
        var tracker = new DepthTracker(StartUrl);
        var request = new Request("https://example.com/page1");
        tracker.SetDepth(request, 1);

        // Act
        var stats = tracker.GetStatistics();

        // Assert
        Assert.Equal(1, stats.MaxDepth);
        Assert.Equal(1, stats.AverageDepth);
        Assert.Equal(1, stats.TotalUrls);
        Assert.Single(stats.Distribution);
        Assert.Equal(1, stats.Distribution[1]);
    }

    [Fact]
    public void GetStatistics_WithMultipleUrls_CalculatesCorrectMaxDepth()
    {
        // Arrange
        var tracker = new DepthTracker(StartUrl);
        var request1 = new Request("https://example.com/page1");
        var request2 = new Request("https://example.com/page2");
        var request3 = new Request("https://example.com/page3");

        tracker.SetDepth(request1, 1);
        tracker.SetDepth(request2, 2);
        tracker.SetDepth(request3, 5);

        // Act
        var stats = tracker.GetStatistics();

        // Assert
        Assert.Equal(5, stats.MaxDepth);
    }

    [Fact]
    public void GetStatistics_WithMultipleUrls_CalculatesCorrectAverageDepth()
    {
        // Arrange
        var tracker = new DepthTracker(StartUrl);
        var request1 = new Request("https://example.com/page1");
        var request2 = new Request("https://example.com/page2");
        var request3 = new Request("https://example.com/page3");

        tracker.SetDepth(request1, 1);
        tracker.SetDepth(request2, 2);
        tracker.SetDepth(request3, 3);

        // Act
        var stats = tracker.GetStatistics();

        // Assert
        Assert.Equal(2.0, stats.AverageDepth);
    }

    [Fact]
    public void GetStatistics_WithMultipleUrls_CountsTotalUrls()
    {
        // Arrange
        var tracker = new DepthTracker(StartUrl);
        for (int i = 0; i < 10; i++)
        {
            var request = new Request($"https://example.com/page{i}");
            tracker.SetDepth(request, i % 3);
        }

        // Act
        var stats = tracker.GetStatistics();

        // Assert
        Assert.Equal(10, stats.TotalUrls);
    }

    [Fact]
    public void GetStatistics_WithMultipleUrls_CreatesCorrectDistribution()
    {
        // Arrange
        var tracker = new DepthTracker(StartUrl);

        // 1 URL at depth 0
        tracker.SetDepth(new Request("https://example.com"), 0);

        // 3 URLs at depth 1
        tracker.SetDepth(new Request("https://example.com/page1"), 1);
        tracker.SetDepth(new Request("https://example.com/page2"), 1);
        tracker.SetDepth(new Request("https://example.com/page3"), 1);

        // 2 URLs at depth 2
        tracker.SetDepth(new Request("https://example.com/page1/sub1"), 2);
        tracker.SetDepth(new Request("https://example.com/page1/sub2"), 2);

        // Act
        var stats = tracker.GetStatistics();

        // Assert
        Assert.Equal(3, stats.Distribution.Count);
        Assert.Equal(1, stats.Distribution[0]);
        Assert.Equal(3, stats.Distribution[1]);
        Assert.Equal(2, stats.Distribution[2]);
    }

    [Fact]
    public void GetDepth_WithNullRequest_ThrowsArgumentNullException()
    {
        // Arrange
        var tracker = new DepthTracker(StartUrl);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => tracker.GetDepth(null!));
    }

    [Fact]
    public void SetDepth_WithNullRequest_ThrowsArgumentNullException()
    {
        // Arrange
        var tracker = new DepthTracker(StartUrl);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => tracker.SetDepth(null!, 1));
    }

    [Fact]
    public async Task DepthTracker_IsThreadSafe()
    {
        // Arrange
        var tracker = new DepthTracker(StartUrl);
        const int threadCount = 10;
        const int urlsPerThread = 100;
        var tasks = new Task[threadCount];

        // Act
        for (int t = 0; t < threadCount; t++)
        {
            var threadId = t;
            tasks[t] = Task.Run(() =>
            {
                for (int i = 0; i < urlsPerThread; i++)
                {
                    var url = $"https://example.com/t{threadId}/page{i}";
                    var request = new Request(url);
                    tracker.SetDepth(request, threadId);
                }
            });
        }

        await Task.WhenAll(tasks);

        // Assert
        var stats = tracker.GetStatistics();
        Assert.Equal(threadCount * urlsPerThread, stats.TotalUrls);
    }
}
