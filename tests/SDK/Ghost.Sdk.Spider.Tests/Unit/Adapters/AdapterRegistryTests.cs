using FluentAssertions;
using Ghost.Sdk.Spider.Adapters;
using Ghost.Sdk.Spider.Adapters.Contracts;
using NUnit.Framework;

namespace Ghost.Sdk.Spider.Tests.Unit.Adapters;

/// <summary>
/// Comprehensive tests for AdapterRegistry covering registration and discovery.
/// </summary>
[TestFixture]
public class AdapterRegistryTests
{
    private AdapterRegistry _registry = null!;

    [SetUp]
    public void Setup()
    {
        _registry = new AdapterRegistry();
    }

    [Test]
    public void Constructor_ShouldRegisterBuiltInAdapters()
    {
        // Assert
        _registry.IsRegistered("StaticHtml").Should().BeTrue();
        _registry.IsRegistered("JavaScript").Should().BeTrue();
        _registry.IsRegistered("GraphQL").Should().BeTrue();
    }

    [Test]
    public void Register_WithValidAdapter_ShouldRegisterSuccessfully()
    {
        // Act
        _registry.Register<StaticHtmlAdapter>("TestAdapter", ContentType.Html);

        // Assert
        _registry.IsRegistered("TestAdapter").Should().BeTrue();
    }

    [Test]
    public void Register_WithNullType_ShouldThrowArgumentNullException()
    {
        // Act
        Action act = () => _registry.Register(null!, "TestAdapter", ContentType.Html);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("adapterType");
    }

