using FluentAssertions;
using Ghost.Sdk.Middleware;
using Microsoft.Playwright;
using Xunit;

namespace Ghost.Sdk.Tests.Middleware;

public sealed class CookieMiddlewareTests : IDisposable
{
    private readonly CookieMiddleware _middleware;
    private readonly string _testFilePath;

    public CookieMiddlewareTests()
    {
        _middleware = new CookieMiddleware();
        _testFilePath = Path.Combine(Path.GetTempPath(), $"test_cookies_{Guid.NewGuid()}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_testFilePath))
        {
            File.Delete(_testFilePath);
        }
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task GetCookiesAsync_WithNoDomainCookies_ReturnsEmptyList()
    {
        // Act
        var cookies = await _middleware.GetCookiesAsync("example.com");

        // Assert
        cookies.Should().NotBeNull();
        cookies.Should().BeEmpty();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task SetCookieAsync_ThenGetCookiesAsync_ReturnsCookie()
    {
        // Arrange
        var domain = "example.com";
        var cookie = CreateCookie("sessionId", "abc123", domain);

        // Act
        await _middleware.SetCookieAsync(domain, cookie);
        var cookies = await _middleware.GetCookiesAsync(domain);

        // Assert
        cookies.Should().HaveCount(1);
        cookies[0].Name.Should().Be("sessionId");
        cookies[0].Value.Should().Be("abc123");
        cookies[0].Domain.Should().Be(domain);
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task SetCookieAsync_WithSameName_ReplacesExistingCookie()
    {
        // Arrange
        var domain = "example.com";
        var cookie1 = CreateCookie("sessionId", "old-value", domain);
        var cookie2 = CreateCookie("sessionId", "new-value", domain);

        // Act
        await _middleware.SetCookieAsync(domain, cookie1);
        await _middleware.SetCookieAsync(domain, cookie2);
        var cookies = await _middleware.GetCookiesAsync(domain);

        // Assert
        cookies.Should().HaveCount(1);
        cookies[0].Value.Should().Be("new-value");
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task SetCookieAsync_WithDifferentDomains_StoresSeparately()
    {
        // Arrange
        var domain1 = "example.com";
        var domain2 = "test.com";
        var cookie1 = CreateCookie("sessionId", "value1", domain1);
        var cookie2 = CreateCookie("sessionId", "value2", domain2);

        // Act
        await _middleware.SetCookieAsync(domain1, cookie1);
        await _middleware.SetCookieAsync(domain2, cookie2);
        var cookies1 = await _middleware.GetCookiesAsync(domain1);
        var cookies2 = await _middleware.GetCookiesAsync(domain2);

        // Assert
        cookies1.Should().HaveCount(1);
        cookies2.Should().HaveCount(1);
        cookies1[0].Value.Should().Be("value1");
        cookies2[0].Value.Should().Be("value2");
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task SetCookieAsync_WithMultipleCookies_StoresAll()
    {
        // Arrange
        var domain = "example.com";
        var cookie1 = CreateCookie("sessionId", "abc123", domain);
        var cookie2 = CreateCookie("userId", "user456", domain);
        var cookie3 = CreateCookie("theme", "dark", domain);

        // Act
        await _middleware.SetCookieAsync(domain, cookie1);
        await _middleware.SetCookieAsync(domain, cookie2);
        await _middleware.SetCookieAsync(domain, cookie3);
        var cookies = await _middleware.GetCookiesAsync(domain);

        // Assert
        cookies.Should().HaveCount(3);
        cookies.Should().Contain(c => c.Name == "sessionId" && c.Value == "abc123");
        cookies.Should().Contain(c => c.Name == "userId" && c.Value == "user456");
        cookies.Should().Contain(c => c.Name == "theme" && c.Value == "dark");
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task SaveCookiesAsync_ThenLoadCookiesAsync_RestoresCookies()
    {
        // Arrange
        var domain = "example.com";
        var cookie1 = CreateCookie("sessionId", "abc123", domain);
        var cookie2 = CreateCookie("userId", "user456", domain);
        await _middleware.SetCookieAsync(domain, cookie1);
        await _middleware.SetCookieAsync(domain, cookie2);

        // Act
        await _middleware.SaveCookiesAsync(_testFilePath);

        var newMiddleware = new CookieMiddleware();
        await newMiddleware.LoadCookiesAsync(_testFilePath);
        var cookies = await newMiddleware.GetCookiesAsync(domain);

        // Assert
        cookies.Should().HaveCount(2);
        cookies.Should().Contain(c => c.Name == "sessionId" && c.Value == "abc123");
        cookies.Should().Contain(c => c.Name == "userId" && c.Value == "user456");
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task SaveCookiesAsync_WithMultipleDomains_SavesAll()
    {
        // Arrange
        var domain1 = "example.com";
        var domain2 = "test.com";
        var cookie1 = CreateCookie("sessionId", "value1", domain1);
        var cookie2 = CreateCookie("sessionId", "value2", domain2);
        await _middleware.SetCookieAsync(domain1, cookie1);
        await _middleware.SetCookieAsync(domain2, cookie2);

        // Act
        await _middleware.SaveCookiesAsync(_testFilePath);

        var newMiddleware = new CookieMiddleware();
        await newMiddleware.LoadCookiesAsync(_testFilePath);
        var cookies1 = await newMiddleware.GetCookiesAsync(domain1);
        var cookies2 = await newMiddleware.GetCookiesAsync(domain2);

        // Assert
        cookies1.Should().HaveCount(1);
        cookies2.Should().HaveCount(1);
        cookies1[0].Value.Should().Be("value1");
        cookies2[0].Value.Should().Be("value2");
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task LoadCookiesAsync_WithNonExistentFile_DoesNotThrow()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}.json");

        // Act
        var act = async () => await _middleware.LoadCookiesAsync(nonExistentPath);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task LoadCookiesAsync_WithEmptyFile_DoesNotThrow()
    {
        // Arrange
        var emptyFilePath = Path.Combine(Path.GetTempPath(), $"empty_{Guid.NewGuid()}.json");
        File.WriteAllText(emptyFilePath, "{}");

        try
        {
            // Act
            var act = async () => await _middleware.LoadCookiesAsync(emptyFilePath);

            // Assert
            await act.Should().NotThrowAsync();
        }
        finally
        {
            File.Delete(emptyFilePath);
        }
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task ClearCookiesAsync_RemovesAllCookiesForDomain()
    {
        // Arrange
        var domain = "example.com";
        var cookie1 = CreateCookie("sessionId", "abc123", domain);
        var cookie2 = CreateCookie("userId", "user456", domain);
        await _middleware.SetCookieAsync(domain, cookie1);
        await _middleware.SetCookieAsync(domain, cookie2);

        // Act
        await _middleware.ClearCookiesAsync(domain);
        var cookies = await _middleware.GetCookiesAsync(domain);

        // Assert
        cookies.Should().BeEmpty();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task ClearCookiesAsync_DoesNotAffectOtherDomains()
    {
        // Arrange
        var domain1 = "example.com";
        var domain2 = "test.com";
        var cookie1 = CreateCookie("sessionId", "value1", domain1);
        var cookie2 = CreateCookie("sessionId", "value2", domain2);
        await _middleware.SetCookieAsync(domain1, cookie1);
        await _middleware.SetCookieAsync(domain2, cookie2);

        // Act
        await _middleware.ClearCookiesAsync(domain1);
        var cookies1 = await _middleware.GetCookiesAsync(domain1);
        var cookies2 = await _middleware.GetCookiesAsync(domain2);

        // Assert
        cookies1.Should().BeEmpty();
        cookies2.Should().HaveCount(1);
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task ClearAllCookiesAsync_RemovesAllCookies()
    {
        // Arrange
        var domain1 = "example.com";
        var domain2 = "test.com";
        var cookie1 = CreateCookie("sessionId", "value1", domain1);
        var cookie2 = CreateCookie("sessionId", "value2", domain2);
        await _middleware.SetCookieAsync(domain1, cookie1);
        await _middleware.SetCookieAsync(domain2, cookie2);

        // Act
        await _middleware.ClearAllCookiesAsync();
        var cookies1 = await _middleware.GetCookiesAsync(domain1);
        var cookies2 = await _middleware.GetCookiesAsync(domain2);

        // Assert
        cookies1.Should().BeEmpty();
        cookies2.Should().BeEmpty();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task SetCookieAsync_WithNullDomain_ThrowsArgumentNullException()
    {
        // Arrange
        var cookie = CreateCookie("sessionId", "abc123", "example.com");

        // Act
        var act = async () => await _middleware.SetCookieAsync(null!, cookie);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task SetCookieAsync_WithNullCookie_ThrowsArgumentNullException()
    {
        // Act
        var act = async () => await _middleware.SetCookieAsync("example.com", null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task GetCookiesAsync_WithNullDomain_ThrowsArgumentNullException()
    {
        // Act
        var act = async () => await _middleware.GetCookiesAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task LoadCookiesAsync_WithNullFilePath_ThrowsArgumentNullException()
    {
        // Act
        var act = async () => await _middleware.LoadCookiesAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task SaveCookiesAsync_WithNullFilePath_ThrowsArgumentNullException()
    {
        // Act
        var act = async () => await _middleware.SaveCookiesAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task ClearCookiesAsync_WithNullDomain_ThrowsArgumentNullException()
    {
        // Act
        var act = async () => await _middleware.ClearCookiesAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task SetCookieAsync_WithCancellationToken_CompletesSuccessfully()
    {
        // Arrange
        var domain = "example.com";
        var cookie = CreateCookie("sessionId", "abc123", domain);
        using var cts = new CancellationTokenSource();

        // Act
        await _middleware.SetCookieAsync(domain, cookie, cts.Token);
        var cookies = await _middleware.GetCookiesAsync(domain, cts.Token);

        // Assert
        cookies.Should().HaveCount(1);
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task SaveCookiesAsync_CreatesValidJsonFile()
    {
        // Arrange
        var domain = "example.com";
        var cookie = CreateCookie("sessionId", "abc123", domain);
        await _middleware.SetCookieAsync(domain, cookie);

        // Act
        await _middleware.SaveCookiesAsync(_testFilePath);

        // Assert
        File.Exists(_testFilePath).Should().BeTrue();
        var content = File.ReadAllText(_testFilePath);
        content.Should().Contain("example.com");
        content.Should().Contain("sessionId");
        content.Should().Contain("abc123");
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task SetCookieAsync_WithComplexCookie_PreservesAllProperties()
    {
        // Arrange
        var domain = "example.com";
        var cookie = new Cookie
        {
            Name = "sessionId",
            Value = "abc123",
            Domain = domain,
            Path = "/api",
            Expires = 1735689600, // Unix timestamp
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteAttribute.Strict
        };

        // Act
        await _middleware.SetCookieAsync(domain, cookie);
        await _middleware.SaveCookiesAsync(_testFilePath);

        var newMiddleware = new CookieMiddleware();
        await newMiddleware.LoadCookiesAsync(_testFilePath);
        var cookies = await newMiddleware.GetCookiesAsync(domain);

        // Assert
        cookies.Should().HaveCount(1);
        var restored = cookies[0];
        restored.Name.Should().Be("sessionId");
        restored.Value.Should().Be("abc123");
        restored.Domain.Should().Be(domain);
        restored.Path.Should().Be("/api");
        restored.Expires.Should().Be(1735689600);
        restored.HttpOnly.Should().BeTrue();
        restored.Secure.Should().BeTrue();
        restored.SameSite.Should().Be(SameSiteAttribute.Strict);
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task GetCookiesAsync_ReturnsImmutableCopy()
    {
        // Arrange
        var domain = "example.com";
        var cookie = CreateCookie("sessionId", "abc123", domain);
        await _middleware.SetCookieAsync(domain, cookie);

        // Act
        var cookies1 = await _middleware.GetCookiesAsync(domain);
        var cookies2 = await _middleware.GetCookiesAsync(domain);

        // Assert
        cookies1.Should().NotBeSameAs(cookies2); // Different list instances
        cookies1.Should().HaveCount(1);
        cookies2.Should().HaveCount(1);
    }

    private static Cookie CreateCookie(string name, string value, string domain)
    {
        return new Cookie
        {
            Name = name,
            Value = value,
            Domain = domain,
            Path = "/",
            Expires = -1,
            HttpOnly = false,
            Secure = false,
            SameSite = SameSiteAttribute.None
        };
    }
}
