using FluentAssertions;
using Ghost.Sdk.Deduplication;
using Ghost.Sdk.Spider.Adapters.Contracts;
using Xunit;

namespace Ghost.Sdk.Tests.Deduplication;

[Trait("Category", "Unit")]
public class InMemoryDupeFilterTests
{
    [Fact]
    public async Task IsDuplicateAsync_WithNewRequest_ReturnsFalse()
    {
        // Arrange
        var filter = new InMemoryDupeFilter();
        var request = new Request("https://example.com/page");

        // Act
        var isDuplicate = await filter.IsDuplicateAsync(request);

        // Assert
        isDuplicate.Should().BeFalse();
    }

    [Fact]
    public async Task IsDuplicateAsync_WithDuplicateRequest_ReturnsTrue()
    {
        // Arrange
        var filter = new InMemoryDupeFilter();
        var request = new Request("https://example.com/page");

        // Act
        await filter.IsDuplicateAsync(request);
        var isDuplicate = await filter.IsDuplicateAsync(request);

        // Assert
        isDuplicate.Should().BeTrue();
    }

    [Fact]
    public async Task IsDuplicateAsync_WithNewFingerprint_ReturnsFalse()
    {
        // Arrange
        var filter = new InMemoryDupeFilter();
        var fingerprint = "abc123";

        // Act
        var isDuplicate = await filter.IsDuplicateAsync(fingerprint);

        // Assert
        isDuplicate.Should().BeFalse();
    }

    [Fact]
    public async Task IsDuplicateAsync_WithDuplicateFingerprint_ReturnsTrue()
    {
        // Arrange
        var filter = new InMemoryDupeFilter();
        var fingerprint = "abc123";

        // Act
        await filter.IsDuplicateAsync(fingerprint);
        var isDuplicate = await filter.IsDuplicateAsync(fingerprint);

        // Assert
        isDuplicate.Should().BeTrue();
    }

    [Fact]
    public async Task IsDuplicateAsync_WithNullRequest_ThrowsArgumentNullException()
    {
        // Arrange
        var filter = new InMemoryDupeFilter();

        // Act
        var act = async () => await filter.IsDuplicateAsync((Request)null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task IsDuplicateAsync_WithNullFingerprint_ThrowsArgumentException()
    {
        // Arrange
        var filter = new InMemoryDupeFilter();

        // Act
        var act = async () => await filter.IsDuplicateAsync((string)null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task IsDuplicateAsync_WithEmptyFingerprint_ThrowsArgumentException()
    {
        // Arrange
        var filter = new InMemoryDupeFilter();

        // Act
        var act = async () => await filter.IsDuplicateAsync(string.Empty);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ClearAsync_RemovesAllFingerprints()
    {
        // Arrange
        var filter = new InMemoryDupeFilter();
        var request = new Request("https://example.com/page");

        // Act
        await filter.IsDuplicateAsync(request);
        var isDuplicateBefore = await filter.IsDuplicateAsync(request);

        await filter.ClearAsync();

        var isDuplicateAfter = await filter.IsDuplicateAsync(request);

        // Assert
        isDuplicateBefore.Should().BeTrue();
        isDuplicateAfter.Should().BeFalse();
    }

    [Fact]
    public async Task Count_ReturnsNumberOfUniqueFingerprints()
    {
        // Arrange
        var filter = new InMemoryDupeFilter();
        var request1 = new Request("https://example.com/page1");
        var request2 = new Request("https://example.com/page2");

        // Act
        await filter.IsDuplicateAsync(request1);
        await filter.IsDuplicateAsync(request2);
        await filter.IsDuplicateAsync(request1); // Duplicate

        // Assert
        filter.Count.Should().Be(2);
    }

    [Fact]
    public async Task IsDuplicateAsync_WithConcurrentRequests_IsThreadSafe()
    {
        // Arrange
        var filter = new InMemoryDupeFilter();
        var requests = Enumerable.Range(0, 100)
            .Select(i => new Request($"https://example.com/page{i % 50}")) // 50 unique URLs, each called twice
            .ToList();

        // Act
        var tasks = requests.Select(r => filter.IsDuplicateAsync(r));
        var results = await Task.WhenAll(tasks);

        // Assert
        var duplicateCount = results.Count(r => r);
        duplicateCount.Should().Be(50); // Half should be duplicates
        filter.Count.Should().Be(50); // Should have 50 unique fingerprints
    }

    [Fact]
    public async Task IsDuplicateAsync_WithSameFingerprintDifferentRequests_RecognizesDuplicates()
    {
        // Arrange
        var filter = new InMemoryDupeFilter();
        // These URLs should produce the same fingerprint (only query param order differs)
        var request1 = new Request("https://example.com/page?a=1&b=2");
        var request2 = new Request("https://example.com/page?b=2&a=1");

        // Act
        var isDuplicate1 = await filter.IsDuplicateAsync(request1);
        var isDuplicate2 = await filter.IsDuplicateAsync(request2);

        // Assert
        isDuplicate1.Should().BeFalse();
        isDuplicate2.Should().BeTrue();
    }
}
