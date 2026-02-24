using FluentAssertions;
using Ghost.Sdk.Spider.Adapters;
using Ghost.Sdk.Spider.Adapters.Contracts;
using Xunit;
using System.Net.Http;
using Ghost.Testing.Reliability;
using Xunit.Abstractions;

namespace Ghost.Sdk.Spider.Tests.Unit.Adapters;

/// <summary>
/// Comprehensive tests for HttpRequestBuilder class.
/// </summary>
public class HttpRequestBuilderTests : ReliabilityTestBase
{
    public HttpRequestBuilderTests(ITestOutputHelper output) : base(output) { }
    private readonly StaticHtmlAdapterOptions _options;

    public HttpRequestBuilderTests()
    {
        _options = new StaticHtmlAdapterOptions
        {
            UserAgent = "TestAgent/1.0",
            AcceptHeader = "text/html,application/xhtml+xml",
            AcceptLanguage = "en-US,en;q=0.9",
            AcceptEncoding = "gzip, deflate",
            CustomHeaders = new Dictionary<string, string>
            {
                ["X-Custom-Header"] = "CustomValue"
            },
            Cookies = new Dictionary<string, string>
            {
                ["sessionId"] = "abc123"
            }
        };
    }

    [Fact]
    public void Constructor_WithNullRequest_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new HttpRequestBuilder(null!, _options));
    }

    [Fact]
    public void Constructor_WithNullOptions_ShouldThrowArgumentNullException()
    {
        // Arrange
        var request = new Request("https://example.com");

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new HttpRequestBuilder(request, null!));
    }

    [Fact]
    public void Build_WithSimpleGetRequest_ShouldCreateHttpRequestMessage()
    {
        // Arrange
        var request = new Request("https://example.com");
        var builder = new HttpRequestBuilder(request, _options);

        // Act
        var httpRequest = builder.Build();

        // Assert
        httpRequest.Should().NotBeNull();
        httpRequest.Method.Should().Be(HttpMethod.Get);
        httpRequest.RequestUri.Should().Be(new Uri("https://example.com"));
    }

    [Fact]
    public void Build_WithInvalidUrl_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var request = new Request("not-a-valid-url");
        var builder = new HttpRequestBuilder(request, _options);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void Build_WithPostMethod_ShouldSetCorrectHttpMethod()
    {
        // Arrange
        var request = new Request("https://example.com") { Method = "POST" };
        var builder = new HttpRequestBuilder(request, _options);

        // Act
        var httpRequest = builder.Build();

        // Assert
        httpRequest.Method.Should().Be(HttpMethod.Post);
    }

    [Fact]
    public void Build_WithPutMethod_ShouldSetCorrectHttpMethod()
    {
        // Arrange
        var request = new Request("https://example.com") { Method = "PUT" };
        var builder = new HttpRequestBuilder(request, _options);

        // Act
        var httpRequest = builder.Build();

        // Assert
        httpRequest.Method.Should().Be(HttpMethod.Put);
    }

    [Fact]
    public void Build_WithDeleteMethod_ShouldSetCorrectHttpMethod()
    {
        // Arrange
        var request = new Request("https://example.com") { Method = "DELETE" };
        var builder = new HttpRequestBuilder(request, _options);

        // Act
        var httpRequest = builder.Build();

        // Assert
        httpRequest.Method.Should().Be(HttpMethod.Delete);
    }

    [Fact]
    public void Build_WithPatchMethod_ShouldSetCorrectHttpMethod()
    {
        // Arrange
        var request = new Request("https://example.com") { Method = "PATCH" };
        var builder = new HttpRequestBuilder(request, _options);

        // Act
        var httpRequest = builder.Build();

        // Assert
        httpRequest.Method.Should().Be(HttpMethod.Patch);
    }

    [Fact]
    public void Build_WithCustomMethod_ShouldCreateCustomHttpMethod()
    {
        // Arrange
        var request = new Request("https://example.com") { Method = "CUSTOM" };
        var builder = new HttpRequestBuilder(request, _options);

        // Act
        var httpRequest = builder.Build();

        // Assert
        httpRequest.Method.Method.Should().Be("CUSTOM");
    }

    [Fact]
    public void Build_ShouldIncludeUserAgentFromOptions()
    {
        // Arrange
        var request = new Request("https://example.com");
        var builder = new HttpRequestBuilder(request, _options);

        // Act
        var httpRequest = builder.Build();

        // Assert
        httpRequest.Headers.UserAgent.ToString().Should().Contain("TestAgent/1.0");
    }

    [Fact]
    public void Build_ShouldIncludeAcceptHeaderFromOptions()
    {
        // Arrange
        var request = new Request("https://example.com");
        var builder = new HttpRequestBuilder(request, _options);

        // Act
        var httpRequest = builder.Build();

        // Assert
        httpRequest.Headers.Accept.ToString().Should().Contain("text/html");
    }

    [Fact]
    public void Build_ShouldIncludeCustomHeadersFromOptions()
    {
        // Arrange
        var request = new Request("https://example.com");
        var builder = new HttpRequestBuilder(request, _options);

        // Act
        var httpRequest = builder.Build();

        // Assert
        httpRequest.Headers.GetValues("X-Custom-Header").Should().Contain("CustomValue");
    }

    [Fact]
    public void Build_WithRequestHeaders_ShouldOverrideDefaultHeaders()
    {
        // Arrange
        var request = new Request("https://example.com")
        {
            Headers = new Dictionary<string, string>
            {
                ["X-Custom-Header"] = "OverriddenValue"
            }
        };
        var builder = new HttpRequestBuilder(request, _options);

        // Act
        var httpRequest = builder.Build();

        // Assert
        httpRequest.Headers.GetValues("X-Custom-Header").Should().Contain("OverriddenValue");
    }

    [Fact]
    public void Build_WithCookies_ShouldSetCookieHeader()
    {
        // Arrange
        var request = new Request("https://example.com");
        var builder = new HttpRequestBuilder(request, _options);

        // Act
        var httpRequest = builder.Build();

        // Assert
        httpRequest.Headers.Contains("Cookie").Should().BeTrue();
        httpRequest.Headers.GetValues("Cookie").First().Should().Contain("sessionId=abc123");
    }

    [Fact]
    public void Build_WithRequestCookies_ShouldMergeWithOptionsCookies()
    {
        // Arrange
        var request = new Request("https://example.com")
        {
            Metadata = new Dictionary<string, object>
            {
                ["Cookies"] = new Dictionary<string, string>
                {
                    ["requestCookie"] = "xyz789"
                }
            }
        };
        var builder = new HttpRequestBuilder(request, _options);

        // Act
        var httpRequest = builder.Build();

        // Assert
        var cookieHeader = httpRequest.Headers.GetValues("Cookie").First();
        cookieHeader.Should().Contain("sessionId=abc123");
        cookieHeader.Should().Contain("requestCookie=xyz789");
    }

    [Fact]
    public void Build_WithRefererInMetadata_ShouldSetRefererHeader()
    {
        // Arrange
        var request = new Request("https://example.com")
        {
            Metadata = new Dictionary<string, object>
            {
                ["Referer"] = "https://referrer.com"
            }
        };
        var builder = new HttpRequestBuilder(request, _options);

        // Act
        var httpRequest = builder.Build();

        // Assert
        httpRequest.Headers.Referrer.Should().Be(new Uri("https://referrer.com"));
    }

    [Fact]
    public void Build_WithQueryParametersInUrl_ShouldPreserveThem()
    {
        // Arrange
        var request = new Request("https://example.com?param1=value1&param2=value2");
        var builder = new HttpRequestBuilder(request, _options);

        // Act
        var httpRequest = builder.Build();

        // Assert
        httpRequest.RequestUri!.Query.Should().Contain("param1=value1");
        httpRequest.RequestUri.Query.Should().Contain("param2=value2");
    }

    [Fact]
    public void Build_WithQueryParametersInMetadata_ShouldAppendToUrl()
    {
        // Arrange
        var request = new Request("https://example.com")
        {
            Metadata = new Dictionary<string, object>
            {
                ["QueryParameters"] = new Dictionary<string, string>
                {
                    ["key1"] = "value1",
                    ["key2"] = "value2"
                }
            }
        };
        var builder = new HttpRequestBuilder(request, _options);

        // Act
        var httpRequest = builder.Build();

        // Assert
        httpRequest.RequestUri!.Query.Should().Contain("key1=value1");
        httpRequest.RequestUri.Query.Should().Contain("key2=value2");
    }

    [Fact]
    public void Build_WithBothUrlAndMetadataQueryParams_ShouldMergeThem()
    {
        // Arrange
        var request = new Request("https://example.com?existing=param")
        {
            Metadata = new Dictionary<string, object>
            {
                ["QueryParameters"] = new Dictionary<string, string>
                {
                    ["new"] = "param"
                }
            }
        };
        var builder = new HttpRequestBuilder(request, _options);

        // Act
        var httpRequest = builder.Build();

        // Assert
        httpRequest.RequestUri!.Query.Should().Contain("existing=param");
        httpRequest.RequestUri.Query.Should().Contain("new=param");
    }

    [Fact]
    public void Build_WithPostAndBody_ShouldSetContentAsJson()
    {
        // Arrange
        var request = new Request("https://example.com")
        {
            Method = "POST",
            Body = "{\"name\":\"test\"}"
        };
        var builder = new HttpRequestBuilder(request, _options);

        // Act
        var httpRequest = builder.Build();

        // Assert
        httpRequest.Content.Should().NotBeNull();
        httpRequest.Content!.Headers.ContentType!.MediaType.Should().Be("application/json");
    }

    [Fact]
    public void Build_WithPostAndXmlContentType_ShouldSetContentAsXml()
    {
        // Arrange
        var request = new Request("https://example.com")
        {
            Method = "POST",
            Body = "<root><name>test</name></root>",
            Metadata = new Dictionary<string, object>
            {
                ["ContentType"] = "application/xml"
            }
        };
        var builder = new HttpRequestBuilder(request, _options);

        // Act
        var httpRequest = builder.Build();

        // Assert
        httpRequest.Content.Should().NotBeNull();
        httpRequest.Content!.Headers.ContentType!.MediaType.Should().Be("application/xml");
    }

    [Fact]
    public void Build_WithPostAndFormData_ShouldSetFormUrlEncodedContent()
    {
        // Arrange
        var request = new Request("https://example.com")
        {
            Method = "POST",
            Body = "placeholder", // Body must be set for content to be configured
            Metadata = new Dictionary<string, object>
            {
                ["FormData"] = new Dictionary<string, string>
                {
                    ["username"] = "testuser",
                    ["password"] = "testpass"
                }
            }
        };
        var builder = new HttpRequestBuilder(request, _options);

        // Act
        var httpRequest = builder.Build();

        // Assert
        httpRequest.Content.Should().NotBeNull();
        httpRequest.Content!.Headers.ContentType!.MediaType.Should().Be("application/x-www-form-urlencoded");
    }

    [Fact]
    public void Build_WithGetMethod_ShouldNotSetContent()
    {
        // Arrange
        var request = new Request("https://example.com")
        {
            Method = "GET",
            Body = "should be ignored"
        };
        var builder = new HttpRequestBuilder(request, _options);

        // Act
        var httpRequest = builder.Build();

        // Assert
        httpRequest.Content.Should().BeNull();
    }

    [Fact]
    public void Build_WithHeadMethod_ShouldNotSetContent()
    {
        // Arrange
        var request = new Request("https://example.com")
        {
            Method = "HEAD",
            Body = "should be ignored"
        };
        var builder = new HttpRequestBuilder(request, _options);

        // Act
        var httpRequest = builder.Build();

        // Assert
        httpRequest.Content.Should().BeNull();
    }

    [Fact]
    public void Build_WithContentTypeInMetadata_ShouldUseItForContent()
    {
        // Arrange
        var request = new Request("https://example.com")
        {
            Method = "POST",
            Body = "plain text content",
            Metadata = new Dictionary<string, object>
            {
                ["ContentType"] = "text/plain"
            }
        };
        var builder = new HttpRequestBuilder(request, _options);

        // Act
        var httpRequest = builder.Build();

        // Assert
        httpRequest.Content!.Headers.ContentType!.MediaType.Should().Be("text/plain");
    }

    [Fact]
    public void Build_WithAcceptLanguageHeader_ShouldIncludeIt()
    {
        // Arrange
        var request = new Request("https://example.com");
        var builder = new HttpRequestBuilder(request, _options);

        // Act
        var httpRequest = builder.Build();

        // Assert
        httpRequest.Headers.Contains("Accept-Language").Should().BeTrue();
        var acceptLanguage = string.Join(",", httpRequest.Headers.GetValues("Accept-Language"));
        acceptLanguage.Should().Contain("en-US");
    }

    [Fact]
    public void Build_WithAcceptEncodingHeader_ShouldIncludeIt()
    {
        // Arrange
        var request = new Request("https://example.com");
        var builder = new HttpRequestBuilder(request, _options);

        // Act
        var httpRequest = builder.Build();

        // Assert
        httpRequest.Headers.Contains("Accept-Encoding").Should().BeTrue();
        var acceptEncoding = string.Join(",", httpRequest.Headers.GetValues("Accept-Encoding"));
        acceptEncoding.Should().Contain("gzip");
    }

    [Fact]
    public void Build_WithLowercaseMethod_ShouldMapCorrectly()
    {
        // Arrange
        var request = new Request("https://example.com") { Method = "post" };
        var builder = new HttpRequestBuilder(request, _options);

        // Act
        var httpRequest = builder.Build();

        // Assert
        httpRequest.Method.Should().Be(HttpMethod.Post);
    }

    [Fact]
    public void Build_WithMixedCaseMethod_ShouldMapCorrectly()
    {
        // Arrange
        var request = new Request("https://example.com") { Method = "PoSt" };
        var builder = new HttpRequestBuilder(request, _options);

        // Act
        var httpRequest = builder.Build();

        // Assert
        httpRequest.Method.Should().Be(HttpMethod.Post);
    }
}