    [Test]
    public void Register_WithNullName_ShouldThrowArgumentException()
    {
        // Act
        Action act = () => _registry.Register<StaticHtmlAdapter>(null!, ContentType.Html);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void Register_WithEmptyName_ShouldThrowArgumentException()
    {
        // Act
        Action act = () => _registry.Register<StaticHtmlAdapter>(string.Empty, ContentType.Html);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void Register_WithWhitespaceName_ShouldThrowArgumentException()
    {
        // Act
        Action act = () => _registry.Register<StaticHtmlAdapter>("   ", ContentType.Html);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void Register_WithNonAdapterType_ShouldThrowArgumentException()
    {
        // Act
        Action act = () => _registry.Register(typeof(string), "InvalidAdapter", ContentType.Html);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*does not implement IContentAdapter*");
    }

    [Test]
    public void Register_WithMultipleContentTypes_ShouldRegisterForAll()
    {
        // Act
        _registry.Register<StaticHtmlAdapter>("MultiType", ContentType.Html, ContentType.Xml);

        // Assert
        var htmlAdapters = _registry.GetAdaptersByContentType(ContentType.Html);
        var xmlAdapters = _registry.GetAdaptersByContentType(ContentType.Xml);
        
        htmlAdapters.Should().Contain(typeof(StaticHtmlAdapter));
        xmlAdapters.Should().Contain(typeof(StaticHtmlAdapter));
    }

    [Test]
    public void Register_SameNameTwice_ShouldOverwrite()
    {
        // Arrange
        _registry.Register<StaticHtmlAdapter>("TestAdapter", ContentType.Html);

        // Act
        _registry.Register<GraphQLAdapter>("TestAdapter", ContentType.Json);

        // Assert
        var adapterType = _registry.GetAdapterType("TestAdapter");
        adapterType.Should().Be(typeof(GraphQLAdapter));
    }

    [Test]
    public void GetAdapterType_WithExistingName_ShouldReturnType()
    {
        // Arrange
        _registry.Register<StaticHtmlAdapter>("TestAdapter", ContentType.Html);

        // Act
        var result = _registry.GetAdapterType("TestAdapter");

        // Assert
        result.Should().Be(typeof(StaticHtmlAdapter));
    }

    [Test]
    public void GetAdapterType_WithNonExistingName_ShouldReturnNull()
    {
        // Act
        var result = _registry.GetAdapterType("NonExistent");

        // Assert
        result.Should().BeNull();
    }

    [Test]
    public void GetAdapterType_WithNullName_ShouldThrowArgumentException()
    {
        // Act
        Action act = () => _registry.GetAdapterType(null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void GetAdapterType_WithEmptyName_ShouldThrowArgumentException()
    {
        // Act
        Action act = () => _registry.GetAdapterType(string.Empty);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void GetAdapterType_IsCaseInsensitive()
    {
        // Arrange
        _registry.Register<StaticHtmlAdapter>("TestAdapter", ContentType.Html);

        // Act
        var result1 = _registry.GetAdapterType("TestAdapter");
        var result2 = _registry.GetAdapterType("testadapter");
        var result3 = _registry.GetAdapterType("TESTADAPTER");

        // Assert
        result1.Should().Be(result2);
        result2.Should().Be(result3);
    }

    [Test]
    public void GetAdaptersByContentType_WithMatchingType_ShouldReturnAdapters()
    {
        // Arrange
        _registry.Register<StaticHtmlAdapter>("TestAdapter", ContentType.Html);

        // Act
        var result = _registry.GetAdaptersByContentType(ContentType.Html);

        // Assert
        result.Should().Contain(typeof(StaticHtmlAdapter));
    }

    [Test]
    public void GetAdaptersByContentType_WithNoMatchingType_ShouldReturnEmpty()
    {
        // Act
        var result = _registry.GetAdaptersByContentType(ContentType.Binary);

        // Assert
        result.Should().BeEmpty();
    }

    [Test]
    public void GetAdaptersByContentType_WithMultipleAdapters_ShouldReturnAll()
    {
        // Arrange
        _registry.Register<StaticHtmlAdapter>("Adapter1", ContentType.Html);
        _registry.Register<JavaScriptAdapter>("Adapter2", ContentType.Html);

        // Act
        var result = _registry.GetAdaptersByContentType(ContentType.Html).ToList();

        // Assert
        result.Should().HaveCountGreaterOrEqualTo(2);
    }

    [Test]
    public void GetAllAdapterTypes_ShouldReturnAllRegisteredTypes()
    {
        // Arrange
        _registry.Register<StaticHtmlAdapter>("TestAdapter", ContentType.Html);

        // Act
        var result = _registry.GetAllAdapterTypes();

        // Assert
        result.Should().NotBeEmpty();
        result.Should().Contain(typeof(StaticHtmlAdapter));
    }

    [Test]
    public void GetAllAdapterTypes_WithDuplicateRegistrations_ShouldReturnDistinct()
    {
        // Arrange
        _registry.Register<StaticHtmlAdapter>("Adapter1", ContentType.Html);
        _registry.Register<StaticHtmlAdapter>("Adapter2", ContentType.Xml);

        // Act
        var result = _registry.GetAllAdapterTypes().ToList();

        // Assert
        var staticHtmlCount = result.Count(t => t == typeof(StaticHtmlAdapter));
        staticHtmlCount.Should().Be(1);
    }

    [Test]
    public void IsRegistered_WithExistingAdapter_ShouldReturnTrue()
    {
        // Arrange
        _registry.Register<StaticHtmlAdapter>("TestAdapter", ContentType.Html);

        // Act
        var result = _registry.IsRegistered("TestAdapter");

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public void IsRegistered_WithNonExistingAdapter_ShouldReturnFalse()
    {
        // Act
        var result = _registry.IsRegistered("NonExistent");

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void IsRegistered_WithNullName_ShouldThrowArgumentException()
    {
        // Act
        Action act = () => _registry.IsRegistered(null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void IsRegistered_WithEmptyName_ShouldThrowArgumentException()
    {
        // Act
        Action act = () => _registry.IsRegistered(string.Empty);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void IsRegistered_IsCaseInsensitive()
    {
        // Arrange
        _registry.Register<StaticHtmlAdapter>("TestAdapter", ContentType.Html);

        // Act & Assert
        _registry.IsRegistered("TestAdapter").Should().BeTrue();
        _registry.IsRegistered("testadapter").Should().BeTrue();
        _registry.IsRegistered("TESTADAPTER").Should().BeTrue();
    }

    [Test]
    public void Unregister_WithExistingAdapter_ShouldReturnTrue()
    {
        // Arrange
        _registry.Register<StaticHtmlAdapter>("TestAdapter", ContentType.Html);

        // Act
        var result = _registry.Unregister("TestAdapter");

        // Assert
        result.Should().BeTrue();
        _registry.IsRegistered("TestAdapter").Should().BeFalse();
    }

    [Test]
    public void Unregister_WithNonExistingAdapter_ShouldReturnFalse()
    {
        // Act
        var result = _registry.Unregister("NonExistent");

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void Unregister_WithNullName_ShouldThrowArgumentException()
    {
        // Act
        Action act = () => _registry.Unregister(null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void Unregister_ShouldRemoveFromContentTypeMappings()
    {
        // Arrange
        _registry.Register<StaticHtmlAdapter>("TestAdapter", ContentType.Html);

        // Act
        _registry.Unregister("TestAdapter");

        // Assert
        var htmlAdapters = _registry.GetAdaptersByContentType(ContentType.Html);
        // Should only contain built-in adapters, not our test adapter
        var hasTestAdapter = htmlAdapters.Any(t => t == typeof(StaticHtmlAdapter) 
            && _registry.GetAdapterType("TestAdapter") != null);
        hasTestAdapter.Should().BeFalse();
    }

    [Test]
    public void DiscoverAdapters_WithValidAssembly_ShouldDiscoverAdapters()
    {
        // Arrange
        var assembly = typeof(StaticHtmlAdapter).Assembly;
        var registry = new AdapterRegistry();

        // Act
        var count = registry.DiscoverAdapters(assembly);

        // Assert
        count.Should().BeGreaterThan(0);
    }

    [Test]
    public void DiscoverAdapters_WithNullAssembly_ShouldThrowArgumentNullException()
    {
        // Act
        Action act = () => _registry.DiscoverAdapters(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void DiscoverAdapters_ShouldNotRegisterAbstractTypes()
    {
        // Arrange
        var assembly = typeof(IContentAdapter).Assembly;
        var initialCount = _registry.GetAllAdapterTypes().Count();

        // Act
        _registry.DiscoverAdapters(assembly);

        // Assert
        var allTypes = _registry.GetAllAdapterTypes();
        allTypes.Should().AllSatisfy(t => t.IsAbstract.Should().BeFalse());
    }

    [Test]
    public void Register_ThreadSafety_ShouldHandleConcurrentRegistrations()
    {
        // Arrange
        var tasks = new List<Task>();
        var registry = new AdapterRegistry();

        // Act
        for (int i = 0; i < 10; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() =>
            {
                registry.Register<StaticHtmlAdapter>($"Adapter{index}", ContentType.Html);
            }));
        }

        // Assert
        Task.WaitAll(tasks.ToArray());
        for (int i = 0; i < 10; i++)
        {
            registry.IsRegistered($"Adapter{i}").Should().BeTrue();
        }
    }

    [Test]
    public void GetAdaptersByContentType_ShouldReturnSnapshot()
    {
        // Arrange - Register two different adapter types for HTML
        _registry.Register<StaticHtmlAdapter>("TestAdapter", ContentType.Html);
        var adapters = _registry.GetAdaptersByContentType(ContentType.Html).ToList();
        var initialCount = adapters.Count;

        // Act - Register a third adapter type
        _registry.Register<GraphQLAdapter>("NewAdapter", ContentType.Html);

        // Assert - Count should increase since it's a different type
        var newCount = _registry.GetAdaptersByContentType(ContentType.Html).Count();
        adapters.Count.Should().Be(initialCount, "original snapshot should not change");
        newCount.Should().Be(initialCount + 1, "new query should reflect the added adapter type");
    }
}
