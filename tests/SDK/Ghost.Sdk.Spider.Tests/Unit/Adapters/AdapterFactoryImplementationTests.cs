using FluentAssertions;
using Ghost.Sdk.Spider.Adapters;
using Ghost.Sdk.Spider.Adapters.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Ghost.Sdk.Spider.Tests.Unit.Adapters;

public class AdapterFactoryImplementationTests : IDisposable
{
    private readonly AdapterRegistry _registry;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AdapterFactory> _logger;
    private readonly AdapterFactory _factory;

    public AdapterFactoryImplementationTests()
    {
        _registry = new AdapterRegistry();
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        _serviceProvider = services.BuildServiceProvider();
        _logger = _serviceProvider.GetRequiredService<ILogger<AdapterFactory>>();
        _factory = new AdapterFactory(_registry, _serviceProvider, _logger);
    }

    public void Dispose()
    {
        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Registry_RegisterAdapter_StoresAdapterType()
    {
        // Arrange & Act
        _registry.Register<TestAdapter>("TestAdapter", ContentType.Html);

        // Assert
        var adapterType = _registry.GetAdapterType("TestAdapter");
        adapterType.Should().NotBeNull();
        adapterType.Should().Be<TestAdapter>();
    }

    [Fact]
    public void Registry_RegisterAdapterWithMultipleContentTypes_StoresForAllTypes()
    {
        // Arrange & Act
        _registry.Register<TestAdapter>("TestAdapter", ContentType.Html, ContentType.Json);

        // Assert
        var htmlAdapters = _registry.GetAdaptersByContentType(ContentType.Html);
        var jsonAdapters = _registry.GetAdaptersByContentType(ContentType.Json);

        htmlAdapters.Should().Contain(typeof(TestAdapter));
        jsonAdapters.Should().Contain(typeof(TestAdapter));
    }

    [Fact]
    public void Registry_GetAdapterType_WithInvalidName_ReturnsNull()
    {
        // Act
        var adapterType = _registry.GetAdapterType("NonExistentAdapter");

        // Assert
        adapterType.Should().BeNull();
    }

    [Fact]
    public void Registry_GetAdaptersByContentType_WithNoAdapters_ReturnsEmpty()
    {
        // Act
        var adapters = _registry.GetAdaptersByContentType(ContentType.Binary);

        // Assert
        adapters.Should().BeEmpty();
    }

    [Fact]
    public void Registry_GetAllAdapterTypes_ReturnsAllRegistered()
    {
        // Arrange
        _registry.Register<TestAdapter>("TestAdapter1", ContentType.Html);
        _registry.Register<TestAdapter>("TestAdapter2", ContentType.Json);

        // Act
        var allAdapters = _registry.GetAllAdapterTypes();

        // Assert
        allAdapters.Should().HaveCountGreaterOrEqualTo(2);
    }

    [Fact]
    public void Registry_RegisterWithInvalidType_ThrowsException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            _registry.Register(typeof(string), "InvalidAdapter", ContentType.Html));
    }

    [Fact]
    public void Registry_RegisterWithCaseInsensitiveName_RetrievesCaseInsensitive()
    {
        // Arrange
        _registry.Register<TestAdapter>("TestAdapter", ContentType.Html);

        // Act
        var adapterType1 = _registry.GetAdapterType("TESTADAPTER");
        var adapterType2 = _registry.GetAdapterType("testadapter");

        // Assert
        adapterType1.Should().Be<TestAdapter>();
        adapterType2.Should().Be<TestAdapter>();
    }

    [Fact]
    public void Registry_RegisterMultipleTimes_OverwritesPreviousRegistration()
    {
        // Arrange
        _registry.Register<TestAdapter>("TestAdapter", ContentType.Html);
        _registry.Register<TestAdapter>("TestAdapter", ContentType.Json);

        // Act
        var adapterType = _registry.GetAdapterType("TestAdapter");

        // Assert
        adapterType.Should().Be<TestAdapter>();
    }

    [Fact]
    public void Registry_BuiltInAdapters_AreRegistered()
    {
        // Arrange
        var registry = new AdapterRegistry();

        // Act
        var allAdapters = registry.GetAllAdapterTypes();

        // Assert
        allAdapters.Should().NotBeEmpty("built-in adapters should be registered");
    }

    // Test helper classes
    private sealed class TestAdapter : IContentAdapter
    {
        public string Name => "TestAdapter";
        public ContentType ContentType => ContentType.Html;
        public bool IsAvailable => true;

        public Task<bool> CanHandleAsync(Request request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public Task<Response> ExtractAsync(Request request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new Response
            {
                Content = new ContentResult
                {
                    Content = "<html></html>",
                    ContentType = ContentType.Html
                },
                StatusCode = 200,
                IsSuccess = true
            });
        }

        public Task<Response> ExtractAsync(Request request, AdapterOptions options, CancellationToken cancellationToken = default)
        {
            return ExtractAsync(request, cancellationToken);
        }
    }
}
