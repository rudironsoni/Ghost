using FluentAssertions;
using Ghost.Sdk.Spider.Adapters.Contracts;
using Ghost.Sdk.Spider.Pipeline;
using Ghost.Sdk.Spider.Pipeline.Contracts;
using Ghost.Sdk.Spider.Pipeline.Middleware;
using Xunit;
using Ghost.Testing.Reliability;
using Xunit.Abstractions;

namespace Ghost.Sdk.Spider.Tests.Unit.Pipeline.Middleware;

/// <summary>
/// Comprehensive tests for StealthMiddleware covering edge cases and stealth techniques.
/// </summary>
public class StealthMiddlewareFullTests : ReliabilityTestBase
{
    public StealthMiddlewareFullTests(ITestOutputHelper output) : base(output) { }
    [Fact]
    public void Constructor_WithEmptyUserAgentList_ShouldUseDefaults()
    {
        // Arrange & Act
        var config = new Dictionary<string, object>
        {
            ["UserAgents"] = []
        };
        var middleware = new StealthMiddleware(config);

        // Assert
        middleware.Should().NotBeNull();
    }

    [Fact]
    public async Task InvokeAsync_WithSingleUserAgent_ShouldReuseSameAgent()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["UserAgents"] = new List<string> { "SingleAgent/1.0" },
            ["EnableFingerprinting"] = true,
            ["RandomDelay"] = false
        };
        var middleware = new StealthMiddleware(config);
        List<string> userAgents = [];

        // Act - Make 3 requests
        for (int i = 0; i < 3; i++)
        {
            var request = new Request { Url = "http://test.com", Method = "GET", Timeout = TimeSpan.FromSeconds(30) };
            var context = new PipelineContext { Request = request, RequestId = i + 1, CancellationToken = CancellationToken.None };

            await middleware.InvokeAsync(context, async (ctx) =>
            {
                var req = ctx.GetRequestAs<Request>();
                userAgents.Add(req!.Headers["User-Agent"]);
                await Task.CompletedTask;
            });
        }

        // Assert
        userAgents.Should().AllBe("SingleAgent/1.0");
    }

    [Fact]
    public async Task InvokeAsync_WithRequestTypeOtherThanRequest_ShouldHandleGracefully()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["EnableFingerprinting"] = true,
            ["RandomDelay"] = false
        };
        var middleware = new StealthMiddleware(config);
        var context = new PipelineContext
        {
            Request = "NotARequestObject",
            RequestId = 1,
            CancellationToken = CancellationToken.None
        };
        var nextCalled = false;

        // Act
        await middleware.InvokeAsync(context, async (ctx) =>
        {
            nextCalled = true;
            await Task.CompletedTask;
        });

        // Assert
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WithExtremeDelayValues_ShouldHandleCorrectly()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["EnableFingerprinting"] = false,
            ["RandomDelay"] = true,
            ["MinDelayMs"] = 1,
            ["MaxDelayMs"] = 2
        };
        var middleware = new StealthMiddleware(config);
        var request = new Request { Url = "http://test.com", Method = "GET", Timeout = TimeSpan.FromSeconds(30) };
        var context = new PipelineContext { Request = request, RequestId = 1, CancellationToken = CancellationToken.None };

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        await middleware.InvokeAsync(context, async (ctx) => await Task.CompletedTask);
        stopwatch.Stop();

        // Assert - Should have minimal delay
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(50);
    }

    [Fact]
    public async Task InvokeAsync_WithAllSecFetchHeaders_ShouldApplyAll()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["EnableFingerprinting"] = true,
            ["RandomDelay"] = false
        };
        var middleware = new StealthMiddleware(config);
        var request = new Request { Url = "http://test.com", Method = "GET", Timeout = TimeSpan.FromSeconds(30) };
        var context = new PipelineContext { Request = request, RequestId = 1, CancellationToken = CancellationToken.None };

        // Act
        await middleware.InvokeAsync(context, async (ctx) => await Task.CompletedTask);

        // Assert
        request.Headers["Sec-Fetch-Dest"].Should().Be("document");
        request.Headers["Sec-Fetch-Mode"].Should().Be("navigate");
        request.Headers["Sec-Fetch-Site"].Should().Be("none");
        request.Headers["Sec-Fetch-User"].Should().Be("?1");
    }

    [Fact]
    public async Task InvokeAsync_WithPresetSecFetchHeaders_ShouldNotOverride()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["EnableFingerprinting"] = true,
            ["RandomDelay"] = false
        };
        var middleware = new StealthMiddleware(config);
        var request = new Request { Url = "http://test.com", Method = "GET", Timeout = TimeSpan.FromSeconds(30) };
        request.Headers["Sec-Fetch-Dest"] = "iframe";
        request.Headers["Sec-Fetch-Mode"] = "cors";

        var context = new PipelineContext { Request = request, RequestId = 1, CancellationToken = CancellationToken.None };

        // Act
        await middleware.InvokeAsync(context, async (ctx) => await Task.CompletedTask);

        // Assert - Should preserve custom values
        request.Headers["Sec-Fetch-Dest"].Should().Be("iframe");
        request.Headers["Sec-Fetch-Mode"].Should().Be("cors");
    }

    [Fact]
    public async Task InvokeAsync_ConcurrentRequests_ShouldRotateUserAgentsSafely()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["UserAgents"] = new List<string> { "Agent1", "Agent2", "Agent3" },
            ["EnableFingerprinting"] = true,
            ["RandomDelay"] = false
        };
        var middleware = new StealthMiddleware(config);
        var userAgents = new System.Collections.Concurrent.ConcurrentBag<string>();

        // Act - Make concurrent requests
        var tasks = Enumerable.Range(0, 10).Select(async i =>
        {
            var request = new Request { Url = "http://test.com", Method = "GET", Timeout = TimeSpan.FromSeconds(30) };
            var context = new PipelineContext { Request = request, RequestId = i + 1, CancellationToken = CancellationToken.None };

            await middleware.InvokeAsync(context, async (ctx) =>
            {
                var req = ctx.GetRequestAs<Request>();
                userAgents.Add(req!.Headers["User-Agent"]);
                await Task.CompletedTask;
            });
        });

        await Task.WhenAll(tasks);

        // Assert - All should be valid agents
        userAgents.Should().HaveCount(10);
        userAgents.Should().OnlyContain(ua => ua == "Agent1" || ua == "Agent2" || ua == "Agent3");
    }

    [Fact]
    public async Task InvokeAsync_WithMatchTimezoneEnabled_ShouldInitialize()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["MatchTimezone"] = true,
            ["EnableFingerprinting"] = false,
            ["RandomDelay"] = false
        };
        var middleware = new StealthMiddleware(config);
        var request = new Request { Url = "http://test.com", Method = "GET", Timeout = TimeSpan.FromSeconds(30) };
        var context = new PipelineContext { Request = request, RequestId = 1, CancellationToken = CancellationToken.None };
        var nextCalled = false;

        // Act
        await middleware.InvokeAsync(context, async (ctx) =>
        {
            nextCalled = true;
            await Task.CompletedTask;
        });

        // Assert
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WithCompleteCustomConfiguration_ShouldApplyAll()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["UserAgents"] = new List<string> { "CustomAgent/1.0" },
            ["MatchTimezone"] = false,
            ["RandomDelay"] = true,
            ["MinDelayMs"] = 10,
            ["MaxDelayMs"] = 20,
            ["EnableFingerprinting"] = true
        };
        var middleware = new StealthMiddleware(config);
        var request = new Request { Url = "http://test.com", Method = "GET", Timeout = TimeSpan.FromSeconds(30) };
        var context = new PipelineContext { Request = request, RequestId = 1, CancellationToken = CancellationToken.None };

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        await middleware.InvokeAsync(context, async (ctx) => await Task.CompletedTask);
        stopwatch.Stop();

        // Assert
        request.Headers["User-Agent"].Should().Be("CustomAgent/1.0");
        request.Metadata["StealthApplied"].Should().Be(true);
        stopwatch.ElapsedMilliseconds.Should().BeGreaterOrEqualTo(10);
    }

    [Fact]
    public async Task InvokeAsync_WithVeryLargeUserAgentList_ShouldRotateCorrectly()
    {
        // Arrange
        var largeList = Enumerable.Range(1, 100).Select(i => $"Agent{i}").ToList();
        var config = new Dictionary<string, object>
        {
            ["UserAgents"] = largeList,
            ["EnableFingerprinting"] = true,
            ["RandomDelay"] = false
        };
        var middleware = new StealthMiddleware(config);
        List<string> userAgents = [];

        // Act - Make 105 requests (to test wrap-around)
        for (int i = 0; i < 105; i++)
        {
            var request = new Request { Url = "http://test.com", Method = "GET", Timeout = TimeSpan.FromSeconds(30) };
            var context = new PipelineContext { Request = request, RequestId = i + 1, CancellationToken = CancellationToken.None };

            await middleware.InvokeAsync(context, async (ctx) =>
            {
                var req = ctx.GetRequestAs<Request>();
                userAgents.Add(req!.Headers["User-Agent"]);
                await Task.CompletedTask;
            });
        }

        // Assert - Should cycle correctly
        userAgents[0].Should().Be("Agent1");
        userAgents[99].Should().Be("Agent100");
        userAgents[100].Should().Be("Agent1"); // Wrap around
        userAgents[104].Should().Be("Agent5");
    }

    [Fact]
    public async Task InvokeAsync_WithAllHeadersPreset_ShouldOnlySetUserAgent()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["EnableFingerprinting"] = true,
            ["RandomDelay"] = false
        };
        var middleware = new StealthMiddleware(config);
        var request = new Request { Url = "http://test.com", Method = "GET", Timeout = TimeSpan.FromSeconds(30) };

        // Preset all headers
        request.Headers["Accept"] = "custom-accept";
        request.Headers["Accept-Language"] = "custom-lang";
        request.Headers["Accept-Encoding"] = "custom-encoding";
        request.Headers["Connection"] = "custom-connection";
        request.Headers["Upgrade-Insecure-Requests"] = "0";
        request.Headers["Sec-Fetch-Dest"] = "custom-dest";
        request.Headers["Sec-Fetch-Mode"] = "custom-mode";
        request.Headers["Sec-Fetch-Site"] = "custom-site";
        request.Headers["Sec-Fetch-User"] = "custom-user";

        var context = new PipelineContext { Request = request, RequestId = 1, CancellationToken = CancellationToken.None };

        // Act
        await middleware.InvokeAsync(context, async (ctx) => await Task.CompletedTask);

        // Assert - All custom headers should be preserved
        request.Headers["Accept"].Should().Be("custom-accept");
        request.Headers["Accept-Language"].Should().Be("custom-lang");
        request.Headers["Accept-Encoding"].Should().Be("custom-encoding");
        request.Headers["Connection"].Should().Be("custom-connection");
        request.Headers["Upgrade-Insecure-Requests"].Should().Be("0");
        request.Headers["Sec-Fetch-Dest"].Should().Be("custom-dest");
        request.Headers["Sec-Fetch-Mode"].Should().Be("custom-mode");
        request.Headers["Sec-Fetch-Site"].Should().Be("custom-site");
        request.Headers["Sec-Fetch-User"].Should().Be("custom-user");

        // Only User-Agent should be set by middleware
        request.Headers["User-Agent"].Should().Contain("Mozilla");
    }
}
