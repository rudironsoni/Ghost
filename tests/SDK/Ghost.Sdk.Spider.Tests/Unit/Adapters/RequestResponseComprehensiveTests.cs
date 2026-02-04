using FluentAssertions;
using Ghost.Sdk.Spider.Adapters.Contracts;
using NUnit.Framework;

namespace Ghost.Sdk.Spider.Tests.Unit.Adapters;

/// <summary>
/// Comprehensive tests for Request and Response classes.
/// </summary>
[TestFixture]
public class RequestResponseComprehensiveTests
{
    [Test]
    public void Request_Constructor_WithUrl_ShouldSetUrl()
    {
        // Arrange & Act
        var request = new Request("https://example.com");

        // Assert
        request.Url.Should().Be("https://example.com");
        request.Method.Should().Be("GET");
        request.Headers.Should().NotBeNull();
        request.Metadata.Should().NotBeNull();
    }

    [Test]

    public void Request_SetMethod_ShouldWork()
    {
        // Arrange
        var request = new Request("https://example.com")
        {
            Method = "POST"
        };

        // Act & Assert
        request.Method.Should().Be("POST");
    }

    [Test]
    public void Request_SetBody_ShouldWork()
    {
        // Arrange
        var request = new Request("https://example.com")
        {
            Body = "{\"key\":\"value\"}"
        };

        // Act & Assert
        request.Body.Should().Be("{\"key\":\"value\"}");
    }

    [Test]
    public void Request_AddHeaders_ShouldWork()
    {
        // Arrange
        var request = new Request("https://example.com");

        // Act
        request.Headers["Authorization"] = "Bearer token";
        request.Headers["Accept"] = "application/json";

        // Assert
        request.Headers.Should().HaveCount(2);
        request.Headers["Authorization"].Should().Be("Bearer token");
    }

    [Test]
    public void Request_AddMetadata_ShouldWork()
    {
        // Arrange
        var request = new Request("https://example.com");

        // Act
        request.Metadata["Priority"] = 10;
        request.Metadata["Category"] = "Products";

        // Assert
        request.Metadata.Should().HaveCount(2);
        request.Metadata["Priority"].Should().Be(10);
    }

    [Test]
    public void Request_SetTimeout_ShouldWork()
    {
        // Arrange
        var request = new Request("https://example.com")
        {
            Timeout = TimeSpan.FromSeconds(60)
        };

        // Act & Assert
        request.Timeout.Should().Be(TimeSpan.FromSeconds(60));
    }

    [Test]
    public void Request_SetExpectedContentType_ShouldWork()
    {
        // Arrange
        var request = new Request("https://example.com")
        {
            ExpectedContentType = ContentType.GraphQL
        };

        // Act & Assert
        request.ExpectedContentType.Should().Be(ContentType.GraphQL);
    }

    [Test]
    public void Request_DefaultExpectedContentType_ShouldBeUnknown()
    {
        // Arrange & Act
        var request = new Request("https://example.com");

        // Assert
        request.ExpectedContentType.Should().Be(ContentType.Unknown);
    }

    [Test]
    public void Response_Constructor_WithContentResult_ShouldSetProperties()
    {
        // Arrange
        var contentResult = new ContentResult
        {
            Content = "<html></html>",
            ContentType = ContentType.StaticHtml,
            Success = true
        };

        // Act
        var response = new Response(contentResult);

        // Assert
        response.Content.Should().Be(contentResult);
        response.Content.Content.Should().Be("<html></html>");
        response.Content.Success.Should().BeTrue();
    }

    [Test]
    public void Response_SetStatusCode_ShouldWork()
    {
        // Arrange
        var contentResult = ContentResult.CreateSuccess("<html></html>", ContentType.StaticHtml);
        var response = new Response(contentResult)
        {
            StatusCode = 200
        };

        // Act & Assert
        response.StatusCode.Should().Be(200);
    }

    [Test]
    public void Response_SetIsSuccess_ShouldWork()
    {
        // Arrange
        var contentResult = ContentResult.CreateSuccess("<html></html>", ContentType.StaticHtml);
        var response = new Response(contentResult)
        {
            IsSuccess = true
        };

        // Act & Assert
        response.IsSuccess.Should().BeTrue();
    }

    [Test]
    public void Response_SetFinalUrl_ShouldWork()
    {
        // Arrange
        var contentResult = ContentResult.CreateSuccess("<html></html>", ContentType.StaticHtml);
        var response = new Response(contentResult)
        {
            FinalUrl = "https://example.com/final"
        };

        // Act & Assert
        response.FinalUrl.Should().Be("https://example.com/final");
    }

    [Test]
    public void Response_Duration_ShouldCalculateCorrectly()
    {
        // Arrange
        var contentResult = ContentResult.CreateSuccess("<html></html>", ContentType.StaticHtml);
        var now = DateTimeOffset.UtcNow;
        var response = new Response(contentResult)
        {
            RequestedAt = now,
            RespondedAt = now.AddSeconds(2)
        };

        // Act & Assert
        response.Duration.Should().BeCloseTo(TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(10));
    }

