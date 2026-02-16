using FluentAssertions;
using Ghost.Sdk.Spider.Pipeline;
using Ghost.Sdk.Spider.Pipeline.Compilation;
using Ghost.Sdk.Spider.Pipeline.Contracts;
using Xunit;

namespace Ghost.Sdk.Spider.Tests.Unit.Pipeline;

public class PipelineExecutionTests
{
    [Fact]
    public async Task Build_WithMultipleMiddleware_ExecutesInOrder()
    {
        // Arrange
        List<string> executionOrder = [];
        var builder = new PipelineBuilder()
            .Use(async (ctx, next) =>
            {
                executionOrder.Add("First-Before");
                await next(ctx);
                executionOrder.Add("First-After");
            })
            .Use(async (ctx, next) =>
            {
                executionOrder.Add("Second-Before");
                await next(ctx);
                executionOrder.Add("Second-After");
            })
            .Use(async (ctx, next) =>
            {
                executionOrder.Add("Third");
                await next(ctx);
            });

        var pipeline = builder.Build();
        var context = new PipelineContext
        {
            Request = new object(),
            RequestId = 1,
            CancellationToken = CancellationToken.None
        };

        // Act
        await pipeline.ExecuteAsync(context);

        // Assert
        executionOrder.Should().Equal(
            "First-Before",
            "Second-Before",
            "Third",
            "Second-After",
            "First-After"
        );
    }

    [Fact]
    public async Task Build_WithMiddleware_ModifiesStateBox()
    {
        // Arrange
        var stateBox = new SpiderStateBox();
        var builder = new PipelineBuilder()
            .Use(async (ctx, next) =>
            {
                ctx.StateBox?.Properties.TryAdd("key1", "value1");
                await next(ctx);
            })
            .Use(async (ctx, next) =>
            {
                ctx.StateBox?.Properties.TryAdd("key2", "value2");
                await next(ctx);
            });

        var pipeline = builder.Build();
        var context = new PipelineContext
        {
            Request = new object(),
            RequestId = 1,
            CancellationToken = CancellationToken.None,
            StateBox = stateBox
        };

        // Act
        await pipeline.ExecuteAsync(context);

        // Assert
        stateBox.Properties["key1"].Should().Be("value1");
        stateBox.Properties["key2"].Should().Be("value2");
    }

    [Fact]
    public async Task Build_WithMiddlewareThatShortCircuits_StopsExecution()
    {
        // Arrange
        List<string> executionOrder = [];
        var builder = new PipelineBuilder()
            .Use(async (ctx, next) =>
            {
                executionOrder.Add("First");
                await next(ctx);
            })
            .Use(async (ctx, next) =>
            {
                executionOrder.Add("Second-ShortCircuit");
                // Don't call next - short circuit the pipeline
                await Task.CompletedTask;
            })
            .Use(async (ctx, next) =>
            {
                executionOrder.Add("Third-NotCalled");
                await next(ctx);
            });

        var pipeline = builder.Build();
        var context = new PipelineContext
        {
            Request = new object(),
            RequestId = 1,
            CancellationToken = CancellationToken.None
        };

        // Act
        await pipeline.ExecuteAsync(context);

        // Assert
        executionOrder.Should().Equal("First", "Second-ShortCircuit");
        executionOrder.Should().NotContain("Third-NotCalled");
    }

    [Fact]
    public void Build_WithEmptyPipeline_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new PipelineBuilder();

        // Act
        var action = () => builder.Build();

        // Assert - Pipeline requires at least one middleware
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*at least one middleware*");
    }

    [Fact]
    public async Task Build_WithSingleMiddleware_ExecutesMiddleware()
    {
        // Arrange
        var executed = false;
        var builder = new PipelineBuilder()
            .Use(async (ctx, next) =>
            {
                executed = true;
                await next(ctx);
            });

        var pipeline = builder.Build();
        var context = new PipelineContext
        {
            Request = new object(),
            RequestId = 1,
            CancellationToken = CancellationToken.None
        };

        // Act
        await pipeline.ExecuteAsync(context);

        // Assert
        executed.Should().BeTrue();
    }

    [Fact]
    public async Task Build_WithFactoryMiddleware_CreatesAndExecutes()
    {
        // Arrange
        var executed = false;
        var builder = new PipelineBuilder()
            .Use(() => new TestMiddleware(() => executed = true));

        var pipeline = builder.Build();
        var context = new PipelineContext
        {
            Request = new object(),
            RequestId = 1,
            CancellationToken = CancellationToken.None
        };

        // Act
        await pipeline.ExecuteAsync(context);

        // Assert
        executed.Should().BeTrue();
    }

    [Fact]
    public async Task Build_WithNamedMiddleware_ExecutesSuccessfully()
    {
        // Arrange
        var executed = false;
        var builder = new PipelineBuilder()
            .Use(new TestMiddleware(() => executed = true), "TestMiddleware");

        var pipeline = builder.Build();
        var context = new PipelineContext
        {
            Request = new object(),
            RequestId = 1,
            CancellationToken = CancellationToken.None
        };

        // Act
        await pipeline.ExecuteAsync(context);

        // Assert
        executed.Should().BeTrue();
    }

    [Fact]
    public async Task Build_WithConfiguredMiddleware_ExecutesSuccessfully()
    {
        // Arrange
        var executed = false;
        var builder = new PipelineBuilder()
            .Use(
                new TestMiddleware(() => executed = true),
                MiddlewareConfiguration.WithName("Test")
            );

        var pipeline = builder.Build();
        var context = new PipelineContext
        {
            Request = new object(),
            RequestId = 1,
            CancellationToken = CancellationToken.None
        };

        // Act
        await pipeline.ExecuteAsync(context);

        // Assert
        executed.Should().BeTrue();
    }

    [Fact]
    public void StateBox_Properties_StoresAndRetrievesValues()
    {
        // Arrange
        var stateBox = new SpiderStateBox();

        // Act
        stateBox.Properties["string"] = "value";
        stateBox.Properties["int"] = 42;
        stateBox.Properties["bool"] = true;

        // Assert
        stateBox.Properties["string"].Should().Be("value");
        stateBox.Properties["int"].Should().Be(42);
        stateBox.Properties["bool"].Should().Be(true);
    }

    [Fact]
    public void StateBox_IncrementRequestCount_IncrementsCounter()
    {
        // Arrange
        var stateBox = new SpiderStateBox();

        // Act
        stateBox.IncrementRequestCount();
        stateBox.IncrementRequestCount();

        // Assert
        stateBox.RequestCount.Should().Be(2);
    }

    // Test helper classes
    private sealed class TestMiddleware : IPipelineMiddleware
    {
        private readonly Action _onExecute;

        public TestMiddleware(Action onExecute)
        {
            _onExecute = onExecute;
        }

        public async Task InvokeAsync(PipelineContext context, PipelineDelegate next)
        {
            _onExecute();
            await next(context);
        }
    }
}
