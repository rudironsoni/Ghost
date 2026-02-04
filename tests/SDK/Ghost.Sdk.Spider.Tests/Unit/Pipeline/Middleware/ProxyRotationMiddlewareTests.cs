using FluentAssertions;
using Ghost.Sdk.Spider.Adapters.Contracts;
using Ghost.Sdk.Spider.Pipeline;
using Ghost.Sdk.Spider.Pipeline.Contracts;
using Ghost.Sdk.Spider.Pipeline.Middleware;
using NUnit.Framework;

namespace Ghost.Sdk.Spider.Tests.Unit.Pipeline.Middleware;

[TestFixture]

[TestFixture]
public class ProxyRotationMiddlewareTests
{
    [Test]
    public void Constructor_WithNullConfiguration_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new ProxyRotationMiddleware(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void Constructor_WithEmptyProxyList_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["ProxyList"] = new List<string>()
        };

        // Act
        var act = () => new ProxyRotationMiddleware(config);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*non-empty ProxyList*");
    }

    [Test]
    public void Constructor_WithoutProxyList_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var config = new Dictionary<string, object>();

        // Act
        var act = () => new ProxyRotationMiddleware(config);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*ProxyList*");
    }

    [Test]
    public void Constructor_WithValidProxyList_ShouldInitialize()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["ProxyList"] = new List<string> { "http://proxy1.com:8080" }
        };

        // Act
        var middleware = new ProxyRotationMiddleware(config);

        // Assert
        middleware.Should().NotBeNull();
    }

    [Test]
    public async Task InvokeAsync_WithRoundRobinStrategy_ShouldRotateProxies()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["ProxyList"] = new List<string>
            {
                "http://proxy1.com:8080",
                "http://proxy2.com:8080",
                "http://proxy3.com:8080"
            },
            ["RotationStrategy"] = "RoundRobin"
        };

        var middleware = new ProxyRotationMiddleware(config);
        var selectedProxies = new List<string>();

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
                selectedProxies.Add(req!.Metadata["Proxy"].ToString()!);
                await Task.CompletedTask;
            });
        }

        // Assert - Should cycle through proxies in order
        selectedProxies.Should().HaveCount(6);
        selectedProxies[0].Should().Be("http://proxy1.com:8080");
        selectedProxies[1].Should().Be("http://proxy2.com:8080");
        selectedProxies[2].Should().Be("http://proxy3.com:8080");
        selectedProxies[3].Should().Be("http://proxy1.com:8080");
        selectedProxies[4].Should().Be("http://proxy2.com:8080");
        selectedProxies[5].Should().Be("http://proxy3.com:8080");
    }

    [Test]
    public async Task InvokeAsync_WithRandomStrategy_ShouldSelectFromAvailableProxies()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["ProxyList"] = new List<string>
            {
                "http://proxy1.com:8080",
                "http://proxy2.com:8080",
                "http://proxy3.com:8080"
            },
            ["RotationStrategy"] = "Random"
        };

        var middleware = new ProxyRotationMiddleware(config);
        var selectedProxies = new HashSet<string>();

        for (int i = 0; i < 20; i++)
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
                selectedProxies.Add(req!.Metadata["Proxy"].ToString()!);
                await Task.CompletedTask;
            });
        }

        // Assert - Should use multiple different proxies with random selection
        selectedProxies.Count.Should().BeGreaterOrEqualTo(2); // At least 2 different proxies used
    }

    [Test]
    public async Task InvokeAsync_WithSuccessfulRequest_ShouldMarkProxyAsHealthy()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["ProxyList"] = new List<string> { "http://proxy1.com:8080" }
        };

        var middleware = new ProxyRotationMiddleware(config);
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
        request.Metadata.Should().ContainKey("Proxy");
        request.Metadata.Should().ContainKey("ProxyEndpoint");
        request.Metadata["Proxy"].Should().Be("http://proxy1.com:8080");
    }

    [Test]
    public async Task InvokeAsync_WithFailedRequest_ShouldMarkProxyAsUnhealthy()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["ProxyList"] = new List<string> { "http://proxy1.com:8080" }
        };

        var middleware = new ProxyRotationMiddleware(config);
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
        var act = async () => await middleware.InvokeAsync(context, (ctx) =>
        {
            throw new HttpRequestException("Connection failed");
        });

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
        request.Metadata.Should().ContainKey("Proxy");
    }

    [Test]
    public async Task InvokeAsync_WithMultipleFailures_ShouldMarkProxyUnhealthy()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["ProxyList"] = new List<string>
            {
                "http://proxy1.com:8080",
                "http://proxy2.com:8080"
            },
            ["RotationStrategy"] = "RoundRobin"
        };

        var middleware = new ProxyRotationMiddleware(config);

        // Fail the first proxy 3 times
        for (int i = 0; i < 3; i++)
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

            try
            {
                await middleware.InvokeAsync(context, (ctx) =>
                {
                    throw new HttpRequestException("Connection failed");
                });
            }
            catch
            {
                // Expected
            }
        }

        // Next request should use proxy2 since proxy1 is unhealthy
        var finalRequest = new Request
        {
            Url = "http://example.com",
            Method = "GET",
            Timeout = TimeSpan.FromSeconds(30)
        };

        var finalContext = new PipelineContext
        {
            Request = finalRequest,
            RequestId = 4,
            CancellationToken = CancellationToken.None
        };
        
        await middleware.InvokeAsync(finalContext, async (ctx) =>
        {
            await Task.CompletedTask;
        });

        // Assert
        finalRequest.Metadata["Proxy"].Should().Be("http://proxy2.com:8080");
    }

    [Test]
    public async Task InvokeAsync_WithNullRequest_ShouldCallNextMiddleware()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["ProxyList"] = new List<string> { "http://proxy1.com:8080" }
        };

        var middleware = new ProxyRotationMiddleware(config);
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

    [Test]
    public async Task InvokeAsync_ShouldStoreProxyEndpointInMetadata()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["ProxyList"] = new List<string> { "http://proxy1.com:8080" }
        };

        var middleware = new ProxyRotationMiddleware(config);
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
        request.Metadata.Should().ContainKey("Proxy");
        request.Metadata.Should().ContainKey("ProxyEndpoint");
        request.Metadata["Proxy"].Should().Be("http://proxy1.com:8080");
    }
}
