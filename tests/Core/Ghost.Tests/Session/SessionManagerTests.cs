using Ghost.Session;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using Moq;
using Xunit;

namespace Ghost.Tests.Session;

public sealed class SessionManagerTests
{
    [Fact]
    public async Task SaveSessionAsync_WithValidContext_SavesSession()
    {
        // Arrange
        IOptions<SessionManagerOptions> options = Options.Create(new SessionManagerOptions
        {
            Backend = SessionStorageBackend.FileSystem,
            StoragePath = Path.Combine(Path.GetTempPath(), $"ghost-test-{Guid.NewGuid()}"),
            EnableEncryption = false,
            EnableCompression = false
        });

        var manager = new SessionManager(options);
        var mockContext = new Mock<IBrowserContext>();

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

        mockContext.Setup(c => c.CookiesAsync()).ReturnsAsync(cookies);
        mockContext.Setup(c => c.StorageStateAsync()).ReturnsAsync("{\"cookies\":[],\"origins\":[]}");

        // Act
        string sessionId = await manager.SaveSessionAsync(mockContext.Object, "TestPlatform");

        // Assert
        Assert.NotNull(sessionId);
        Assert.NotEmpty(sessionId);

        // Verify session was saved
        List<string> sessions = await manager.ListSessionsAsync("TestPlatform");
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
        IOptions<SessionManagerOptions> options = Options.Create(new SessionManagerOptions
        {
            Backend = SessionStorageBackend.FileSystem,
            StoragePath = Path.Combine(Path.GetTempPath(), $"ghost-test-{Guid.NewGuid()}"),
            EnableEncryption = false,
            EnableCompression = false
        });

        var manager = new SessionManager(options);
        var mockContext = new Mock<IBrowserContext>();

        mockContext.Setup(c => c.CookiesAsync()).ReturnsAsync(new List<BrowserContextCookiesResult>());
        mockContext.Setup(c => c.StorageStateAsync()).ReturnsAsync("{\"cookies\":[],\"origins\":[]}");

        string sessionId = await manager.SaveSessionAsync(mockContext.Object, "TestPlatform");

        // Act
        BrowserSession? session = await manager.LoadSessionAsync("TestPlatform", sessionId);

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
        IOptions<SessionManagerOptions> options = Options.Create(new SessionManagerOptions
        {
            Backend = SessionStorageBackend.FileSystem,
            StoragePath = Path.Combine(Path.GetTempPath(), $"ghost-test-{Guid.NewGuid()}"),
            EnableEncryption = false,
            EnableCompression = false,
            DefaultTtl = TimeSpan.FromMilliseconds(1) // Expire immediately
        });

        var manager = new SessionManager(options);
        var mockContext = new Mock<IBrowserContext>();

        mockContext.Setup(c => c.CookiesAsync()).ReturnsAsync(new List<BrowserContextCookiesResult>());
        mockContext.Setup(c => c.StorageStateAsync()).ReturnsAsync("{\"cookies\":[],\"origins\":[]}");

        string sessionId = await manager.SaveSessionAsync(mockContext.Object, "TestPlatform");

        // Wait for session to expire
        await Task.Delay(10);

        // Act
        BrowserSession? session = await manager.LoadSessionAsync("TestPlatform", sessionId);

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
        IOptions<SessionManagerOptions> options = Options.Create(new SessionManagerOptions
        {
            Backend = SessionStorageBackend.FileSystem,
            StoragePath = Path.Combine(Path.GetTempPath(), $"ghost-test-{Guid.NewGuid()}"),
            EnableEncryption = false,
            EnableCompression = false
        });

        var manager = new SessionManager(options);
        var mockContext = new Mock<IBrowserContext>();

        mockContext.Setup(c => c.CookiesAsync()).ReturnsAsync(new List<BrowserContextCookiesResult>());
        mockContext.Setup(c => c.StorageStateAsync()).ReturnsAsync("{\"cookies\":[],\"origins\":[]}");

        string sessionId = await manager.SaveSessionAsync(mockContext.Object, "TestPlatform");

        // Act
        await manager.DeleteSessionAsync("TestPlatform", sessionId);

        // Assert
        List<string> sessions = await manager.ListSessionsAsync("TestPlatform");
        Assert.Empty(sessions);

        // Cleanup
        manager.Dispose();
        Directory.Delete(options.Value.StoragePath, recursive: true);
    }
}
