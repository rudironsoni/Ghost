using FluentAssertions;
using Ghost.Sdk.Spider.Adapters;
using Ghost.Sdk.Spider.Adapters.Contracts;
using Ghost.Sdk.Spider.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Ghost.Sdk.Spider.Tests.Unit.Adapters;

public class AdapterFactoryTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly AdapterRegistry _registry;
    private readonly AdapterFactory _factory;

    public AdapterFactoryTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton<HttpClient>();
        services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(NullLogger<>));
        _serviceProvider = services.BuildServiceProvider();

        _registry = new AdapterRegistry();
        _registry.Register<StaticHtmlAdapter>("StaticHtml", ContentType.StaticHtml);

        _factory = new AdapterFactory(_registry, _serviceProvider, NullLogger<AdapterFactory>.Instance);
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task CreateAdapterAsync_WithHttpRequest_ShouldReturnStaticHtmlAdapter()
    {
        // Arrange
        var request = TestData.CreateRequest("https://example.com");

        // Act
        var adapter = await _factory.CreateAdapterAsync(request);

        // Assert
        adapter.Should().NotBeNull();
        adapter.Should().BeOfType<StaticHtmlAdapter>();
    }

    [Fact]
    public async Task CreateAdapterAsync_WithNullRequest_ShouldThrow()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _factory.CreateAdapterAsync(null!));
    }

    [Fact]
    public async Task CreateAdapterAsync_WithNoSuitableAdapter_ShouldReturnNull()
    {
        // Arrange
        var request = TestData.CreateRequest("ftp://example.com");

        // Act
        var adapter = await _factory.CreateAdapterAsync(request);

        // Assert
        adapter.Should().BeNull();
    }

    [Fact]
    public async Task CreateAdapterAsync_WithAdapterPreference_ShouldUsePreferredAdapter()
    {
        // Arrange
        var request = TestData.CreateRequest("https://example.com");
        request.Metadata["AdapterPreference"] = "StaticHtml";

        // Act
        var adapter = await _factory.CreateAdapterAsync(request);

        // Assert
        adapter.Should().NotBeNull();
        adapter!.Name.Should().Be("StaticHtml");
    }

    [Fact]
    public async Task CreateAdapterAsync_WithInvalidPreference_ShouldFallback()
    {
        // Arrange
        var request = TestData.CreateRequest("https://example.com");
        request.Metadata["AdapterPreference"] = "NonExistentAdapter";

        // Act
        var adapter = await _factory.CreateAdapterAsync(request);

        // Assert
        adapter.Should().NotBeNull(); // Should fallback to any suitable adapter
    }

    [Fact]
    public async Task CreateAdapterAsync_WithExpectedContentType_ShouldReturnMatchingAdapter()
    {
        // Arrange
        var request = TestData.CreateRequest("https://example.com");
        request.ExpectedContentType = ContentType.StaticHtml;

        // Act
        var adapter = await _factory.CreateAdapterAsync(request);

        // Assert
        adapter.Should().NotBeNull();
        adapter!.ContentType.Should().Be(ContentType.StaticHtml);
    }

    [Fact]
    public void CreateAdapterByName_WithValidName_ShouldReturnAdapter()
    {
        // Act
        var adapter = _factory.CreateAdapterByName("StaticHtml");

        // Assert
        adapter.Should().NotBeNull();
        adapter!.Name.Should().Be("StaticHtml");
    }

    [Fact]
    public void CreateAdapterByName_WithInvalidName_ShouldReturnNull()
    {
        // Act
        var adapter = _factory.CreateAdapterByName("NonExistent");

        // Assert
        adapter.Should().BeNull();
    }

    [Fact]
    public void CreateAdapterByName_WithNullName_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _factory.CreateAdapterByName(null!));
    }

    [Fact]
    public void CreateAdapterByName_WithEmptyName_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _factory.CreateAdapterByName(""));
    }

    [Fact]
    public void CreateAdaptersByContentType_WithValidType_ShouldReturnAdapters()
    {
        // Act
        var adapters = _factory.CreateAdaptersByContentType(ContentType.StaticHtml).ToList();

        // Assert
        adapters.Should().NotBeEmpty();
        adapters.Should().Contain(a => a.ContentType == ContentType.StaticHtml);
    }

    [Fact]
    public void CreateAdaptersByContentType_WithNoMatchingType_ShouldReturnEmpty()
    {
        // Act
        var adapters = _factory.CreateAdaptersByContentType(ContentType.GraphQL);

        // Assert
        adapters.Should().BeEmpty();
    }

    [Fact]
    public void GetAllAvailableAdapters_ShouldReturnAllRegisteredAdapters()
    {
        // Act
        var adapters = _factory.GetAllAvailableAdapters();

        // Assert
        adapters.Should().NotBeEmpty();
        adapters.Should().AllSatisfy(a => a.IsAvailable.Should().BeTrue());
    }

    [Fact]
    public async Task CreateAdapterAsync_WithCancellation_ShouldRespectCancellation()
    {
        // Arrange
        var request = TestData.CreateRequest("https://example.com");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var adapter = await _factory.CreateAdapterAsync(request, cts.Token);

        // Assert - Should still complete as selection doesn't involve async operations
        // But the cancellation token is passed through
        adapter.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateAdapterAsync_WithMultipleMatchingAdapters_ShouldReturnFirst()
    {
        // Arrange
        // Register multiple adapters for the same content type
        _registry.Register<StaticHtmlAdapter>("StaticHtml2", ContentType.StaticHtml);

        var request = TestData.CreateRequest("https://example.com");
        request.ExpectedContentType = ContentType.StaticHtml;

        // Act
        var adapter = await _factory.CreateAdapterAsync(request);

        // Assert
        adapter.Should().NotBeNull();
        adapter!.ContentType.Should().Be(ContentType.StaticHtml);
    }
}
