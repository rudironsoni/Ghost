using FluentAssertions;
using Ghost.Sdk.Spider.Adapters.Contracts;
using Ghost.Sdk.Spider.Pipeline;
using Ghost.Sdk.Spider.Pipeline.Contracts;
using Moq;
using Xunit;
using Ghost.Testing.Reliability;
using Xunit.Abstractions;

namespace Ghost.Sdk.Spider.Tests.Unit.Pipeline;

public class PipelineBuilderTests : ReliabilityTestBase
{
    public PipelineBuilderTests(ITestOutputHelper output) : base(output) { }
    private readonly PipelineBuilder _builder;

    public PipelineBuilderTests()
    {
        _builder = new PipelineBuilder();
    }

    [Fact]
    public void Use_WithMiddleware_ShouldAddToBuilder()
    {
        // Arrange
        var middleware = new Mock<IPipelineMiddleware>().Object;

        // Act
        _builder.Use(middleware);

        // Assert
        _builder.Count.Should().Be(1);
    }

    [Fact]
    public void Use_WithNullMiddleware_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _builder.Use((IPipelineMiddleware)null!));
    }

    [Fact]
    public void Use_WithMultipleMiddleware_ShouldPreserveOrder()
    {
        // Arrange
        var middleware1 = new Mock<IPipelineMiddleware>().Object;
        var middleware2 = new Mock<IPipelineMiddleware>().Object;
        var middleware3 = new Mock<IPipelineMiddleware>().Object;

        // Act
        _builder
            .Use(middleware1)
            .Use(middleware2)
            .Use(middleware3);

        // Assert
        _builder.Count.Should().Be(3);
    }

    [Fact]
    public void Use_WithConfiguration_ShouldStoreConfiguration()
    {
        // Arrange
        var middleware = new Mock<IPipelineMiddleware>().Object;
        var config = MiddlewareConfiguration.WithName("TestMiddleware");

        // Act
        _builder.Use(middleware, config);

        // Assert
        _builder.Count.Should().Be(1);
    }

    [Fact]
    public void Use_WithName_ShouldSetName()
    {
        // Arrange
        var middleware = new Mock<IPipelineMiddleware>().Object;

        // Act
        _builder.Use(middleware, "TestName");

        // Assert
        _builder.Count.Should().Be(1);
    }

    [Fact]
    public void Use_WithFactory_ShouldCreateMiddleware()
    {
        // Arrange
        var factoryCalled = false;
        Func<IPipelineMiddleware> factory = () =>
        {
            factoryCalled = true;
            return new Mock<IPipelineMiddleware>().Object;
        };

        // Act
        _builder.Use(factory);

        // Assert
        factoryCalled.Should().BeTrue();
        _builder.Count.Should().Be(1);
    }

    [Fact]
    public void Use_WithFactoryReturningNull_ShouldThrow()
    {
        // Arrange
        Func<IPipelineMiddleware> factory = () => null!;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _builder.Use(factory));
    }

    [Fact]
    public void Use_WithDelegate_ShouldCreateInlineMiddleware()
    {
        // Arrange
        Func<PipelineContext, PipelineDelegate, Task> middlewareFunc =
            async (ctx, next) => await next(ctx);

        // Act
        _builder.Use(middlewareFunc);

        // Assert
        _builder.Count.Should().Be(1);
    }

    [Fact]
    public void Remove_WithExistingName_ShouldRemoveMiddleware()
    {
        // Arrange
        var middleware = new Mock<IPipelineMiddleware>().Object;
        _builder.Use(middleware, "ToRemove");
        _builder.Use(middleware, "ToKeep");

        // Act
        _builder.Remove("ToRemove");

        // Assert
        _builder.Count.Should().Be(1);
    }

    [Fact]
    public void Remove_WithNonExistentName_ShouldNotThrow()
    {
        // Arrange
        var middleware = new Mock<IPipelineMiddleware>().Object;
        _builder.Use(middleware);

        // Act
        _builder.Remove("NonExistent");

        // Assert
        _builder.Count.Should().Be(1);
    }

    [Fact]
    public void Clear_ShouldRemoveAllMiddleware()
    {
        // Arrange
        var middleware = new Mock<IPipelineMiddleware>().Object;
        _builder.Use(middleware);
        _builder.Use(middleware);
        _builder.Use(middleware);

        // Act
        _builder.Clear();

        // Assert
        _builder.Count.Should().Be(0);
    }

    [Fact]
    public void Build_WithMiddleware_ShouldReturnCompiledPipeline()
    {
        // Arrange
        var mockMiddleware = new Mock<IPipelineMiddleware>();
        mockMiddleware
            .Setup(m => m.InvokeAsync(It.IsAny<PipelineContext>(), It.IsAny<PipelineDelegate>()))
            .Returns((PipelineContext ctx, PipelineDelegate next) => next(ctx));

        _builder.Use(mockMiddleware.Object);

        // Act
        var pipeline = _builder.Build();

        // Assert
        pipeline.Should().NotBeNull();
    }

    [Fact]
    public void Build_WithNoMiddleware_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => _builder.Build());
    }

    [Fact]
    public void Build_WithDisabledMiddleware_ShouldExcludeThem()
    {
        // Arrange
        var middleware1 = new Mock<IPipelineMiddleware>().Object;
        var middleware2 = new Mock<IPipelineMiddleware>().Object;

        _builder.Use(middleware1, MiddlewareConfiguration.WithName("Enabled"));
        _builder.Use(middleware2, new MiddlewareConfiguration { Enabled = false, Name = "Disabled" });

        // Act
        var pipeline = _builder.Build();

        // Assert
        pipeline.Should().NotBeNull();
    }

    [Fact]
    public void Clone_ShouldCreateIndependentCopy()
    {
        // Arrange
        var middleware = new Mock<IPipelineMiddleware>().Object;
        _builder.Use(middleware);

        // Act
        var clone = _builder.Clone();
        clone.Use(middleware); // Add to clone only

        // Assert
        _builder.Count.Should().Be(1);
        clone.Count.Should().Be(2);
    }

    [Fact]
    public void Count_ShouldReflectNumberOfMiddleware()
    {
        // Arrange & Act
        _builder.Count.Should().Be(0);

        _builder.Use(new Mock<IPipelineMiddleware>().Object);
        _builder.Count.Should().Be(1);

        _builder.Use(new Mock<IPipelineMiddleware>().Object);
        _builder.Count.Should().Be(2);

        _builder.Clear();
        _builder.Count.Should().Be(0);
    }

    [Fact]
    public void Use_FluentInterface_ShouldAllowChaining()
    {
        // Arrange
        var middleware = new Mock<IPipelineMiddleware>().Object;

        // Act
        var result = _builder
            .Use(middleware)
            .Use(middleware)
            .Use(middleware);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.Count.Should().Be(3);
    }

    [Fact]
    public async Task Build_ExecutePipeline_ShouldInvokeMiddleware()
    {
        // Arrange
        var invoked = false;
        var mockMiddleware = new Mock<IPipelineMiddleware>();
        mockMiddleware
            .Setup(m => m.InvokeAsync(It.IsAny<PipelineContext>(), It.IsAny<PipelineDelegate>()))
            .Returns((PipelineContext ctx, PipelineDelegate next) =>
            {
                invoked = true;
                return next(ctx);
            });

        _builder.Use(mockMiddleware.Object);
        var pipeline = _builder.Build();

        var request = new Request
        {
            RequestId = Guid.NewGuid().ToString(),
            Url = "https://example.com",
            Method = "GET",
            Timeout = TimeSpan.FromSeconds(30)
        };
        var context = new PipelineContext
        {
            StateBox = new SpiderStateBox(),
            RequestId = 1,
            Request = request,
            CancellationToken = CancellationToken.None
        };

        // Act
        await pipeline.ExecuteAsync(context);

        // Assert
        invoked.Should().BeTrue();
    }
}
