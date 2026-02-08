using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Ghost.Platform.Glassdoor.Internal;

namespace Ghost.Platform.Glassdoor.Tests;

/// <summary>
/// Integration tests for GlassdoorApiClient covering CSRF token, rate limiting, and error handling.
/// </summary>
public class GlassdoorApiClientIntegrationTests
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GlassdoorApiClient> _logger;
    private readonly MockHttpMessageHandler _httpMessageHandler;

    public GlassdoorApiClientIntegrationTests()
    {
        _httpMessageHandler = new MockHttpMessageHandler();
        _httpClient = new HttpClient(_httpMessageHandler);
        _logger = new Mock<ILogger<GlassdoorApiClient>>().Object;
    }

    [Fact]
    public async Task GetCsrfTokenAsync_ReturnsToken_WhenHtmlContainsToken()
    {
        var htmlWithToken = """
            <!DOCTYPE html>
            <html>
            <head>
                <script>
                    window.__INITIAL_STATE__ = {
                        "token": "gd-csrf-token-1234567890abcdef"
                    };
                </script>
            </head>
            <body></body>
            </html>
            """;

        var validValidationResponse = """
            {
                "data": {
                    "jobListings": {
                        "jobListings": [],
                        "totalJobsCount": 0
                    }
                }
            }
            """;

        var callCount = 0;
        _httpMessageHandler.GetResponseFunc = (request, ct) =>
        {
            callCount++;
            // First call is for token extraction (GET request)
            if (callCount == 1 && request.Method == HttpMethod.Get)
            {
                return Task.FromResult(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(htmlWithToken, Encoding.UTF8, "text/html")
                });
            }
            // Second call is for token validation (POST request)
            return Task.FromResult(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(validValidationResponse, Encoding.UTF8, "application/json")
            });
        };

        var client = new GlassdoorApiClient(_httpClient, _logger);

        var token = await client.GetCsrfTokenAsync();

        token.Should().NotBeNull();
        token.Should().Be("gd-csrf-token-1234567890abcdef");
    }

    [Fact]
    public async Task GetCsrfTokenAsync_ReturnsFallbackToken_WhenConsentPageDetected()
    {
        var consentHtml = """
            <!DOCTYPE html>
            <html>
            <head>
                <title>Consent Required</title>
            </head>
            <body>
                <h1>Please accept our cookies</h1>
            </body>
            </html>
            """;

        _httpMessageHandler.Response = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(consentHtml, Encoding.UTF8, "text/html")
        };

        var client = new GlassdoorApiClient(_httpClient, _logger);

        var token = await client.GetCsrfTokenAsync();

        token.Should().NotBeNull();
        token.Should().Be(GlassdoorConstants.FallbackToken);
    }

    [Fact]
    public async Task GetCsrfTokenAsync_TriesAlternativeHeaders_WhenFirstAttemptFails()
    {
        var consentHtml = """
            <!DOCTYPE html>
            <html>
            <head>
                <title>Consent Required</title>
            </head>
            <body>
                <h1>Please accept our cookies</h1>
            </body>
            </html>
            """;

        var htmlWithToken = """
            <!DOCTYPE html>
            <html>
            <head>
                <script>
                    window.__INITIAL_STATE__ = {
                        "token": "gd-csrf-token-alternative"
                    };
                </script>
            </head>
            <body></body>
            </html>
            """;

        var validValidationResponse = """
            {
                "data": {
                    "jobListings": {
                        "jobListings": [],
                        "totalJobsCount": 0
                    }
                }
            }
            """;

        var callCount = 0;
        _httpMessageHandler.GetResponseFunc = (request, ct) =>
        {
            callCount++;
            // First call: consent page (GET request)
            if (callCount == 1 && request.Method == HttpMethod.Get)
            {
                return Task.FromResult(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(consentHtml, Encoding.UTF8, "text/html")
                });
            }
            // Second call: alternative headers with token (GET request)
            if (callCount == 2 && request.Method == HttpMethod.Get)
            {
                return Task.FromResult(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(htmlWithToken, Encoding.UTF8, "text/html")
                });
            }
            // Third call: token validation (POST request)
            return Task.FromResult(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(validValidationResponse, Encoding.UTF8, "application/json")
            });
        };

        var client = new GlassdoorApiClient(_httpClient, _logger);

        var token = await client.GetCsrfTokenAsync();

        token.Should().NotBeNull();
        token.Should().Be("gd-csrf-token-alternative");
        callCount.Should().BeGreaterThan(2);
    }

    [Fact]
    public async Task SearchAsync_ReturnsJobs_WhenResponseIsValid()
    {
        var validJson = """
            {
                "data": {
                    "jobSearchResults": {
                        "jobs": [
                            {
                                "jobId": "job-123",
                                "jobTitleText": "Software Engineer",
                                "employerNameFromSearch": "Tech Company",
                                "location": "San Francisco, CA",
                                "header": {
                                    "payPeriodAdjustedPay": {
                                        "p10": 100000,
                                        "p90": 150000,
                                        "payCurrency": "USD"
                                    }
                                }
                            }
                        ],
                        "totalResults": 1
                    }
                }
            }
            """;

        _httpMessageHandler.Response = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(validJson, Encoding.UTF8, "application/json")
        };

        var client = new GlassdoorApiClient(_httpClient, _logger);

        var result = await client.SearchAsync("software engineer", "San Francisco");

        result.Should().NotBeNull();
        result.Should().Contain("Software Engineer");
    }

    [Fact]
    public async Task SearchAsync_RetriesOnRateLimitError()
    {
        var validJson = """
            {
                "data": {
                    "jobSearchResults": {
                        "jobs": [
                            {
                                "jobId": "job-123",
                                "jobTitleText": "Software Engineer",
                                "employerNameFromSearch": "Tech Company",
                                "location": "San Francisco, CA"
                            }
                        ],
                        "totalResults": 1
                    }
                }
            }
            """;

        var callCount = 0;
        _httpMessageHandler.GetResponseFunc = (request, ct) =>
        {
            callCount++;
            // Return 429 for first 2 calls, then success
            if (callCount <= 2)
            {
                return Task.FromResult(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.TooManyRequests,
                    Content = new StringContent("Rate limit exceeded", Encoding.UTF8, "text/plain")
                });
            }
            return Task.FromResult(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(validJson, Encoding.UTF8, "application/json")
            });
        };

        var client = new GlassdoorApiClient(_httpClient, _logger);
        var customToken = "custom-csrf-token-123";

        var result = await client.SearchAsync("software engineer", "San Francisco", customToken);

        result.Should().NotBeNull();
        result.Should().Contain("Software Engineer");
        callCount.Should().BeGreaterThan(2);
    }

    [Fact]
    public async Task SearchAsync_ReturnsNull_WhenAuthErrorOccurs()
    {
        var authErrorJson = """
            {
                "errors": [
                    {
                        "message": "Unauthorized access",
                        "extensions": {
                            "code": "UNAUTHORIZED"
                        }
                    }
                ]
            }
            """;

        _httpMessageHandler.Response = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(authErrorJson, Encoding.UTF8, "application/json")
        };

        var client = new GlassdoorApiClient(_httpClient, _logger);

        var result = await client.SearchAsync("software engineer", "San Francisco");

        result.Should().BeNull();
    }

    [Fact]
    public async Task SearchAsync_AppliesRateLimiting()
    {
        var validJson = """
            {
                "data": {
                    "jobSearchResults": {
                        "jobs": [
                            {
                                "jobId": "job-123",
                                "jobTitleText": "Software Engineer",
                                "employerNameFromSearch": "Tech Company",
                                "location": "San Francisco, CA"
                            }
                        ],
                        "totalResults": 1
                    }
                }
            }
            """;

        var requestTimes = new List<DateTime>();
        _httpMessageHandler.GetResponseFunc = (request, ct) =>
        {
            requestTimes.Add(DateTime.UtcNow);
            return Task.FromResult(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(validJson, Encoding.UTF8, "application/json")
            });
        };

        var client = new GlassdoorApiClient(_httpClient, _logger);
        var customToken = "custom-csrf-token-123";

        // Provide token to avoid GetCsrfTokenAsync calls
        await client.SearchAsync("software engineer", "San Francisco", customToken);
        await client.SearchAsync("data scientist", "New York", customToken);

        requestTimes.Should().HaveCount(2);
        var timeBetweenRequests = requestTimes[1] - requestTimes[0];
        // Rate limiting is set to 2 seconds in the implementation
        // Allow for slight timing variations in test environment
        timeBetweenRequests.Should().BeGreaterOrEqualTo(TimeSpan.FromMilliseconds(1900));
    }

    [Fact]
    public async Task SearchAsync_HandlesNetworkTimeout()
    {
        _httpMessageHandler.GetResponseFunc = (request, ct) =>
        {
            throw new TaskCanceledException("Request timed out");
        };

        var client = new GlassdoorApiClient(_httpClient, _logger);

        var result = await client.SearchAsync("software engineer", "San Francisco");

        result.Should().BeNull();
    }

    [Fact]
    public async Task SearchAsync_HandlesHttpRequestException()
    {
        _httpMessageHandler.GetResponseFunc = (request, ct) =>
        {
            throw new HttpRequestException("Network error");
        };

        var client = new GlassdoorApiClient(_httpClient, _logger);

        var result = await client.SearchAsync("software engineer", "San Francisco");

        result.Should().BeNull();
    }

    [Fact]
    public async Task SearchAsync_UsesProvidedCsrfToken()
    {
        var validJson = """
            {
                "data": {
                    "jobSearchResults": {
                        "jobs": [
                            {
                                "jobId": "job-123",
                                "jobTitleText": "Software Engineer",
                                "employerNameFromSearch": "Tech Company",
                                "location": "San Francisco, CA"
                            }
                        ],
                        "totalResults": 1
                    }
                }
            }
            """;

        HttpRequestMessage? capturedRequest = null;
        _httpMessageHandler.GetResponseFunc = (request, ct) =>
        {
            capturedRequest = request;
            return Task.FromResult(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(validJson, Encoding.UTF8, "application/json")
            });
        };

        var client = new GlassdoorApiClient(_httpClient, _logger);
        var customToken = "custom-csrf-token-123";

        await client.SearchAsync("software engineer", "San Francisco", customToken);

        capturedRequest.Should().NotBeNull();
        capturedRequest!.Headers.Contains("gd-csrf-token").Should().BeTrue();
    }

    [Fact]
    public async Task SearchAsync_HandlesServerError()
    {
        var validJson = """
            {
                "data": {
                    "jobSearchResults": {
                        "jobs": [
                            {
                                "jobId": "job-123",
                                "jobTitleText": "Software Engineer",
                                "employerNameFromSearch": "Tech Company",
                                "location": "San Francisco, CA"
                            }
                        ],
                        "totalResults": 1
                    }
                }
            }
            """;

        var callCount = 0;
        _httpMessageHandler.GetResponseFunc = (request, ct) =>
        {
            callCount++;
            // Return 500 for first 2 calls, then success
            if (callCount <= 2)
            {
                return Task.FromResult(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.InternalServerError,
                    Content = new StringContent("Internal server error", Encoding.UTF8, "text/plain")
                });
            }
            return Task.FromResult(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(validJson, Encoding.UTF8, "application/json")
            });
        };

        var client = new GlassdoorApiClient(_httpClient, _logger);
        var customToken = "custom-csrf-token-123";

        var result = await client.SearchAsync("software engineer", "San Francisco", customToken);

        result.Should().NotBeNull();
        result.Should().Contain("Software Engineer");
        callCount.Should().BeGreaterThan(2);
    }

    [Fact]
    public async Task SearchAsync_HandlesEmptyResponse()
    {
        _httpMessageHandler.Response = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("", Encoding.UTF8, "application/json")
        };

        var client = new GlassdoorApiClient(_httpClient, _logger);

        var result = await client.SearchAsync("software engineer", "San Francisco");

        result.Should().BeNull();
    }

    [Fact]
    public async Task SearchAsync_HandlesInvalidJson()
    {
        _httpMessageHandler.Response = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("invalid json", Encoding.UTF8, "application/json")
        };

        var client = new GlassdoorApiClient(_httpClient, _logger);

        var result = await client.SearchAsync("software engineer", "San Francisco");

        result.Should().BeNull();
    }

    [Fact]
    public async Task SearchAsync_RespectsCancellationToken()
    {
        var cts = new CancellationTokenSource();
        _httpMessageHandler.GetResponseFunc = (request, ct) =>
        {
            // This should not be called because cancellation happens before the request
            return Task.FromResult(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            });
        };

        var client = new GlassdoorApiClient(_httpClient, _logger);

        // Cancel before making the request
        cts.Cancel();

        // The cancellation happens in ApplyRateLimitAsync before the try-catch block
        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            client.SearchAsync("software engineer", "San Francisco", null, cts.Token));
    }

    [Fact]
    public void Dispose_ReleasesResources()
    {
        var client = new GlassdoorApiClient(_httpClient, _logger);

        client.Dispose();

        client.Dispose();
    }

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        public HttpResponseMessage? Response { get; set; }
        public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? GetResponseFunc { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (GetResponseFunc != null)
            {
                return await GetResponseFunc(request, cancellationToken);
            }
            return await Task.FromResult(Response ?? new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
