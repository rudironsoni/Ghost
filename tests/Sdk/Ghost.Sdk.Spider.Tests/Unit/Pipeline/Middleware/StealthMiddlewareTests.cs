using FluentAssertions;
using Ghost.Sdk.Spider.Adapters.Contracts;
using Ghost.Sdk.Spider.Pipeline;
using Ghost.Sdk.Spider.Pipeline.Contracts;
using Ghost.Sdk.Spider.Pipeline.Middleware;
using Xunit;
using Ghost.Testing.Reliability;
using Xunit.Abstractions;

namespace Ghost.Sdk.Spider.Tests.Unit.Pipeline.Middleware;

public class StealthMiddlewareTests : ReliabilityTestBase
{
    public StealthMiddlewareTests(ITestOutputHelper output) : base(output) { }
    [Fact]
    public void Constructor_WithNullConfiguration_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new StealthMiddleware(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithEmptyConfiguration_ShouldUseDefaults()
    {
        // Arrange
        Dictionary<string, object> config = [];

        // Act
        var middleware = new StealthMiddleware(config);

        // Assert
        middleware.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithCustomUserAgents_ShouldInitialize()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["UserAgents"] = new List<string>
            {
                "CustomUserAgent/1.0",
                "CustomUserAgent/2.0"
            }
        };

        // Act
        var middleware = new StealthMiddleware(config);

        // Assert
        middleware.Should().NotBeNull();
    }

