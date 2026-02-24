using FluentAssertions;
using Ghost.Sdk.Middleware;
using Xunit;
using Ghost.Testing.Reliability;
using Xunit.Abstractions;

namespace Ghost.Sdk.Tests.Middleware;

/// <summary>
/// Unit tests for RedirectMiddleware.
/// </summary>
[Trait("Category", "Unit")]
public class RedirectMiddlewareTests : ReliabilityTestBase
{
    public RedirectMiddlewareTests(ITestOutputHelper output) : base(output) { }
    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new RedirectMiddleware(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("options");
    }

    [Fact]
    public async Task HandleRedirectsAsync_WithNullRequest_ThrowsArgumentNullException()
    {
        // Arrange
        var options = new RedirectOptions();
        var middleware = new RedirectMiddleware(options);

        // Act
        var act = async () => await middleware.HandleRedirectsAsync(
            null!,
            _ => Task.FromResult(new Response()),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("request");
    }

    [Fact]
    public async Task HandleRedirectsAsync_WithNullExecute_ThrowsArgumentNullException()
    {
        // Arrange
        var options = new RedirectOptions();
        var middleware = new RedirectMiddleware(options);
        var request = new Request { Url = "https://example.com" };

        // Act
        var act = async () => await middleware.HandleRedirectsAsync(
            request,
            null!,
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("execute");
    }

    [Fact]
    public async Task HandleRedirectsAsync_WithNonRedirectResponse_ReturnsResponseImmediately()
    {
        // Arrange
        var options = new RedirectOptions();
        var middleware = new RedirectMiddleware(options);
        var request = new Request { Url = "https://example.com" };
        var expectedResponse = new Response
        {
            StatusCode = 200,
            Headers = []
        };

        // Act
        var response = await middleware.HandleRedirectsAsync(
            request,
            _ => Task.FromResult(expectedResponse),
            CancellationToken.None);

        // Assert
        response.Should().BeSameAs(expectedResponse);
    }

    [Fact]
    public async Task HandleRedirectsAsync_With301Redirect_FollowsRedirect()
    {
        // Arrange
        var options = new RedirectOptions();
        var middleware = new RedirectMiddleware(options);
        var request = new Request { Url = "https://example.com/old" };
        var callCount = 0;

        // Act
        var response = await middleware.HandleRedirectsAsync(
            request,
            req =>
            {
                callCount++;
                if (callCount == 1)
                {
                    return Task.FromResult(new Response
                    {
                        StatusCode = 301,
                        Headers = new Dictionary<string, string>
                        {
                            ["Location"] = "https://example.com/new"
                        }
                    });
                }
                return Task.FromResult(new Response
                {
                    StatusCode = 200,
                    Headers = []
                });
            },
            CancellationToken.None);

        // Assert
        callCount.Should().Be(2);
        response.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task HandleRedirectsAsync_With302Redirect_FollowsRedirect()
    {
        // Arrange
        var options = new RedirectOptions();
        var middleware = new RedirectMiddleware(options);
        var request = new Request { Url = "https://example.com/temp" };
        var callCount = 0;

        // Act
        var response = await middleware.HandleRedirectsAsync(
            request,
            req =>
            {
                callCount++;
                if (callCount == 1)
                {
                    return Task.FromResult(new Response
                    {
                        StatusCode = 302,
                        Headers = new Dictionary<string, string>
                        {
                            ["Location"] = "https://example.com/found"
                        }
                    });
                }
                return Task.FromResult(new Response
                {
                    StatusCode = 200,
                    Headers = []
                });
            },
            CancellationToken.None);

        // Assert
        callCount.Should().Be(2);
        response.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task HandleRedirectsAsync_With303Redirect_FollowsRedirect()
    {
        // Arrange
        var options = new RedirectOptions();
        var middleware = new RedirectMiddleware(options);
        var request = new Request { Url = "https://example.com/post", Method = "POST" };
        var callCount = 0;

        // Act
        var response = await middleware.HandleRedirectsAsync(
            request,
            req =>
            {
                callCount++;
                if (callCount == 1)
                {
                    return Task.FromResult(new Response
                    {
                        StatusCode = 303,
                        Headers = new Dictionary<string, string>
                        {
                            ["Location"] = "https://example.com/see-other"
                        }
                    });
                }
                return Task.FromResult(new Response
                {
                    StatusCode = 200,
                    Headers = []
                });
            },
            CancellationToken.None);

        // Assert
        callCount.Should().Be(2);
        response.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task HandleRedirectsAsync_With307Redirect_FollowsRedirect()
    {
        // Arrange
        var options = new RedirectOptions();
        var middleware = new RedirectMiddleware(options);
        var request = new Request { Url = "https://example.com/resource", Method = "POST" };
        var callCount = 0;

        // Act
        var response = await middleware.HandleRedirectsAsync(
            request,
            req =>
            {
                callCount++;
                if (callCount == 1)
                {
                    return Task.FromResult(new Response
                    {
                        StatusCode = 307,
                        Headers = new Dictionary<string, string>
                        {
                            ["Location"] = "https://example.com/temp-moved"
                        }
                    });
                }
                return Task.FromResult(new Response
                {
                    StatusCode = 200,
                    Headers = []
                });
            },
            CancellationToken.None);

        // Assert
        callCount.Should().Be(2);
        response.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task HandleRedirectsAsync_With308Redirect_FollowsRedirect()
    {
        // Arrange
        var options = new RedirectOptions();
        var middleware = new RedirectMiddleware(options);
        var request = new Request { Url = "https://example.com/permanent", Method = "POST" };
        var callCount = 0;

        // Act
        var response = await middleware.HandleRedirectsAsync(
            request,
            req =>
            {
                callCount++;
                if (callCount == 1)
                {
                    return Task.FromResult(new Response
                    {
                        StatusCode = 308,
                        Headers = new Dictionary<string, string>
                        {
                            ["Location"] = "https://example.com/perm-moved"
                        }
                    });
                }
                return Task.FromResult(new Response
                {
                    StatusCode = 200,
                    Headers = []
                });
            },
            CancellationToken.None);

        // Assert
        callCount.Should().Be(2);
        response.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task HandleRedirectsAsync_WithMissingLocationHeader_ReturnsRedirectResponse()
    {
        // Arrange
        var options = new RedirectOptions();
        var middleware = new RedirectMiddleware(options);
        var request = new Request { Url = "https://example.com" };
        var expectedResponse = new Response
        {
            StatusCode = 301,
            Headers = [] // No Location header
        };

        // Act
        var response = await middleware.HandleRedirectsAsync(
            request,
            _ => Task.FromResult(expectedResponse),
            CancellationToken.None);

        // Assert
        response.Should().BeSameAs(expectedResponse);
    }

    [Fact]
    public async Task HandleRedirectsAsync_WithEmptyLocationHeader_ReturnsRedirectResponse()
    {
        // Arrange
        var options = new RedirectOptions();
        var middleware = new RedirectMiddleware(options);
        var request = new Request { Url = "https://example.com" };
        var expectedResponse = new Response
        {
            StatusCode = 302,
            Headers = new Dictionary<string, string>
            {
                ["Location"] = ""
            }
        };

        // Act
        var response = await middleware.HandleRedirectsAsync(
            request,
            _ => Task.FromResult(expectedResponse),
            CancellationToken.None);

        // Assert
        response.Should().BeSameAs(expectedResponse);
    }

    [Fact]
    public async Task HandleRedirectsAsync_WithRelativeUrl_ResolvesCorrectly()
    {
        // Arrange
        var options = new RedirectOptions();
        var middleware = new RedirectMiddleware(options);
        var request = new Request { Url = "https://example.com/path/page" };
        var capturedUrl = string.Empty;

        // Act
        await middleware.HandleRedirectsAsync(
            request,
            req =>
            {
                capturedUrl = req.Url;
                if (req.Url == "https://example.com/path/page")
                {
                    return Task.FromResult(new Response
                    {
                        StatusCode = 301,
                        Headers = new Dictionary<string, string>
                        {
                            ["Location"] = "/new-path"
                        }
                    });
                }
                return Task.FromResult(new Response
                {
                    StatusCode = 200,
                    Headers = []
                });
            },
            CancellationToken.None);

        // Assert
        capturedUrl.Should().Be("https://example.com/new-path");
    }

    [Fact]
    public async Task HandleRedirectsAsync_WithAbsoluteUrl_UsesDirectly()
    {
        // Arrange
        var options = new RedirectOptions();
        var middleware = new RedirectMiddleware(options);
        var request = new Request { Url = "https://example.com/old" };
        var capturedUrl = string.Empty;

        // Act
        await middleware.HandleRedirectsAsync(
            request,
            req =>
            {
                capturedUrl = req.Url;
                if (req.Url == "https://example.com/old")
                {
                    return Task.FromResult(new Response
                    {
                        StatusCode = 301,
                        Headers = new Dictionary<string, string>
                        {
                            ["Location"] = "https://other.com/new"
                        }
                    });
                }
                return Task.FromResult(new Response
                {
                    StatusCode = 200,
                    Headers = []
                });
            },
            CancellationToken.None);

        // Assert
        capturedUrl.Should().Be("https://other.com/new");
    }

    [Fact]
    public async Task HandleRedirectsAsync_With301PostRequest_ChangesToGet()
    {
        // Arrange
        var options = new RedirectOptions();
        var middleware = new RedirectMiddleware(options);
        var request = new Request { Url = "https://example.com/form", Method = "POST" };
        var capturedMethod = string.Empty;

        // Act
        await middleware.HandleRedirectsAsync(
            request,
            req =>
            {
                capturedMethod = req.Method;
                if (req.Url == "https://example.com/form")
                {
                    return Task.FromResult(new Response
                    {
                        StatusCode = 301,
                        Headers = new Dictionary<string, string>
                        {
                            ["Location"] = "https://example.com/result"
                        }
                    });
                }
                return Task.FromResult(new Response
                {
                    StatusCode = 200,
                    Headers = []
                });
            },
            CancellationToken.None);

        // Assert
        capturedMethod.Should().Be("GET");
    }

    [Fact]
    public async Task HandleRedirectsAsync_With302PostRequest_ChangesToGet()
    {
        // Arrange
        var options = new RedirectOptions();
        var middleware = new RedirectMiddleware(options);
        var request = new Request { Url = "https://example.com/form", Method = "POST" };
        var capturedMethod = string.Empty;

        // Act
        await middleware.HandleRedirectsAsync(
            request,
            req =>
            {
                capturedMethod = req.Method;
                if (req.Url == "https://example.com/form")
                {
                    return Task.FromResult(new Response
                    {
                        StatusCode = 302,
                        Headers = new Dictionary<string, string>
                        {
                            ["Location"] = "https://example.com/result"
                        }
                    });
                }
                return Task.FromResult(new Response
                {
                    StatusCode = 200,
                    Headers = []
                });
            },
            CancellationToken.None);

        // Assert
        capturedMethod.Should().Be("GET");
    }

    [Fact]
    public async Task HandleRedirectsAsync_With303AnyRequest_ChangesToGet()
    {
        // Arrange
        var options = new RedirectOptions();
        var middleware = new RedirectMiddleware(options);
        var request = new Request { Url = "https://example.com/form", Method = "PUT" };
        var capturedMethod = string.Empty;

        // Act
        await middleware.HandleRedirectsAsync(
            request,
            req =>
            {
                capturedMethod = req.Method;
                if (req.Url == "https://example.com/form")
                {
                    return Task.FromResult(new Response
                    {
                        StatusCode = 303,
                        Headers = new Dictionary<string, string>
                        {
                            ["Location"] = "https://example.com/result"
                        }
                    });
                }
                return Task.FromResult(new Response
                {
                    StatusCode = 200,
                    Headers = []
                });
            },
            CancellationToken.None);

        // Assert
        capturedMethod.Should().Be("GET");
    }

    [Fact]
    public async Task HandleRedirectsAsync_With307PostRequest_PreservesMethod()
    {
        // Arrange
        var options = new RedirectOptions();
        var middleware = new RedirectMiddleware(options);
        var request = new Request { Url = "https://example.com/form", Method = "POST" };
        var capturedMethod = string.Empty;

        // Act
        await middleware.HandleRedirectsAsync(
            request,
            req =>
            {
                capturedMethod = req.Method;
                if (req.Url == "https://example.com/form")
                {
                    return Task.FromResult(new Response
                    {
                        StatusCode = 307,
                        Headers = new Dictionary<string, string>
                        {
                            ["Location"] = "https://example.com/temp"
                        }
                    });
                }
                return Task.FromResult(new Response
                {
                    StatusCode = 200,
                    Headers = []
                });
            },
            CancellationToken.None);

        // Assert
        capturedMethod.Should().Be("POST");
    }

    [Fact]
    public async Task HandleRedirectsAsync_With308PostRequest_PreservesMethod()
    {
        // Arrange
        var options = new RedirectOptions();
        var middleware = new RedirectMiddleware(options);
        var request = new Request { Url = "https://example.com/form", Method = "POST" };
        var capturedMethod = string.Empty;

        // Act
        await middleware.HandleRedirectsAsync(
            request,
            req =>
            {
                capturedMethod = req.Method;
                if (req.Url == "https://example.com/form")
                {
                    return Task.FromResult(new Response
                    {
                        StatusCode = 308,
                        Headers = new Dictionary<string, string>
                        {
                            ["Location"] = "https://example.com/perm"
                        }
                    });
                }
                return Task.FromResult(new Response
                {
                    StatusCode = 200,
                    Headers = []
                });
            },
            CancellationToken.None);

        // Assert
        capturedMethod.Should().Be("POST");
    }

    [Fact]
    public async Task HandleRedirectsAsync_With301GetRequest_PreservesGet()
    {
        // Arrange
        var options = new RedirectOptions();
        var middleware = new RedirectMiddleware(options);
        var request = new Request { Url = "https://example.com/page", Method = "GET" };
        var capturedMethod = string.Empty;

        // Act
        await middleware.HandleRedirectsAsync(
            request,
            req =>
            {
                capturedMethod = req.Method;
                if (req.Url == "https://example.com/page")
                {
                    return Task.FromResult(new Response
                    {
                        StatusCode = 301,
                        Headers = new Dictionary<string, string>
                        {
                            ["Location"] = "https://example.com/new-page"
                        }
                    });
                }
                return Task.FromResult(new Response
                {
                    StatusCode = 200,
                    Headers = []
                });
            },
            CancellationToken.None);

        // Assert
        capturedMethod.Should().Be("GET");
    }

    [Fact]
    public async Task HandleRedirectsAsync_ExceedingMaxRedirects_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new RedirectOptions { MaxRedirects = 3 };
        var middleware = new RedirectMiddleware(options);
        var request = new Request { Url = "https://example.com/start" };
        var callCount = 0;

        // Act
        var act = async () => await middleware.HandleRedirectsAsync(
            request,
            req =>
            {
                callCount++;
                return Task.FromResult(new Response
                {
                    StatusCode = 301,
                    Headers = new Dictionary<string, string>
                    {
                        ["Location"] = $"https://example.com/redirect{callCount}"
                    }
                });
            },
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Maximum redirects (3) exceeded");
        callCount.Should().Be(3);
    }

    [Fact]
    public async Task HandleRedirectsAsync_WithChainedRedirects_FollowsAll()
    {
        // Arrange
        var options = new RedirectOptions { MaxRedirects = 5 };
        var middleware = new RedirectMiddleware(options);
        var request = new Request { Url = "https://example.com/start" };
        var callCount = 0;

        // Act
        var response = await middleware.HandleRedirectsAsync(
            request,
            req =>
            {
                callCount++;
                if (req.Url == "https://example.com/start")
                {
                    return Task.FromResult(new Response
                    {
                        StatusCode = 301,
                        Headers = new Dictionary<string, string>
                        {
                            ["Location"] = "https://example.com/middle1"
                        }
                    });
                }
                if (req.Url == "https://example.com/middle1")
                {
                    return Task.FromResult(new Response
                    {
                        StatusCode = 302,
                        Headers = new Dictionary<string, string>
                        {
                            ["Location"] = "https://example.com/middle2"
                        }
                    });
                }
                if (req.Url == "https://example.com/middle2")
                {
                    return Task.FromResult(new Response
                    {
                        StatusCode = 307,
                        Headers = new Dictionary<string, string>
                        {
                            ["Location"] = "https://example.com/final"
                        }
                    });
                }
                return Task.FromResult(new Response
                {
                    StatusCode = 200,
                    Headers = []
                });
            },
            CancellationToken.None);

        // Assert
        callCount.Should().Be(4);
        response.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task HandleRedirectsAsync_WithCrossSchemeDisallowed_StopsAtSchemeChange()
    {
        // Arrange
        var options = new RedirectOptions { AllowCrossScheme = false };
        var middleware = new RedirectMiddleware(options);
        var request = new Request { Url = "https://example.com/secure" };
        var expectedResponse = new Response
        {
            StatusCode = 301,
            Headers = new Dictionary<string, string>
            {
                ["Location"] = "http://example.com/insecure"
            }
        };

        // Act
        var response = await middleware.HandleRedirectsAsync(
            request,
            _ => Task.FromResult(expectedResponse),
            CancellationToken.None);

        // Assert
        response.Should().BeSameAs(expectedResponse);
    }

    [Fact]
    public async Task HandleRedirectsAsync_WithCrossSchemeAllowed_FollowsSchemeChange()
    {
        // Arrange
        var options = new RedirectOptions { AllowCrossScheme = true };
        var middleware = new RedirectMiddleware(options);
        var request = new Request { Url = "https://example.com/secure" };
        var callCount = 0;

        // Act
        var response = await middleware.HandleRedirectsAsync(
            request,
            req =>
            {
                callCount++;
                if (req.Url == "https://example.com/secure")
                {
                    return Task.FromResult(new Response
                    {
                        StatusCode = 301,
                        Headers = new Dictionary<string, string>
                        {
                            ["Location"] = "http://example.com/insecure"
                        }
                    });
                }
                return Task.FromResult(new Response
                {
                    StatusCode = 200,
                    Headers = []
                });
            },
            CancellationToken.None);

        // Assert
        callCount.Should().Be(2);
        response.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task HandleRedirectsAsync_WithSameSchemeAndCrossSchemeDisallowed_Follows()
    {
        // Arrange
        var options = new RedirectOptions { AllowCrossScheme = false };
        var middleware = new RedirectMiddleware(options);
        var request = new Request { Url = "https://example.com/old" };
        var callCount = 0;

        // Act
        var response = await middleware.HandleRedirectsAsync(
            request,
            req =>
            {
                callCount++;
                if (req.Url == "https://example.com/old")
                {
                    return Task.FromResult(new Response
                    {
                        StatusCode = 301,
                        Headers = new Dictionary<string, string>
                        {
                            ["Location"] = "https://example.com/new"
                        }
                    });
                }
                return Task.FromResult(new Response
                {
                    StatusCode = 200,
                    Headers = []
                });
            },
            CancellationToken.None);

        // Assert
        callCount.Should().Be(2);
        response.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task HandleRedirectsAsync_WithZeroMaxRedirects_ThrowsImmediately()
    {
        // Arrange
        var options = new RedirectOptions { MaxRedirects = 0 };
        var middleware = new RedirectMiddleware(options);
        var request = new Request { Url = "https://example.com/start" };

        // Act
        var act = async () => await middleware.HandleRedirectsAsync(
            request,
            _ => Task.FromResult(new Response
            {
                StatusCode = 301,
                Headers = new Dictionary<string, string>
                {
                    ["Location"] = "https://example.com/next"
                }
            }),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Maximum redirects (0) exceeded");
    }
}
