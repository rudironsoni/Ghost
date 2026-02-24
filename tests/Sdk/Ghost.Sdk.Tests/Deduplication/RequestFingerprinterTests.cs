using FluentAssertions;
using Ghost.Sdk.Deduplication;
using Ghost.Sdk.Spider.Adapters.Contracts;
using Ghost.Testing.Reliability;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Sdk.Tests.Deduplication;

[Trait("Category", "Unit")]
public class RequestFingerprinterTests : ReliabilityTestBase
{
    public RequestFingerprinterTests(ITestOutputHelper output) : base(output) { }
    [Fact]
    public void CreateFingerprint_WithSameUrl_ReturnsSameFingerprint()
    {
        // Arrange
        var request1 = new Request("https://example.com/page");
        var request2 = new Request("https://example.com/page");

        // Act
        var fingerprint1 = RequestFingerprinter.CreateFingerprint(request1);
        var fingerprint2 = RequestFingerprinter.CreateFingerprint(request2);

        // Assert
        fingerprint1.Should().Be(fingerprint2);
    }

    [Fact]
    public void CreateFingerprint_WithDifferentUrl_ReturnsDifferentFingerprints()
    {
        // Arrange
        var request1 = new Request("https://example.com/page1");
        var request2 = new Request("https://example.com/page2");

        // Act
        var fingerprint1 = RequestFingerprinter.CreateFingerprint(request1);
        var fingerprint2 = RequestFingerprinter.CreateFingerprint(request2);

        // Assert
        fingerprint1.Should().NotBe(fingerprint2);
    }

    [Fact]
    public void CreateFingerprint_WithFragments_IgnoresFragments()
    {
        // Arrange
        var request1 = new Request("https://example.com/page#section1");
        var request2 = new Request("https://example.com/page#section2");
        var request3 = new Request("https://example.com/page");

        // Act
        var fingerprint1 = RequestFingerprinter.CreateFingerprint(request1);
        var fingerprint2 = RequestFingerprinter.CreateFingerprint(request2);
        var fingerprint3 = RequestFingerprinter.CreateFingerprint(request3);

        // Assert
        fingerprint1.Should().Be(fingerprint2);
        fingerprint1.Should().Be(fingerprint3);
    }

    [Fact]
    public void CreateFingerprint_WithUnsortedQueryParams_SortsParams()
    {
        // Arrange
        var request1 = new Request("https://example.com/page?b=2&a=1");
        var request2 = new Request("https://example.com/page?a=1&b=2");

        // Act
        var fingerprint1 = RequestFingerprinter.CreateFingerprint(request1);
        var fingerprint2 = RequestFingerprinter.CreateFingerprint(request2);

        // Assert
        fingerprint1.Should().Be(fingerprint2);
    }

    [Fact]
    public void CreateFingerprint_WithDifferentMethods_ReturnsDifferentFingerprints()
    {
        // Arrange
        var request1 = Request.Get("https://example.com/page");
        var request2 = Request.Post("https://example.com/page", "data");

        // Act
        var fingerprint1 = RequestFingerprinter.CreateFingerprint(request1);
        var fingerprint2 = RequestFingerprinter.CreateFingerprint(request2);

        // Assert
        fingerprint1.Should().NotBe(fingerprint2);
    }

    [Fact]
    public void CreateFingerprint_WithBody_IncludesBodyInFingerprint()
    {
        // Arrange
        var request1 = Request.Post("https://example.com/page", "body1");
        var request2 = Request.Post("https://example.com/page", "body2");
        var request3 = Request.Post("https://example.com/page", "body1");

        // Act
        var fingerprint1 = RequestFingerprinter.CreateFingerprint(request1);
        var fingerprint2 = RequestFingerprinter.CreateFingerprint(request2);
        var fingerprint3 = RequestFingerprinter.CreateFingerprint(request3);

        // Assert
        fingerprint1.Should().NotBe(fingerprint2);
        fingerprint1.Should().Be(fingerprint3);
    }

    [Fact]
    public void CreateFingerprint_WithCaseInsensitiveSchemeAndHost_NormalizesCase()
    {
        // Arrange
        var request1 = new Request("HTTP://EXAMPLE.COM/page");
        var request2 = new Request("http://example.com/page");

        // Act
        var fingerprint1 = RequestFingerprinter.CreateFingerprint(request1);
        var fingerprint2 = RequestFingerprinter.CreateFingerprint(request2);

        // Assert
        fingerprint1.Should().Be(fingerprint2);
    }

    [Fact]
    public void CreateFingerprint_WithDefaultPorts_RemovesDefaultPorts()
    {
        // Arrange
        var request1 = new Request("http://example.com:80/page");
        var request2 = new Request("http://example.com/page");

        // Act
        var fingerprint1 = RequestFingerprinter.CreateFingerprint(request1);
        var fingerprint2 = RequestFingerprinter.CreateFingerprint(request2);

        // Assert
        fingerprint1.Should().Be(fingerprint2);
    }

    [Fact]
    public void CreateFingerprint_WithNonDefaultPort_IncludesPortInFingerprint()
    {
        // Arrange
        var request1 = new Request("http://example.com:8080/page");
        var request2 = new Request("http://example.com/page");

        // Act
        var fingerprint1 = RequestFingerprinter.CreateFingerprint(request1);
        var fingerprint2 = RequestFingerprinter.CreateFingerprint(request2);

        // Assert
        fingerprint1.Should().NotBe(fingerprint2);
    }

    [Fact]
    public void CreateFingerprint_WithNullRequest_ThrowsArgumentNullException()
    {
        // Act
        var act = () => RequestFingerprinter.CreateFingerprint(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CreateFingerprint_ReturnsLowercaseHexString()
    {
        // Arrange
        var request = new Request("https://example.com/page");

        // Act
        var fingerprint = RequestFingerprinter.CreateFingerprint(request);

        // Assert
        fingerprint.Should().MatchRegex("^[0-9a-f]{64}$"); // SHA256 produces 64 hex chars
    }
}
