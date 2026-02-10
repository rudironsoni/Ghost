using FluentAssertions;
using Ghost.Sdk.Middleware;
using Xunit;

namespace Ghost.Sdk.Tests.Middleware;

/// <summary>
/// Unit tests for <see cref="OffsiteMiddleware"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class OffsiteMiddlewareTests
{
    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        OffsiteOptions? options = null;

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => new OffsiteMiddleware(options!));
        Assert.Equal("options", exception.ParamName);
    }

    [Fact]
    public void ShouldFollowUrl_WithInvalidUrl_ReturnsFalse()
    {
        // Arrange
        var options = new OffsiteOptions();
        var middleware = new OffsiteMiddleware(options);

        // Act
        var result = middleware.ShouldFollowUrl("not-a-valid-url", "example.com");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ShouldFollowUrl_WithRelativeUrl_ReturnsFalse()
    {
        // Arrange
        var options = new OffsiteOptions();
        var middleware = new OffsiteMiddleware(options);

        // Act
        var result = middleware.ShouldFollowUrl("/relative/path", "example.com");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ShouldFollowUrl_WithNullUrl_ThrowsArgumentNullException()
    {
        // Arrange
        var options = new OffsiteOptions();
        var middleware = new OffsiteMiddleware(options);

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => middleware.ShouldFollowUrl(null!, "example.com"));
        Assert.Equal("url", exception.ParamName);
    }

    [Fact]
    public void ShouldFollowUrl_WithNullBaseDomain_ThrowsArgumentNullException()
    {
        // Arrange
        var options = new OffsiteOptions();
        var middleware = new OffsiteMiddleware(options);

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => middleware.ShouldFollowUrl("https://example.com", null!));
        Assert.Equal("baseDomain", exception.ParamName);
    }

    [Fact]
    public void ShouldFollowUrl_WithSameDomain_ReturnsTrue()
    {
        // Arrange
        var options = new OffsiteOptions();
        var middleware = new OffsiteMiddleware(options);

        // Act
        var result = middleware.ShouldFollowUrl("https://example.com/page", "example.com");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldFollowUrl_WithDifferentDomain_ReturnsFalse()
    {
        // Arrange
        var options = new OffsiteOptions();
        var middleware = new OffsiteMiddleware(options);

        // Act
        var result = middleware.ShouldFollowUrl("https://other.com/page", "example.com");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ShouldFollowUrl_WithSubdomainAndAllowSubdomainsTrue_ReturnsTrue()
    {
        // Arrange
        var options = new OffsiteOptions { AllowSubdomains = true };
        var middleware = new OffsiteMiddleware(options);

        // Act
        var result = middleware.ShouldFollowUrl("https://sub.example.com/page", "example.com");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldFollowUrl_WithSubdomainAndAllowSubdomainsFalse_ReturnsFalse()
    {
        // Arrange
        var options = new OffsiteOptions { AllowSubdomains = false };
        var middleware = new OffsiteMiddleware(options);

        // Act
        var result = middleware.ShouldFollowUrl("https://sub.example.com/page", "example.com");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ShouldFollowUrl_WithAllowedDomain_ReturnsTrue()
    {
        // Arrange
        var options = new OffsiteOptions
        {
            AllowedDomains = new List<string> { "allowed.com" }
        };
        var middleware = new OffsiteMiddleware(options);

        // Act
        var result = middleware.ShouldFollowUrl("https://allowed.com/page", "example.com");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldFollowUrl_WithDeniedDomain_ReturnsFalse()
    {
        // Arrange
        var options = new OffsiteOptions
        {
            AllowedDomains = new List<string> { "denied.com" },
            DenyDomains = new List<string> { "denied.com" }
        };
        var middleware = new OffsiteMiddleware(options);

        // Act
        var result = middleware.ShouldFollowUrl("https://denied.com/page", "example.com");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ShouldFollowUrl_DeniedDomainTakesPrecedenceOverAllowed()
    {
        // Arrange
        var options = new OffsiteOptions
        {
            AllowedDomains = new List<string> { "test.com" },
            DenyDomains = new List<string> { "test.com" }
        };
        var middleware = new OffsiteMiddleware(options);

        // Act
        var result = middleware.ShouldFollowUrl("https://test.com/page", "example.com");

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData("https://example.com", "example.com")]
    [InlineData("https://EXAMPLE.COM", "example.com")]
    [InlineData("https://Example.Com", "example.com")]
    public void ShouldFollowUrl_CaseInsensitiveDomainMatching(string url, string baseDomain)
    {
        // Arrange
        var options = new OffsiteOptions();
        var middleware = new OffsiteMiddleware(options);

        // Act
        var result = middleware.ShouldFollowUrl(url, baseDomain);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldFollowUrl_WithDifferentPortsSameDomain_ReturnsTrue()
    {
        // Arrange
        var options = new OffsiteOptions();
        var middleware = new OffsiteMiddleware(options);

        // Act
        var result = middleware.ShouldFollowUrl("https://example.com:8080/page", "example.com");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldFollowUrl_WithDeepSubdomain_ReturnsTrue()
    {
        // Arrange
        var options = new OffsiteOptions { AllowSubdomains = true };
        var middleware = new OffsiteMiddleware(options);

        // Act
        var result = middleware.ShouldFollowUrl("https://deep.sub.example.com/page", "example.com");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsSameDomain_WithSameDomain_ReturnsTrue()
    {
        // Arrange
        var options = new OffsiteOptions();
        var middleware = new OffsiteMiddleware(options);

        // Act
        var result = middleware.IsSameDomain("https://example.com/page1", "https://example.com/page2");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsSameDomain_WithDifferentDomains_ReturnsFalse()
    {
        // Arrange
        var options = new OffsiteOptions();
        var middleware = new OffsiteMiddleware(options);

        // Act
        var result = middleware.IsSameDomain("https://example.com/page", "https://other.com/page");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsSameDomain_WithSubdomain_ReturnsFalse()
    {
        // Arrange
        var options = new OffsiteOptions();
        var middleware = new OffsiteMiddleware(options);

        // Act
        var result = middleware.IsSameDomain("https://sub.example.com/page", "https://example.com/page");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsSameDomain_WithInvalidFirstUrl_ReturnsFalse()
    {
        // Arrange
        var options = new OffsiteOptions();
        var middleware = new OffsiteMiddleware(options);

        // Act
        var result = middleware.IsSameDomain("not-a-url", "https://example.com");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsSameDomain_WithInvalidSecondUrl_ReturnsFalse()
    {
        // Arrange
        var options = new OffsiteOptions();
        var middleware = new OffsiteMiddleware(options);

        // Act
        var result = middleware.IsSameDomain("https://example.com", "not-a-url");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsSameDomain_WithNullFirstUrl_ThrowsArgumentNullException()
    {
        // Arrange
        var options = new OffsiteOptions();
        var middleware = new OffsiteMiddleware(options);

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => middleware.IsSameDomain(null!, "https://example.com"));
        Assert.Equal("url1", exception.ParamName);
    }

    [Fact]
    public void IsSameDomain_WithNullSecondUrl_ThrowsArgumentNullException()
    {
        // Arrange
        var options = new OffsiteOptions();
        var middleware = new OffsiteMiddleware(options);

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => middleware.IsSameDomain("https://example.com", null!));
        Assert.Equal("url2", exception.ParamName);
    }

    [Theory]
    [InlineData("https://example.com/page1", "https://EXAMPLE.COM/page2")]
    [InlineData("https://Example.Com/page1", "https://example.com/page2")]
    public void IsSameDomain_CaseInsensitiveMatching(string url1, string url2)
    {
        // Arrange
        var options = new OffsiteOptions();
        var middleware = new OffsiteMiddleware(options);

        // Act
        var result = middleware.IsSameDomain(url1, url2);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsSameDomain_WithDifferentPorts_ReturnsTrue()
    {
        // Arrange
        var options = new OffsiteOptions();
        var middleware = new OffsiteMiddleware(options);

        // Act
        var result = middleware.IsSameDomain("https://example.com:443/page", "https://example.com:8080/page");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsSameDomain_WithDifferentSchemes_ReturnsTrue()
    {
        // Arrange
        var options = new OffsiteOptions();
        var middleware = new OffsiteMiddleware(options);

        // Act
        var result = middleware.IsSameDomain("http://example.com/page", "https://example.com/page");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void OffsiteOptions_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var options = new OffsiteOptions();

        // Assert
        Assert.True(options.AllowSubdomains);
        Assert.Empty(options.AllowedDomains);
        Assert.Empty(options.DenyDomains);
    }

    [Fact]
    public void ShouldFollowUrl_WithMultipleAllowedDomains_WorksCorrectly()
    {
        // Arrange
        var options = new OffsiteOptions
        {
            AllowedDomains = new List<string> { "site1.com", "site2.com", "site3.com" }
        };
        var middleware = new OffsiteMiddleware(options);

        // Act & Assert
        Assert.True(middleware.ShouldFollowUrl("https://site1.com/page", "example.com"));
        Assert.True(middleware.ShouldFollowUrl("https://site2.com/page", "example.com"));
        Assert.True(middleware.ShouldFollowUrl("https://site3.com/page", "example.com"));
        Assert.False(middleware.ShouldFollowUrl("https://site4.com/page", "example.com"));
    }

    [Fact]
    public void ShouldFollowUrl_WithMultipleDeniedDomains_WorksCorrectly()
    {
        // Arrange
        var options = new OffsiteOptions
        {
            DenyDomains = new List<string> { "blocked1.com", "blocked2.com" }
        };
        var middleware = new OffsiteMiddleware(options);

        // Act & Assert
        Assert.False(middleware.ShouldFollowUrl("https://blocked1.com/page", "example.com"));
        Assert.False(middleware.ShouldFollowUrl("https://blocked2.com/page", "example.com"));
    }
}