    [Fact]
    public async Task InvokeAsync_WithFingerprintingEnabled_ShouldApplyHeaders()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["EnableFingerprinting"] = true,
            ["RandomDelay"] = false
        };

        var middleware = new StealthMiddleware(config);
        var request = new Request
        {
            Url = "http://example.com",
            Method = "GET",
            Timeout = TimeSpan.FromSeconds(30)
        };

        var context = new PipelineContext
        {
            Request = request,
            RequestId = 1,
            CancellationToken = CancellationToken.None
        };

        // Act
        await middleware.InvokeAsync(context, async (ctx) =>
        {
            await Task.CompletedTask;
        });

        // Assert
        request.Headers.Should().ContainKey("User-Agent");
        request.Headers.Should().ContainKey("Accept");
        request.Headers.Should().ContainKey("Accept-Language");
        request.Headers.Should().ContainKey("Accept-Encoding");
        request.Headers.Should().ContainKey("Connection");
        request.Headers.Should().ContainKey("Upgrade-Insecure-Requests");
        request.Headers.Should().ContainKey("Sec-Fetch-Dest");
        request.Headers.Should().ContainKey("Sec-Fetch-Mode");
        request.Headers.Should().ContainKey("Sec-Fetch-Site");
        request.Headers.Should().ContainKey("Sec-Fetch-User");

        request.Metadata.Should().ContainKey("StealthApplied");
        request.Metadata["StealthApplied"].Should().Be(true);
    }

    [Fact]
    public async Task InvokeAsync_WithFingerprintingDisabled_ShouldNotApplyHeaders()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["EnableFingerprinting"] = false,
            ["RandomDelay"] = false
        };

        var middleware = new StealthMiddleware(config);
        var request = new Request
        {
            Url = "http://example.com",
            Method = "GET",
            Timeout = TimeSpan.FromSeconds(30)
        };

        var context = new PipelineContext
        {
            Request = request,
            RequestId = 1,
            CancellationToken = CancellationToken.None
        };

        // Act
        await middleware.InvokeAsync(context, async (ctx) =>
        {
            await Task.CompletedTask;
        });

        // Assert
        request.Headers.Should().NotContainKey("User-Agent");
        request.Metadata.Should().NotContainKey("StealthApplied");
    }

    [Fact]
    public async Task InvokeAsync_WithRandomDelayEnabled_ShouldDelay()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["EnableFingerprinting"] = false,
            ["RandomDelay"] = true,
            ["MinDelayMs"] = 100,
            ["MaxDelayMs"] = 200
        };

        var middleware = new StealthMiddleware(config);
        var request = new Request
        {
            Url = "http://example.com",
            Method = "GET",
            Timeout = TimeSpan.FromSeconds(30)
        };

        var context = new PipelineContext
        {
            Request = request,
            RequestId = 1,
            CancellationToken = CancellationToken.None
        };
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        await middleware.InvokeAsync(context, async (ctx) =>
        {
            await Task.CompletedTask;
        });

        stopwatch.Stop();

        // Assert - Should have delayed at least 100ms
        stopwatch.ElapsedMilliseconds.Should().BeGreaterOrEqualTo(100);
    }

    [Fact]
    public async Task InvokeAsync_WithRandomDelayDisabled_ShouldNotDelay()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["EnableFingerprinting"] = false,
            ["RandomDelay"] = false
        };

        var middleware = new StealthMiddleware(config);
        var request = new Request
        {
            Url = "http://example.com",
            Method = "GET",
            Timeout = TimeSpan.FromSeconds(30)
        };

        var context = new PipelineContext
        {
            Request = request,
            RequestId = 1,
            CancellationToken = CancellationToken.None
        };
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        await middleware.InvokeAsync(context, async (ctx) =>
        {
            await Task.CompletedTask;
        });

        stopwatch.Stop();

        // Assert - Should complete quickly (< 50ms)
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(50);
    }

    [Fact]
    public async Task InvokeAsync_WithMultipleRequests_ShouldRotateUserAgents()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["UserAgents"] = new List<string>
            {
                "Agent1",
                "Agent2",
                "Agent3"
            },
            ["EnableFingerprinting"] = true,
            ["RandomDelay"] = false
        };

        var middleware = new StealthMiddleware(config);
        List<string> userAgents = [];

        // Act - Make 6 requests
        for (int i = 0; i < 6; i++)
        {
            var request = new Request
            {
                Url = "http://example.com",
                Method = "GET",
                Timeout = TimeSpan.FromSeconds(30)
            };

            var context = new PipelineContext
            {
                Request = request,
                RequestId = i + 1,
                CancellationToken = CancellationToken.None
            };

            await middleware.InvokeAsync(context, async (ctx) =>
            {
                var req = ctx.GetRequestAs<Request>();
                userAgents.Add(req!.Headers["User-Agent"]);
                await Task.CompletedTask;
            });
        }

        // Assert - Should cycle through user agents
        userAgents.Should().HaveCount(6);
        userAgents[0].Should().Be("Agent1");
        userAgents[1].Should().Be("Agent2");
        userAgents[2].Should().Be("Agent3");
        userAgents[3].Should().Be("Agent1");
        userAgents[4].Should().Be("Agent2");
        userAgents[5].Should().Be("Agent3");
    }

    [Fact]
    public async Task InvokeAsync_ShouldNotOverrideExistingHeaders()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["EnableFingerprinting"] = true,
            ["RandomDelay"] = false
        };

        var middleware = new StealthMiddleware(config);
        var request = new Request
        {
            Url = "http://example.com",
            Method = "GET",
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Pre-set some headers
        request.Headers["Accept"] = "application/json";
        request.Headers["Accept-Language"] = "fr-FR";

        var context = new PipelineContext
        {
            Request = request,
            RequestId = 1,
            CancellationToken = CancellationToken.None
        };

        // Act
        await middleware.InvokeAsync(context, async (ctx) =>
        {
            await Task.CompletedTask;
        });

        // Assert - Should preserve existing headers
        request.Headers["Accept"].Should().Be("application/json");
        request.Headers["Accept-Language"].Should().Be("fr-FR");

        // But should add missing headers
        request.Headers.Should().ContainKey("User-Agent");
        request.Headers.Should().ContainKey("Accept-Encoding");
    }

    [Fact]
    public async Task InvokeAsync_ShouldStoreUserAgentInMetadata()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["EnableFingerprinting"] = true,
            ["RandomDelay"] = false
        };

        var middleware = new StealthMiddleware(config);
        var request = new Request
        {
            Url = "http://example.com",
            Method = "GET",
            Timeout = TimeSpan.FromSeconds(30)
        };

        var context = new PipelineContext
        {
            Request = request,
            RequestId = 1,
            CancellationToken = CancellationToken.None
        };

        // Act
        await middleware.InvokeAsync(context, async (ctx) =>
        {
            await Task.CompletedTask;
        });

        // Assert
        request.Metadata.Should().ContainKey("UserAgent");
        request.Metadata["UserAgent"].Should().Be(request.Headers["User-Agent"]);
    }

    [Fact]
    public async Task InvokeAsync_WithNullRequest_ShouldCallNextMiddleware()
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
            Request = null!,
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
    public async Task InvokeAsync_WithCancellationToken_ShouldRespectCancellation()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["EnableFingerprinting"] = false,
            ["RandomDelay"] = true,
            ["MinDelayMs"] = 5000,
            ["MaxDelayMs"] = 6000
        };

        var middleware = new StealthMiddleware(config);
        var request = new Request
        {
            Url = "http://example.com",
            Method = "GET",
            Timeout = TimeSpan.FromSeconds(30)
        };

        var cts = new CancellationTokenSource();
        var context = new PipelineContext
        {
            Request = request,
            RequestId = 1,
            CancellationToken = cts.Token
        };

        // Act
        var task = middleware.InvokeAsync(context, async (ctx) =>
        {
            await Task.CompletedTask;
        });

        cts.Cancel();

        // Assert
        var act = async () => await task;
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task InvokeAsync_WithDefaultUserAgents_ShouldUseCommonBrowsers()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["EnableFingerprinting"] = true,
            ["RandomDelay"] = false
        };

        var middleware = new StealthMiddleware(config);
        var request = new Request
        {
            Url = "http://example.com",
            Method = "GET",
            Timeout = TimeSpan.FromSeconds(30)
        };

        var context = new PipelineContext
        {
            Request = request,
            RequestId = 1,
            CancellationToken = CancellationToken.None
        };

        // Act
        await middleware.InvokeAsync(context, async (ctx) =>
        {
            await Task.CompletedTask;
        });

        // Assert - Should use one of the default user agents
        request.Headers["User-Agent"].Should().Contain("Mozilla/5.0");
    }

    [Fact]
    public async Task InvokeAsync_WithAllFeaturesEnabled_ShouldApplyAllStealthTechniques()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["EnableFingerprinting"] = true,
            ["RandomDelay"] = true,
            ["MinDelayMs"] = 50,
            ["MaxDelayMs"] = 100,
            ["MatchTimezone"] = true
        };

        var middleware = new StealthMiddleware(config);
        var request = new Request
        {
            Url = "http://example.com",
            Method = "GET",
            Timeout = TimeSpan.FromSeconds(30)
        };

        var context = new PipelineContext
        {
            Request = request,
            RequestId = 1,
            CancellationToken = CancellationToken.None
        };
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        await middleware.InvokeAsync(context, async (ctx) =>
        {
            await Task.CompletedTask;
        });

        stopwatch.Stop();

        // Assert
        request.Headers.Should().ContainKey("User-Agent");
        request.Metadata["StealthApplied"].Should().Be(true);
        stopwatch.ElapsedMilliseconds.Should().BeGreaterOrEqualTo(50);
    }
}