    [Test]
    public void Response_AddHeaders_ShouldWork()
    {
        // Arrange
        var contentResult = ContentResult.CreateSuccess("<html></html>", ContentType.StaticHtml);
        var response = new Response(contentResult);

        // Act
        response.Headers["Content-Type"] = "text/html";
        response.Headers["Server"] = "nginx";

        // Assert
        response.Headers.Should().HaveCount(2);
        response.Headers["Server"].Should().Be("nginx");
    }

    [Test]
    public void Response_AddMetadata_ShouldWork()
    {
        // Arrange
        var contentResult = ContentResult.CreateSuccess("<html></html>", ContentType.StaticHtml);
        var response = new Response(contentResult);

        // Act
        response.Metadata["ExtractedItems"] = 42;
        response.Metadata["ProcessingTime"] = 1.5;

        // Assert
        response.Metadata.Should().HaveCount(2);
        response.Metadata["ExtractedItems"].Should().Be(42);
    }

    [Test]
    public void ContentResult_CreateSuccess_ShouldSetProperties()
    {
        // Act
        var result = ContentResult.CreateSuccess("<html></html>", ContentType.StaticHtml);

        // Assert
        result.Content.Should().Be("<html></html>");
        result.ContentType.Should().Be(ContentType.StaticHtml);
        result.Success.Should().BeTrue();
        result.Error.Should().BeNullOrEmpty();
    }

    [Test]
    public void ContentResult_CreateFailure_ShouldSetProperties()
    {
        // Act
        var result = ContentResult.CreateFailure("Network error", ContentType.StaticHtml);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Be("Network error");
        result.ContentType.Should().Be(ContentType.StaticHtml);
    }

    [Test]
    public void ContentResult_SetContentLength_ShouldWork()
    {
        // Arrange
        var result = new ContentResult
        {
            Content = "test content",
            ContentLength = 12
        };

        // Act & Assert
        result.ContentLength.Should().Be(12);
    }

    [Test]
    public void ContentResult_SetMimeType_ShouldWork()
    {
        // Arrange
        var result = new ContentResult
        {
            MimeType = "application/json"
        };

        // Act & Assert
        result.MimeType.Should().Be("application/json");
    }

    [Test]
    public void ContentResult_SetEncoding_ShouldWork()
    {
        // Arrange
        var result = new ContentResult
        {
            Encoding = "utf-8"
        };

        // Act & Assert
        result.Encoding.Should().Be("utf-8");
    }

    [Test]
    public void ContentResult_SetExtractedAt_ShouldWork()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var result = new ContentResult
        {
            ExtractedAt = now
        };

        // Act & Assert
        result.ExtractedAt.Should().Be(now);
    }

    [Test]
    public void ContentType_Enum_ShouldHaveCorrectValues()
    {
        // Act & Assert
        ContentType.Unknown.Should().Be(ContentType.Unknown);
        ContentType.StaticHtml.Should().Be(ContentType.StaticHtml);
        ContentType.JavaScript.Should().Be(ContentType.JavaScript);
        ContentType.GraphQL.Should().Be(ContentType.GraphQL);
        ContentType.WebSocket.Should().Be(ContentType.WebSocket);
    }
    [Test]

    public void Response_SetAdapterName_ShouldWork()
    {
        // Arrange
        var contentResult = ContentResult.CreateSuccess("<html></html>", ContentType.StaticHtml);
        var response = new Response(contentResult)
        {
            AdapterName = "StaticHtml"
        };

        // Act & Assert
        response.AdapterName.Should().Be("StaticHtml");
    }

    [Test]
    public void Response_SetReasonPhrase_ShouldWork()
    {
        // Arrange
        var contentResult = ContentResult.CreateSuccess("<html></html>", ContentType.StaticHtml);
        var response = new Response(contentResult)
        {
            ReasonPhrase = "OK"
        };

        // Act & Assert
        response.ReasonPhrase.Should().Be("OK");
    }

    [Test]
    public void Response_SetError_ShouldWork()
    {
        // Arrange
        var contentResult = ContentResult.CreateFailure("Error", ContentType.StaticHtml);
        var response = new Response(contentResult)
        {
            Error = "Custom error message"
        };

        // Act & Assert
        response.Error.Should().Be("Custom error message");
    }

    [Test]
    public void Response_SetException_ShouldWork()
    {
        // Arrange
        var contentResult = ContentResult.CreateFailure("Error", ContentType.StaticHtml);
        var exception = new InvalidOperationException("Test exception");
        var response = new Response(contentResult)
        {
            Exception = exception
        };

        // Act & Assert
        response.Exception.Should().Be(exception);
        response.Exception!.Message.Should().Be("Test exception");
    }
}
