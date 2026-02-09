using FluentAssertions;
using Ghost.Sdk.Deduplication;
using Ghost.Sdk.Spider.Adapters.Contracts;
using Moq;
using Xunit;

namespace Ghost.Sdk.Tests.Deduplication;

[Trait("Category", "Unit")]
public class RFPDupeFilterTests
{
    [Fact]
    public async Task IsDuplicateAsync_WithRequest_DelegatesToStorage()
    {
        // Arrange
        var mockStorage = new Mock<IDupeFilter>();
        mockStorage.Setup(s => s.IsDuplicateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var filter = new RFPDupeFilter(mockStorage.Object);
        var request = new Request("https://example.com/page");

        // Act
        var result = await filter.IsDuplicateAsync(request);

        // Assert
        result.Should().BeFalse();
        mockStorage.Verify(s => s.IsDuplicateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IsDuplicateAsync_WithFingerprint_DelegatesToStorage()
    {
        // Arrange
        var mockStorage = new Mock<IDupeFilter>();
        mockStorage.Setup(s => s.IsDuplicateAsync("test-fingerprint", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var filter = new RFPDupeFilter(mockStorage.Object);

        // Act
        var result = await filter.IsDuplicateAsync("test-fingerprint");

        // Assert
        result.Should().BeTrue();
        mockStorage.Verify(s => s.IsDuplicateAsync("test-fingerprint", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ClearAsync_DelegatesToStorage()
    {
        // Arrange
        var mockStorage = new Mock<IDupeFilter>();
        var filter = new RFPDupeFilter(mockStorage.Object);

        // Act
        await filter.ClearAsync();

        // Assert
        mockStorage.Verify(s => s.ClearAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Constructor_WithNullStorage_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new RFPDupeFilter(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task IsDuplicateAsync_WithNullRequest_ThrowsArgumentNullException()
    {
        // Arrange
        var filter = new RFPDupeFilter();

        // Act
        var act = async () => await filter.IsDuplicateAsync((Request)null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task DefaultConstructor_UsesInMemoryStorage()
    {
        // Arrange
        var filter = new RFPDupeFilter();
        var request = new Request("https://example.com/page");

        // Act
        var isDuplicate1 = await filter.IsDuplicateAsync(request);
        var isDuplicate2 = await filter.IsDuplicateAsync(request);

        // Assert
        isDuplicate1.Should().BeFalse();
        isDuplicate2.Should().BeTrue();
    }

    [Fact]
    public async Task IsDuplicateAsync_CreatesConsistentFingerprints()
    {
        // Arrange
        var filter = new RFPDupeFilter();
        var request1 = new Request("https://example.com/page?a=1&b=2");
        var request2 = new Request("https://example.com/page?b=2&a=1");

        // Act
        var isDuplicate1 = await filter.IsDuplicateAsync(request1);
        var isDuplicate2 = await filter.IsDuplicateAsync(request2);

        // Assert
        isDuplicate1.Should().BeFalse();
        isDuplicate2.Should().BeTrue(); // Should recognize as duplicate due to fingerprinting
    }

    [Fact]
    public async Task IsDuplicateAsync_WithCancellationToken_PassesToStorage()
    {
        // Arrange
        var mockStorage = new Mock<IDupeFilter>();
        var cts = new CancellationTokenSource();
        var ct = cts.Token;

        mockStorage.Setup(s => s.IsDuplicateAsync(It.IsAny<string>(), ct))
            .ReturnsAsync(false);

        var filter = new RFPDupeFilter(mockStorage.Object);
        var request = new Request("https://example.com/page");

        // Act
        await filter.IsDuplicateAsync(request, ct);

        // Assert
        mockStorage.Verify(s => s.IsDuplicateAsync(It.IsAny<string>(), ct), Times.Once);
    }

    [Fact]
    public async Task IsDuplicateAsync_WithMultipleRequests_TracksCorrectly()
    {
        // Arrange
        var filter = new RFPDupeFilter();
        var request1 = new Request("https://example.com/page1");
        var request2 = new Request("https://example.com/page2");
        var request3 = new Request("https://example.com/page1"); // Duplicate of request1

        // Act
        var result1 = await filter.IsDuplicateAsync(request1);
        var result2 = await filter.IsDuplicateAsync(request2);
        var result3 = await filter.IsDuplicateAsync(request3);

        // Assert
        result1.Should().BeFalse(); // First time seeing request1
        result2.Should().BeFalse(); // First time seeing request2
        result3.Should().BeTrue();  // Duplicate of request1
    }
}
