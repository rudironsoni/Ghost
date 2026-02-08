using FluentAssertions;
using Ghost.Sdk.Spider.Pipeline;
using Ghost.Sdk.Spider.Pipeline.Contracts;
using Moq;
using NUnit.Framework;

namespace Ghost.Sdk.Spider.Tests.Unit.Pipeline;

/// <summary>
/// Comprehensive tests for Pipeline components.
/// </summary>
[TestFixture]
public class PipelineTests
{
    private Mock<IPipelineMiddleware> _mockMiddleware1 = null!;
    private Mock<IPipelineMiddleware> _mockMiddleware2 = null!;

    [SetUp]
    public void Setup()
    {
        _mockMiddleware1 = new Mock<IPipelineMiddleware>();
        _mockMiddleware2 = new Mock<IPipelineMiddleware>();

        _mockMiddleware1.Setup(m => m.InvokeAsync(It.IsAny<PipelineContext>(), It.IsAny<PipelineDelegate>()))
            .Returns<PipelineContext, PipelineDelegate>(async (ctx, next) => await next(ctx));

        _mockMiddleware2.Setup(m => m.InvokeAsync(It.IsAny<PipelineContext>(), It.IsAny<PipelineDelegate>()))
            .Returns<PipelineContext, PipelineDelegate>(async (ctx, next) => await next(ctx));
    }

    [Test]
    public void PipelineContext_WithRequiredProperties_ShouldInitialize()
    {
        // Act
        var context = new PipelineContext
        {
            Request = new object(),
            RequestId = 123,
            CancellationToken = CancellationToken.None
        };

        // Assert
        context.Request.Should().NotBeNull();
        context.RequestId.Should().Be(123);
        context.CancellationToken.Should().Be(CancellationToken.None);
    }

    [Test]
    public void PipelineContext_IsCancellationRequested_ShouldReturnCorrectValue()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var context = new PipelineContext
        {
            Request = new object(),
            RequestId = 1,
            CancellationToken = cts.Token
        };

        // Act & Assert
        context.IsCancellationRequested.Should().BeFalse();

        cts.Cancel();
        context.IsCancellationRequested.Should().BeTrue();
    }

    [Test]
    public void PipelineContext_ThrowIfCancellationRequested_WhenCancelled_ShouldThrow()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var context = new PipelineContext
        {
            Request = new object(),
            RequestId = 1,
            CancellationToken = cts.Token
        };

        // Act
        Action act = () => context.ThrowIfCancellationRequested();

        // Assert
        act.Should().Throw<OperationCanceledException>();
    }

    [Test]
    public void PipelineContext_ThrowIfCancellationRequested_WhenNotCancelled_ShouldNotThrow()
    {
        // Arrange
        var context = new PipelineContext
        {
            Request = new object(),
            RequestId = 1,
            CancellationToken = CancellationToken.None
        };

        // Act
        Action act = () => context.ThrowIfCancellationRequested();

        // Assert
        act.Should().NotThrow();
    }

    [Test]
    public void PipelineContext_GetRequestAs_WithMatchingType_ShouldReturnRequest()
    {
        // Arrange
        var request = "Test request";
        var context = new PipelineContext
        {
            Request = request,
            RequestId = 1,
            CancellationToken = CancellationToken.None
        };

        // Act
        var result = context.GetRequestAs<string>();

        // Assert
        result.Should().Be(request);
    }

    [Test]
    public void PipelineContext_GetRequestAs_WithNonMatchingType_ShouldReturnNull()
    {
        // Arrange
        var request = "Test request";
        var context = new PipelineContext
        {
            Request = request,
            RequestId = 1,
            CancellationToken = CancellationToken.None
        };

        // Act
        var result = context.GetRequestAs<List<int>>();

        // Assert
        result.Should().BeNull();
    }

    [Test]
    public void PipelineContext_WithStateBox_ShouldRetainReference()
    {
        // Arrange
        var stateBox = new SpiderStateBox();
        var context = new PipelineContext
        {
            Request = new object(),
            RequestId = 1,
            CancellationToken = CancellationToken.None,
            StateBox = stateBox
        };

        // Assert
        context.StateBox.Should().BeSameAs(stateBox);
    }

    [Test]
    public void PipelineContext_WithoutStateBox_ShouldBeNull()
    {
        // Arrange
        var context = new PipelineContext
        {
            Request = new object(),
            RequestId = 1,
            CancellationToken = CancellationToken.None
        };

        // Assert
        context.StateBox.Should().BeNull();
    }

    [Test]
    public void MiddlewareConfiguration_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var config = new MiddlewareConfiguration();

        // Assert
        config.Enabled.Should().BeTrue(); // Default should be enabled
        config.Name.Should().BeNull();
    }

    [Test]
    public void MiddlewareConfiguration_WithCustomValues_ShouldRetain()
    {
        // Arrange & Act
        var config = new MiddlewareConfiguration
        {
            Name = "CustomMiddleware",
            Enabled = false
        };

        // Assert
        config.Name.Should().Be("CustomMiddleware");
        config.Enabled.Should().BeFalse();
    }

    [Test]
    public void PipelineDelegate_ShouldBeInvocable()
    {
        // Arrange
        var executed = false;
        PipelineDelegate del = ctx =>
        {
            executed = true;
            return Task.CompletedTask;
        };

        var context = new PipelineContext
        {
            Request = new object(),
            RequestId = 1,
            CancellationToken = CancellationToken.None
        };

        // Act
        del(context).Wait();

        // Assert
        executed.Should().BeTrue();
    }

    [Test]
    public async Task PipelineDelegate_ShouldChain()
    {
        // Arrange
        var executionOrder = new List<string>();

        PipelineDelegate del1 = async ctx =>
        {
            executionOrder.Add("Step1");
            await Task.CompletedTask;
        };

        PipelineDelegate del2 = async ctx =>
        {
            executionOrder.Add("Step2");
            await del1(ctx);
        };

        var context = new PipelineContext
        {
            Request = new object(),
            RequestId = 1,
            CancellationToken = CancellationToken.None
        };

        // Act
        await del2(context);

        // Assert
        executionOrder.Should().Equal("Step2", "Step1");
    }

    [Test]
    public void SpiderStateBox_ShouldInitialize()
    {
        // Act
        var stateBox = new SpiderStateBox();

        // Assert
        stateBox.Should().NotBeNull();
    }

    [Test]
    public void PipelineContext_IsStruct_ShouldBeValueType()
    {
        // Assert
        typeof(PipelineContext).IsValueType.Should().BeTrue();
    }

    [Test]
    public void PipelineContext_WithDifferentRequests_ShouldBeIndependent()
    {
        // Arrange
        var request1 = "Request 1";
        var request2 = "Request 2";

        var context1 = new PipelineContext
        {
            Request = request1,
            RequestId = 1,
            CancellationToken = CancellationToken.None
        };

        var context2 = new PipelineContext
        {
            Request = request2,
            RequestId = 2,
            CancellationToken = CancellationToken.None
        };

        // Assert
        context1.Request.Should().NotBe(context2.Request);
        context1.RequestId.Should().NotBe(context2.RequestId);
    }
}
