using FluentAssertions;
using Ghost.Sdk.Spider.Adapters;
using Ghost.Sdk.Spider.Adapters.Contracts;
using Ghost.Sdk.Spider.Adapters.GraphQL;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Xunit;
using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace Ghost.Sdk.Spider.Tests.Unit.Adapters;

/// <summary>
/// Comprehensive tests for GraphQLAdapter ExtractAsync method.
/// </summary>
public class GraphQLAdapterExtractTests : IDisposable
{
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly HttpClient _httpClient;
    private readonly GraphQLAdapter _adapter;

    public GraphQLAdapterExtractTests()
    {
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object);
        _adapter = new GraphQLAdapter(_httpClient, NullLogger<GraphQLAdapter>.Instance);
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Constructor_WithNullHttpClient_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new GraphQLAdapter(null!));
    }

    [Fact]
    public void Name_ShouldReturnGraphQL()
    {
        // Assert
        _adapter.Name.Should().Be("GraphQL");
    }

    [Fact]
    public void ContentType_ShouldReturnGraphQL()
    {
        // Assert
        _adapter.ContentType.Should().Be(ContentType.GraphQL);
    }

    [Fact]
    public void IsAvailable_ShouldReturnTrue()
    {
        // Assert
        _adapter.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task CanHandleAsync_WithNullRequest_ShouldReturnFalse()
    {
        // Act
        var result = await _adapter.CanHandleAsync(null!);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanHandleAsync_WithGraphQLContentType_ShouldReturnTrue()
    {
        // Arrange
        var request = new Request("https://example.com/graphql")
        {
            ExpectedContentType = ContentType.GraphQL
        };

        // Act
        var result = await _adapter.CanHandleAsync(request);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanHandleAsync_WithGraphQLInUrl_ShouldReturnTrue()
    {
        // Arrange
        var request = new Request("https://example.com/graphql");

        // Act
        var result = await _adapter.CanHandleAsync(request);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanHandleAsync_WithGraphQLHeader_ShouldReturnTrue()
    {
        // Arrange
        var request = new Request("https://example.com")
        {
            Headers = new Dictionary<string, string>
            {
                ["X-GraphQL-Request"] = "true"
            }
        };

        // Act
        var result = await _adapter.CanHandleAsync(request);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanHandleAsync_WithoutGraphQLIndicators_ShouldReturnFalse()
    {
        // Arrange
        var request = new Request("https://example.com/api");

        // Act
        var result = await _adapter.CanHandleAsync(request);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ExtractAsync_WithNullRequest_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _adapter.ExtractAsync(null!, new GraphQLAdapterOptions()));
    }

    [Fact]
    public async Task ExtractAsync_WithNullOptions_ShouldThrowArgumentNullException()
    {
        // Arrange
        var request = new Request("https://example.com/graphql");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _adapter.ExtractAsync(request, null!));
    }

    [Fact]
    public async Task ExtractAsync_WithBodyContainingQuery_ShouldExecuteSuccessfully()
    {
        // Arrange
        var graphQLRequest = new GraphQLRequest("query { users { id name } }");
        var request = new Request("https://example.com/graphql")
        {
            Body = JsonSerializer.Serialize(graphQLRequest)
        };

        var graphQLResponse = new GraphQLResponse
        {
            Data = new { users = new[] { new { id = "1", name = "John" } } }
        };

        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(graphQLResponse));

        // Act
        var response = await _adapter.ExtractAsync(request, new GraphQLAdapterOptions());

        // Assert
        response.Should().NotBeNull();
        response.IsSuccess.Should().BeTrue();
        response.Content.Success.Should().BeTrue();
        response.Content.Content.Should().Contain("users");
    }

    [Fact]
    public async Task ExtractAsync_WithQueryInMetadata_ShouldExecuteSuccessfully()
    {
        // Arrange
        var request = new Request("https://example.com/graphql")
        {
            Metadata = new Dictionary<string, object>
            {
                ["Query"] = "query { posts { id title } }"
            }
        };

        var graphQLResponse = new GraphQLResponse
        {
            Data = new { posts = new[] { new { id = "1", title = "Test" } } }
        };

        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(graphQLResponse));

        // Act
        var response = await _adapter.ExtractAsync(request, new GraphQLAdapterOptions());

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Content.Content.Should().Contain("posts");
    }

    [Fact]
    public async Task ExtractAsync_WithVariablesInMetadata_ShouldIncludeThem()
    {
        // Arrange
        var request = new Request("https://example.com/graphql")
        {
            Metadata = new Dictionary<string, object>
            {
                ["Query"] = "query($id: ID!) { user(id: $id) { name } }",
                ["Variables"] = new Dictionary<string, object> { ["id"] = "123" }
            }
        };

        var graphQLResponse = new GraphQLResponse
        {
            Data = new { user = new { name = "John" } }
        };

        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(graphQLResponse));

        // Act
        var response = await _adapter.ExtractAsync(request, new GraphQLAdapterOptions());

        // Assert
        response.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ExtractAsync_WithOperationNameInMetadata_ShouldIncludeIt()
    {
        // Arrange
        var request = new Request("https://example.com/graphql")
        {
            Metadata = new Dictionary<string, object>
            {
                ["Query"] = "query GetUser { user { name } }",
                ["OperationName"] = "GetUser"
            }
        };

        var graphQLResponse = new GraphQLResponse
        {
            Data = new { user = new { name = "John" } }
        };

        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(graphQLResponse));

        // Act
        var response = await _adapter.ExtractAsync(request, new GraphQLAdapterOptions());

        // Assert
        response.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ExtractAsync_WithGraphQLErrors_ShouldSetErrorInContentResult()
    {
        // Arrange
        var request = new Request("https://example.com/graphql")
        {
            Body = JsonSerializer.Serialize(new GraphQLRequest("query { invalid }"))
        };

        var graphQLResponse = new GraphQLResponse
        {
            Errors = new List<GraphQLError>
            {
                new() { Message = "Field 'invalid' doesn't exist" }
            }
        };

        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(graphQLResponse));

        // Act
        var response = await _adapter.ExtractAsync(request, new GraphQLAdapterOptions());

        // Assert
        response.Content.Success.Should().BeFalse();
        response.Content.Error.Should().Contain("Field 'invalid' doesn't exist");
    }

    [Fact]
    public async Task ExtractAsync_WithMultipleGraphQLErrors_ShouldConcatenateThem()
    {
        // Arrange
        var request = new Request("https://example.com/graphql")
        {
            Body = JsonSerializer.Serialize(new GraphQLRequest("query { test }"))
        };

        var graphQLResponse = new GraphQLResponse
        {
            Errors = new List<GraphQLError>
            {
                new() { Message = "Error 1" },
                new() { Message = "Error 2" },
                new() { Message = "Error 3" }
            }
        };

        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(graphQLResponse));

        // Act
        var response = await _adapter.ExtractAsync(request, new GraphQLAdapterOptions());

        // Assert
        response.Content.Error.Should().Contain("Error 1");
        response.Content.Error.Should().Contain("Error 2");
        response.Content.Error.Should().Contain("Error 3");
    }

    [Fact]
    public async Task ExtractAsync_WithExtensionsInResponse_ShouldAddToMetadata()
    {
        // Arrange
        var request = new Request("https://example.com/graphql")
        {
            Body = JsonSerializer.Serialize(new GraphQLRequest("query { test }"))
        };

        var extensions = new Dictionary<string, object>
        {
            ["tracing"] = new { duration = 150 }
        };

        var graphQLResponse = new GraphQLResponse
        {
            Data = new { test = true },
            Extensions = extensions
        };

        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(graphQLResponse));

        // Act
        var response = await _adapter.ExtractAsync(request, new GraphQLAdapterOptions());

        // Assert
        response.Metadata.Should().ContainKey("GraphQL.Extensions");
    }

    [Fact]
    public async Task ExtractAsync_WithRequestHeaders_ShouldIncludeThem()
    {
        // Arrange
        var request = new Request("https://example.com/graphql")
        {
            Body = JsonSerializer.Serialize(new GraphQLRequest("query { test }")),
            Headers = new Dictionary<string, string>
            {
                ["Authorization"] = "Bearer token123",
                ["X-Custom"] = "value"
            }
        };

        var graphQLResponse = new GraphQLResponse { Data = new { test = true } };
        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(graphQLResponse));

        // Act
        var response = await _adapter.ExtractAsync(request, new GraphQLAdapterOptions());

        // Assert
        response.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ExtractAsync_WithResponseHeaders_ShouldCopyToResponse()
    {
        // Arrange
        var request = new Request("https://example.com/graphql")
        {
            Body = JsonSerializer.Serialize(new GraphQLRequest("query { test }"))
        };

        var graphQLResponse = new GraphQLResponse { Data = new { test = true } };
        var httpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(graphQLResponse))
        };
        httpResponseMessage.Headers.Add("X-Response-Header", "test-value");

        SetupHttpResponseMessage(httpResponseMessage);

        // Act
        var response = await _adapter.ExtractAsync(request, new GraphQLAdapterOptions());

        // Assert
        response.Headers.Should().ContainKey("X-Response-Header");
        response.Headers["X-Response-Header"].Should().Be("test-value");
    }

    [Fact]
    public async Task ExtractAsync_WithNoQueryProvided_ShouldReturnError()
    {
        // Arrange
        var request = new Request("https://example.com/graphql")
        {
            Body = "", // Empty body
            Metadata = new Dictionary<string, object>() // No query in metadata either
        };

        // Act
        var response = await _adapter.ExtractAsync(request, new GraphQLAdapterOptions());

        // Assert
        response.IsSuccess.Should().BeFalse();
        response.Error.Should().Contain("No GraphQL query provided");
    }

    [Fact]
    public async Task ExtractAsync_WithInvalidJsonBody_ShouldReturnError()
    {
        // Arrange
        var request = new Request("https://example.com/graphql")
        {
            Body = "invalid json {["
        };

        // Act
        var response = await _adapter.ExtractAsync(request, new GraphQLAdapterOptions());

        // Assert
        response.IsSuccess.Should().BeFalse();
        response.Error.Should().Contain("JSON parsing error");
    }

    [Fact]
    public async Task ExtractAsync_WithHttpRequestException_ShouldReturnError()
    {
        // Arrange
        var request = new Request("https://example.com/graphql")
        {
            Body = JsonSerializer.Serialize(new GraphQLRequest("query { test }"))
        };

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act
        var response = await _adapter.ExtractAsync(request, new GraphQLAdapterOptions());

        // Assert
        response.IsSuccess.Should().BeFalse();
        response.Error.Should().Contain("HTTP request failed");
    }

    [Fact]
    public async Task ExtractAsync_WithInvalidGraphQLResponse_ShouldReturnError()
    {
        // Arrange
        var request = new Request("https://example.com/graphql")
        {
            Body = JsonSerializer.Serialize(new GraphQLRequest("query { test }"))
        };

        SetupHttpResponse(HttpStatusCode.OK, "invalid json response");

        // Act
        var response = await _adapter.ExtractAsync(request, new GraphQLAdapterOptions());

        // Assert
        response.IsSuccess.Should().BeFalse();
        response.Error.Should().Contain("JSON parsing error");
    }

    [Fact]
    public async Task ExtractAsync_ShouldSetCorrectContentType()
    {
        // Arrange
        var request = new Request("https://example.com/graphql")
        {
            Body = JsonSerializer.Serialize(new GraphQLRequest("query { test }"))
        };

        var graphQLResponse = new GraphQLResponse { Data = new { test = true } };
        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(graphQLResponse));

        // Act
        var response = await _adapter.ExtractAsync(request, new GraphQLAdapterOptions());

        // Assert
        response.Content.ContentType.Should().Be(ContentType.GraphQL);
        response.Content.MimeType.Should().Be("application/json");
    }

    [Fact]
    public async Task ExtractAsync_ShouldSetTimestamps()
    {
        // Arrange
        var request = new Request("https://example.com/graphql")
        {
            Body = JsonSerializer.Serialize(new GraphQLRequest("query { test }"))
        };

        var graphQLResponse = new GraphQLResponse { Data = new { test = true } };
        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(graphQLResponse));

        var before = DateTimeOffset.UtcNow;

        // Act
        var response = await _adapter.ExtractAsync(request, new GraphQLAdapterOptions());

        var after = DateTimeOffset.UtcNow;

        // Assert
        response.RequestedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        response.RespondedAt.Should().BeOnOrAfter(response.RequestedAt).And.BeOnOrBefore(after);
        response.Duration.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
    }

    [Fact]
    public async Task ExtractAsync_ShouldSetAdapterName()
    {
        // Arrange
        var request = new Request("https://example.com/graphql")
        {
            Body = JsonSerializer.Serialize(new GraphQLRequest("query { test }"))
        };

        var graphQLResponse = new GraphQLResponse { Data = new { test = true } };
        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(graphQLResponse));

        // Act
        var response = await _adapter.ExtractAsync(request, new GraphQLAdapterOptions());

        // Assert
        response.AdapterName.Should().Be("GraphQL");
    }

    [Fact]
    public async Task ExtractAsync_WithSuccessStatusCodeButGraphQLErrors_ShouldSetIsSuccessFalse()
    {
        // Arrange
        var request = new Request("https://example.com/graphql")
        {
            Body = JsonSerializer.Serialize(new GraphQLRequest("query { test }"))
        };

        var graphQLResponse = new GraphQLResponse
        {
            Data = null,
            Errors = new List<GraphQLError> { new() { Message = "Error" } }
        };

        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(graphQLResponse));

        // Act
        var response = await _adapter.ExtractAsync(request, new GraphQLAdapterOptions());

        // Assert
        response.StatusCode.Should().Be(200);
        response.IsSuccess.Should().BeFalse(); // GraphQL errors make it unsuccessful
    }

    [Fact]
    public async Task ExtractAsync_WithTimeout_ShouldRespectRequestTimeout()
    {
        // Arrange
        var request = new Request("https://example.com/graphql")
        {
            Body = JsonSerializer.Serialize(new GraphQLRequest("query { test }")),
            Timeout = TimeSpan.FromMilliseconds(10)
        };

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns(async (HttpRequestMessage req, CancellationToken ct) =>
            {
                await Task.Delay(100, ct); // Delay longer than timeout
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

        // Act
        var response = await _adapter.ExtractAsync(request, new GraphQLAdapterOptions());

        // Assert
        response.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ExtractAsync_ShouldSetStatusCodeAndReasonPhrase()
    {
        // Arrange
        var request = new Request("https://example.com/graphql")
        {
            Body = JsonSerializer.Serialize(new GraphQLRequest("query { test }"))
        };

        var graphQLResponse = new GraphQLResponse { Data = new { test = true } };
        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(graphQLResponse)),
            ReasonPhrase = "OK"
        };

        SetupHttpResponseMessage(httpResponse);

        // Act
        var response = await _adapter.ExtractAsync(request, new GraphQLAdapterOptions());

        // Assert
        response.StatusCode.Should().Be(200);
        response.ReasonPhrase.Should().Be("OK");
    }

    [Fact]
    public async Task ExtractAsync_ShouldSetFinalUrl()
    {
        // Arrange
        var url = "https://example.com/graphql";
        var request = new Request(url)
        {
            Body = JsonSerializer.Serialize(new GraphQLRequest("query { test }"))
        };

        var graphQLResponse = new GraphQLResponse { Data = new { test = true } };
        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(graphQLResponse));

        // Act
        var response = await _adapter.ExtractAsync(request, new GraphQLAdapterOptions());

        // Assert
        response.FinalUrl.Should().Be(url);
    }

    private void SetupHttpResponse(HttpStatusCode statusCode, string content)
    {
        var httpResponse = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content)
        };

        SetupHttpResponseMessage(httpResponse);
    }

    private void SetupHttpResponseMessage(HttpResponseMessage httpResponse)
    {
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);
    }
}
