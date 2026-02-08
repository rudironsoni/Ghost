using Ghost.Contracts.Social;
using Ghost.Platform.X.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Ghost.Platform.X.Tests;

public class XSocialClientTests
{
    private readonly Mock<IBrowserSession> _sessionMock;
    private readonly Mock<IPage> _pageMock;
    private readonly Mock<IOptions<XOptions>> _optionsMock;
    private readonly Mock<ILogger<XSocialClient>> _loggerMock;
    private readonly Mock<XAuthenticator> _authenticatorMock;
    private readonly Mock<XThreadComposer> _composerMock;

    public XSocialClientTests()
    {
        _sessionMock = new Mock<IBrowserSession>();
        _pageMock = new Mock<IPage>();
        _optionsMock = new Mock<IOptions<XOptions>>();
        _loggerMock = new Mock<ILogger<XSocialClient>>();
        _authenticatorMock = new Mock<XAuthenticator>(
            _sessionMock.Object,
            _optionsMock.Object,
            Mock.Of<ILogger<XAuthenticator>>());
        _composerMock = new Mock<XThreadComposer>(
            _optionsMock.Object,
            Mock.Of<ILogger<XThreadComposer>>());

        _optionsMock.Setup(x => x.Value).Returns(new XOptions());
        _sessionMock.Setup(x => x.NewPageAsync(It.IsAny<PageOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_pageMock.Object);
    }

    [Fact]
    public void Constructor_NullSession_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => new XSocialClient(
            null!,
            _optionsMock.Object,
            _loggerMock.Object,
            _authenticatorMock.Object,
            _composerMock.Object,
            null));
    }

    [Fact]
    public void Constructor_NullAuthenticator_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => new XSocialClient(
            _sessionMock.Object,
            _optionsMock.Object,
            _loggerMock.Object,
            null!,
            _composerMock.Object,
            null));
    }

    [Fact]
    public void Constructor_NullComposer_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => new XSocialClient(
            _sessionMock.Object,
            _optionsMock.Object,
            _loggerMock.Object,
            _authenticatorMock.Object,
            null!,
            null));
    }

    [Fact]
    public void PlatformName_ReturnsX()
    {
        // Arrange
        var client = new XSocialClient(
            _sessionMock.Object,
            _optionsMock.Object,
            _loggerMock.Object,
            _authenticatorMock.Object,
            _composerMock.Object,
            null);

        // Assert
        Assert.Equal("X", client.PlatformName);
    }

    [Fact]
    public async Task GetProfileAsync_CreatesNewPage()
    {
        // Arrange
        var client = new XSocialClient(
            _sessionMock.Object,
            _optionsMock.Object,
            _loggerMock.Object,
            _authenticatorMock.Object,
            _composerMock.Object,
            null);

        _authenticatorMock.Setup(x => x.EnsureAuthenticatedAsync(It.IsAny<IPage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await client.GetProfileAsync("testuser", CancellationToken.None);

        // Assert
        _sessionMock.Verify(x => x.NewPageAsync(It.IsAny<PageOptions?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreatePostAsync_CallsComposer()
    {
        // Arrange
        var client = new XSocialClient(
            _sessionMock.Object,
            _optionsMock.Object,
            _loggerMock.Object,
            _authenticatorMock.Object,
            _composerMock.Object,
            null);

        var request = new CreatePostRequest { Content = "Test content" };

        _authenticatorMock.Setup(x => x.EnsureAuthenticatedAsync(It.IsAny<IPage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _composerMock.Setup(x => x.ComposeAndPostAsync(It.IsAny<IPage>(), request, It.IsAny<CancellationToken>()))
            .ReturnsAsync("tweet_12345");

        // Act
        var result = await client.CreatePostAsync(request, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("tweet_12345", result.Id);
        _composerMock.Verify(x => x.ComposeAndPostAsync(It.IsAny<IPage>(), request, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreatePostAsync_SetsCorrectProperties()
    {
        // Arrange
        var client = new XSocialClient(
            _sessionMock.Object,
            _optionsMock.Object,
            _loggerMock.Object,
            _authenticatorMock.Object,
            _composerMock.Object,
            null);

        var request = new CreatePostRequest
        {
            Content = "Test content",
            MediaUrls = new[] { "/path/to/image.jpg" }
        };

        _authenticatorMock.Setup(x => x.EnsureAuthenticatedAsync(It.IsAny<IPage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _composerMock.Setup(x => x.ComposeAndPostAsync(It.IsAny<IPage>(), It.IsAny<CreatePostRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("tweet_12345");

        // Act
        var result = await client.CreatePostAsync(request, CancellationToken.None);

        // Assert
        Assert.Equal("Test content", result.Content);
        Assert.NotNull(result.CreatedAt);
    }

    [Fact]
    public async Task SearchProfilesAsync_ReturnsEmptyList_WhenNoResults()
    {
        // Arrange
        var client = new XSocialClient(
            _sessionMock.Object,
            _optionsMock.Object,
            _loggerMock.Object,
            _authenticatorMock.Object,
            _composerMock.Object,
            null);

        _authenticatorMock.Setup(x => x.EnsureAuthenticatedAsync(It.IsAny<IPage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _pageMock.Setup(x => x.QuerySelectorAllAsync("[data-testid='UserCell']", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<IElement>());

        var criteria = new ProfileSearchCriteria { Query = "nonexistent", MaxResults = 10 };

        // Act
        var results = await client.SearchProfilesAsync(criteria, CancellationToken.None);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public async Task GetFeedAsync_ReturnsPosts()
    {
        // Arrange
        var client = new XSocialClient(
            _sessionMock.Object,
            _optionsMock.Object,
            _loggerMock.Object,
            _authenticatorMock.Object,
            _composerMock.Object,
            null);

        _authenticatorMock.Setup(x => x.EnsureAuthenticatedAsync(It.IsAny<IPage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _pageMock.Setup(x => x.QuerySelectorAllAsync("article[data-testid='tweet']", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<IElement>());

        var options = new FeedOptions { PageSize = 25 };

        // Act
        var results = await client.GetFeedAsync(options, CancellationToken.None);

        // Assert
        Assert.NotNull(results);
    }

    [Fact]
    public async Task GetConnectionsAsync_ReturnsConnections()
    {
        // Arrange
        var client = new XSocialClient(
            _sessionMock.Object,
            _optionsMock.Object,
            _loggerMock.Object,
            _authenticatorMock.Object,
            _composerMock.Object,
            null);

        _authenticatorMock.Setup(x => x.EnsureAuthenticatedAsync(It.IsAny<IPage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _pageMock.Setup(x => x.QuerySelectorAllAsync("[data-testid='UserCell']", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<IElement>());

        var options = new ConnectionsOptions { ProfileId = "user", MaxResults = 25 };

        // Act
        var results = await client.GetConnectionsAsync(options, CancellationToken.None);

        // Assert
        Assert.NotNull(results);
        _pageMock.Verify(x => x.NavigateAsync("https://x.com/user/following", It.IsAny<NavigationOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Constructor_WithNullOptions_UsesDefaults()
    {
        // Arrange & Act
        var optionsMock = new Mock<IOptions<XOptions>>();
        optionsMock.Setup(x => x.Value).Returns((XOptions?)null!);

        var client = new XSocialClient(
            _sessionMock.Object,
            optionsMock.Object,
            _loggerMock.Object,
            _authenticatorMock.Object,
            _composerMock.Object,
            null);

        // Assert - Should not throw
        Assert.NotNull(client);
    }

    [Fact]
    public void Constructor_WithNullLogger_UsesNullLogger()
    {
        // Arrange & Act
        var client = new XSocialClient(
            _sessionMock.Object,
            _optionsMock.Object,
            null!,
            _authenticatorMock.Object,
            _composerMock.Object,
            null);

        // Assert - Should not throw
        Assert.NotNull(client);
    }
}
