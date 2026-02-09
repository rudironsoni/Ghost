using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Ghost.Contracts.Jobs;
using Ghost.Platform.Google.Jobs;
using Ghost.Platform.Google.Jobs.Internal;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Ghost.Platform.Google.Tests;

/// <summary>
/// Integration tests for GoogleJobsApiClient covering job search, consent handling, and retry logic.
/// </summary>
public sealed class GoogleJobsApiClientIntegrationTests : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GoogleJobsApiClient> _logger;
    private readonly GoogleJobsOptions _options;
    private readonly MockHttpMessageHandler _httpMessageHandler;

    public GoogleJobsApiClientIntegrationTests()
    {
        _httpMessageHandler = new MockHttpMessageHandler();
        _httpClient = new HttpClient(_httpMessageHandler);
        _logger = new Mock<ILogger<GoogleJobsApiClient>>().Object;
        _options = new GoogleJobsOptions();
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        _httpMessageHandler?.Dispose();
    }

    [Fact]
    public async Task SearchAsync_ReturnsJobs_WhenResponseIsValid()
    {
        var validHtml = """
            <!DOCTYPE html>
            <html>
            <head></head>
            <body>
                <script type="application/ld+json">
                {
                    "@context": "https://schema.org",
                    "@type": "JobPosting",
                    "title": "Software Engineer",
                    "hiringOrganization": {
                        "@type": "Organization",
                        "name": "Tech Company"
                    },
                    "jobLocation": {
                        "@type": "Place",
                        "address": {
                            "@type": "PostalAddress",
                            "addressLocality": "San Francisco"
                        }
                    },
                    "description": "A great job opportunity",
                    "identifier": {
                        "@type": "PropertyValue",
                        "value": "job-123"
                    }
                }
                </script>
            </body>
            </html>
            """;

        _httpMessageHandler.Response = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(validHtml, Encoding.UTF8, "text/html")
        };

        var client = new GoogleJobsApiClient(_httpClient, _options, _logger);

        var result = await client.SearchAsync("software engineer", "San Francisco");

        result.Should().NotBeNull();
        result.Should().HaveCountGreaterOrEqualTo(1);
        result[0].Title.Should().Be("Software Engineer");
        result[0].Company.Should().Be("Tech Company");
        result[0].Location.Should().Be("San Francisco");
        result[0].Source.Should().Be("Google");
    }

    [Fact]
    public async Task SearchAsync_HandlesConsentPage_WithAlternativeUrls()
    {
        var consentHtml = """
            <!DOCTYPE html>
            <html>
            <head>
                <title>Before you continue to Google Search</title>
            </head>
            <body>
                <h1>Consent Required</h1>
                <p>Please accept our cookies</p>
            </body>
            </html>
            """;

        var validHtml = """
            <!DOCTYPE html>
            <html>
            <head></head>
            <body>
                <script type="application/ld+json">
                {
                    "@context": "https://schema.org",
                    "@type": "JobPosting",
                    "title": "Developer",
                    "hiringOrganization": {
                        "@type": "Organization",
                        "name": "Dev Corp"
                    },
                    "jobLocation": {
                        "@type": "Place",
                        "address": {
                            "@type": "PostalAddress",
                            "addressLocality": "Remote"
                        }
                    },
                    "description": "Remote job",
                    "identifier": {
                        "@type": "PropertyValue",
                        "value": "job-456"
                    }
                }
                </script>
            </body>
            </html>
            """;

        var callCount = 0;
        _httpMessageHandler.GetResponseFunc = (request, ct) =>
        {
            callCount++;
            if (callCount == 1)
            {
                return Task.FromResult(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(consentHtml, Encoding.UTF8, "text/html")
                });
            }
            return Task.FromResult(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(validHtml, Encoding.UTF8, "text/html")
            });
        };

        var client = new GoogleJobsApiClient(_httpClient, _options, _logger);

        var result = await client.SearchAsync("developer", "Remote");

        result.Should().NotBeNull();
        result.Should().HaveCountGreaterOrEqualTo(1);
        result[0].Title.Should().Be("Developer");
        callCount.Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task SearchAsync_ReturnsEmpty_WhenAllAttemptsReturnConsentPage()
    {
        var consentHtml = """
            <!DOCTYPE html>
            <html>
            <head>
                <title>Before you continue to Google Search</title>
            </head>
            <body>
                <h1>Consent Required</h1>
            </body>
            </html>
            """;

        _httpMessageHandler.Response = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(consentHtml, Encoding.UTF8, "text/html")
        };

        var client = new GoogleJobsApiClient(_httpClient, _options, _logger);

        var result = await client.SearchAsync("test", "test");

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_RetriesOnTransientErrors()
    {
        var validHtml = """
            <!DOCTYPE html>
            <html>
            <head></head>
            <body>
                <script type="application/ld+json">
                {
                    "@context": "https://schema.org",
                    "@type": "JobPosting",
                    "title": "Engineer",
                    "hiringOrganization": {
                        "@type": "Organization",
                        "name": "Test Company"
                    },
                    "jobLocation": {
                        "@type": "Place",
                        "address": {
                            "@type": "PostalAddress",
                            "addressLocality": "New York"
                        }
                    },
                    "description": "Test job",
                    "identifier": {
                        "@type": "PropertyValue",
                        "value": "job-789"
                    }
                }
                </script>
            </body>
            </html>
            """;

        var callCount = 0;
        _httpMessageHandler.GetResponseFunc = (request, ct) =>
        {
            callCount++;
            if (callCount <= 2)
            {
                return Task.FromResult(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.ServiceUnavailable
                });
            }
            return Task.FromResult(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(validHtml, Encoding.UTF8, "text/html")
            });
        };

        var client = new GoogleJobsApiClient(_httpClient, _options, _logger);

        var result = await client.SearchAsync("engineer", "New York");

        result.Should().NotBeNull();
        result.Should().HaveCountGreaterOrEqualTo(1);
        callCount.Should().BeGreaterThan(2);
    }

    [Fact]
    public async Task SearchAsync_HandlesEmptyResponse()
    {
        _httpMessageHandler.Response = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("", Encoding.UTF8, "text/html")
        };

        var client = new GoogleJobsApiClient(_httpClient, _options, _logger);

        var result = await client.SearchAsync("test", "test");

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_HandlesMalformedHtml()
    {
        var malformedHtml = """
            <!DOCTYPE html>
            <html>
            <head></head>
            <body>
                <div>Some content but no job data</div>
            </body>
            </html>
            """;

        _httpMessageHandler.Response = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(malformedHtml, Encoding.UTF8, "text/html")
        };

        var client = new GoogleJobsApiClient(_httpClient, _options, _logger);

        var result = await client.SearchAsync("test", "test");

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_DetectsMultipleConsentPatterns()
    {
        var consentPatterns = new[]
        {
            "consent.google.com",
            "Before you continue to Google Search",
            "We need to verify you're human",
            "Checking if the site connection is secure",
            "www.google.com/sorry/index",
            "distil_r_captcha",
            "g-recaptcha",
            "cf_chl_"
        };

        foreach (var pattern in consentPatterns)
        {
            var consentHtml = $"""
                <!DOCTYPE html>
                <html>
                <head>
                    <title>Security Check</title>
                </head>
                <body>
                    <h1>{pattern}</h1>
                </body>
                </html>
                """;

            _httpMessageHandler.Response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(consentHtml, Encoding.UTF8, "text/html")
            };

            var client = new GoogleJobsApiClient(_httpClient, _options, _logger);

            var result = await client.SearchAsync("test", "test");

            result.Should().BeEmpty($"Should detect consent pattern: {pattern}");
        }
    }

    [Fact]
    public async Task SearchAsync_ParsesMultipleJobsFromResponse()
    {
        var multipleJobsHtml = """
            <!DOCTYPE html>
            <html>
            <head></head>
            <body>
                <script type="application/ld+json">
                {
                    "@context": "https://schema.org",
                    "@type": "JobPosting",
                    "title": "Software Engineer",
                    "hiringOrganization": {
                        "@type": "Organization",
                        "name": "Company A"
                    },
                    "jobLocation": {
                        "@type": "Place",
                        "address": {
                            "@type": "PostalAddress",
                            "addressLocality": "San Francisco"
                        }
                    },
                    "description": "Job 1",
                    "identifier": {
                        "@type": "PropertyValue",
                        "value": "job-1"
                    }
                }
                </script>
                <script type="application/ld+json">
                {
                    "@context": "https://schema.org",
                    "@type": "JobPosting",
                    "title": "Data Scientist",
                    "hiringOrganization": {
                        "@type": "Organization",
                        "name": "Company B"
                    },
                    "jobLocation": {
                        "@type": "Place",
                        "address": {
                            "@type": "PostalAddress",
                            "addressLocality": "New York"
                        }
                    },
                    "description": "Job 2",
                    "identifier": {
                        "@type": "PropertyValue",
                        "value": "job-2"
                    }
                }
                </script>
            </body>
            </html>
            """;

        _httpMessageHandler.Response = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(multipleJobsHtml, Encoding.UTF8, "text/html")
        };

        var client = new GoogleJobsApiClient(_httpClient, _options, _logger);

        var result = await client.SearchAsync("software", "US");

        result.Should().NotBeNull();
        result.Should().HaveCountGreaterOrEqualTo(2);
        result.Should().Contain(j => j.Title == "Software Engineer");
        result.Should().Contain(j => j.Title == "Data Scientist");
    }

    [Fact]
    public async Task SearchAsync_UsesCorrectHeaders()
    {
        var validHtml = """
            <!DOCTYPE html>
            <html>
            <head></head>
            <body>
                <script type="application/ld+json">
                {
                    "@context": "https://schema.org",
                    "@type": "JobPosting",
                    "title": "Test Job",
                    "hiringOrganization": {
                        "@type": "Organization",
                        "name": "Test Company"
                    },
                    "jobLocation": {
                        "@type": "Place",
                        "address": {
                            "@type": "PostalAddress",
                            "addressLocality": "Test Location"
                        }
                    },
                    "description": "Test",
                    "identifier": {
                        "@type": "PropertyValue",
                        "value": "job-test"
                    }
                }
                </script>
            </body>
            </html>
            """;

        HttpRequestMessage? capturedRequest = null;
        _httpMessageHandler.GetResponseFunc = (request, ct) =>
        {
            capturedRequest = request;
            return Task.FromResult(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(validHtml, Encoding.UTF8, "text/html")
            });
        };

        var client = new GoogleJobsApiClient(_httpClient, _options, _logger);

        await client.SearchAsync("test", "test");

        capturedRequest.Should().NotBeNull();
        capturedRequest!.Headers.UserAgent.Should().NotBeEmpty();
        capturedRequest.Headers.Accept.Should().NotBeEmpty();
    }

    [Fact]
    public async Task SearchAsync_HandlesNetworkTimeout()
    {
        _httpMessageHandler.GetResponseFunc = (request, ct) =>
        {
            throw new TaskCanceledException("Request timed out");
        };

        var client = new GoogleJobsApiClient(_httpClient, _options, _logger);

        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            client.SearchAsync("test", "test"));
    }

    [Fact]
    public async Task SearchAsync_HandlesHttpRequestException()
    {
        _httpMessageHandler.GetResponseFunc = (request, ct) =>
        {
            throw new HttpRequestException("Network error");
        };

        var client = new GoogleJobsApiClient(_httpClient, _options, _logger);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.SearchAsync("test", "test"));
    }

    private sealed class MockHttpMessageHandler : HttpMessageHandler, IDisposable
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Response?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
