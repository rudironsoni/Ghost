using Ghost.Session;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using NSubstitute;
using Xunit;

namespace Ghost.Tests.Session;

public sealed class SessionManagerTests
{
    [Fact]
    public async Task SaveSessionAsync_WithValidContext_SavesSession()
    {
        // Arrange
        var options = Options.Create(new SessionManagerOptions
        {
            Backend = SessionStorageBackend.FileSystem,
            StoragePath = Path.Combine(Path.GetTempPath(), $"ghost-test-{Guid.NewGuid()}"),
            EnableEncryption = false,
            EnableCompression = false
        });

        var manager = new SessionManager(options);
        var context = Substitute.For<IBrowserContext>();

        var cookies = new List<BrowserContextCookiesResult>
        {
            new()
            {
                Name = "test_cookie",
                Value = "test_value",
                Domain = ".example.com",
                Path = "/",
                Expires = -1,
                HttpOnly = false,
                Secure = true,
                SameSite = SameSiteAttribute.Lax
            }
        };

        context.CookiesAsync().Returns(cookies);
        context.StorageStateAsync().Returns("{\"cookies\":[],\"origins\":[]}");

        // Act
        var sessionId = await manager.SaveSessionAsync(context, "TestPlatform");

        // Assert
        Assert.NotNull(sessionId);
        Assert.NotEmpty(sessionId);

        // Verify session was saved
        var sessions = await manager.ListSessionsAsync("TestPlatform");
        Assert.Single(sessions);
        Assert.Equal(sessionId, sessions[0]);

        // Cleanup
        await manager.DeleteSessionAsync("TestPlatform", sessionId);
        manager.Dispose();
        Directory.Delete(options.Value.StoragePath, recursive: true);
    }

    [Fact]
    public async Task LoadSessionAsync_WithSavedSession_ReturnsSession()
    {
        // Arrange
        var options = Options.Create(new SessionManagerOptions
        {
            Backend = SessionStorageBackend.FileSystem,
            StoragePath = Path.Combine(Path.GetTempPath(), $"ghost-test-{Guid.NewGuid()}"),
            EnableEncryption = false,
            EnableCompression = false
        });

        var manager = new SessionManager(options);
        var context = Substitute.For<IBrowserContext>();

        context.CookiesAsync().Returns(new List<BrowserContextCookiesResult>());
        context.StorageStateAsync().Returns("{\"cookies\":[],\"origins\":[]}");

        var sessionId = await manager.SaveSessionAsync(context, "TestPlatform");

        // Act
        var session = await manager.LoadSessionAsync("TestPlatform", sessionId);

        // Assert
        Assert.NotNull(session);
        Assert.Equal(sessionId, session.SessionId);
        Assert.Equal("TestPlatform", session.Platform);
        Assert.False(session.IsExpired());

        // Cleanup
        await manager.DeleteSessionAsync("TestPlatform", sessionId);
        manager.Dispose();
        Directory.Delete(options.Value.StoragePath, recursive: true);
    }

    [Fact]
    public async Task LoadSessionAsync_WithExpiredSession_ReturnsNull()
    {
        // Arrange
        var options = Options.Create(new SessionManagerOptions
        {
            Backend = SessionStorageBackend.FileSystem,
            StoragePath = Path.Combine(Path.GetTempPath(), $"ghost-test-{Guid.NewGuid()}"),
            EnableEncryption = false,
            EnableCompression = false,
            DefaultTtl = TimeSpan.FromMilliseconds(1) // Expire immediately
        });

        var manager = new SessionManager(options);
        var context = Substitute.For<IBrowserContext>();

        context.CookiesAsync().Returns(new List<BrowserContextCookiesResult>());
        context.StorageStateAsync().Returns("{\"cookies\":[],\"origins\":[]}");

        var sessionId = await manager.SaveSessionAsync(context, "TestPlatform");

        // Wait for session to expire
        await Task.Delay(10);

        // Act
        var session = await manager.LoadSessionAsync("TestPlatform", sessionId);

        // Assert
        Assert.Null(session);

        // Cleanup
        manager.Dispose();
        Directory.Delete(options.Value.StoragePath, recursive: true);
    }

    [Fact]
    public async Task DeleteSessionAsync_RemovesSession()
    {
        // Arrange
        var options = Options.Create(new SessionManagerOptions
        {
            Backend = SessionStorageBackend.FileSystem,
            StoragePath = Path.Combine(Path.GetTempPath(), $"ghost-test-{Guid.NewGuid()}"),
            EnableEncryption = false,
            EnableCompression = false
        });

        var manager = new SessionManager(options);
        var context = Substitute.For<IBrowserContext>();

        context.CookiesAsync().Returns(new List<BrowserContextCookiesResult>());
        context.StorageStateAsync().Returns("{\"cookies\":[],\"origins\":[]}");

        var sessionId = await manager.SaveSessionAsync(context, "TestPlatform");

        // Act
        await manager.DeleteSessionAsync("TestPlatform", sessionId);

        // Assert
        var sessions = await manager.ListSessionsAsync("TestPlatform");
        Assert.Empty(sessions);

        // Cleanup
        manager.Dispose();
        Directory.Delete(options.Value.StoragePath, recursive: true);
    }
}
