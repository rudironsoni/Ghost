using FluentAssertions;
using Ghost.Sdk.Spider.Adapters;
using Ghost.Sdk.Spider.Adapters.Contracts;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Newtonsoft.Json;
using Xunit;
using System.Net;

namespace Ghost.Sdk.Spider.Tests.Unit.Adapters;

/// <summary>
/// Comprehensive unit tests for GraphQL adapter.
/// </summary>
public class GraphQLAdapterComprehensiveTests : IDisposable
{
    private readonly Mock<HttpMessageHandler> _mockHandler;
    private readonly HttpClient _httpClient;

    public GraphQLAdapterComprehensiveTests()
    {
        _mockHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHandler.Object);
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }

    [Fact]
    public void GraphQLAdapter_Properties_ShouldBeCorrect()
    {
        // Arrange
        var adapter = new GraphQLAdapter(_httpClient);

        // Act & Assert
        adapter.Name.Should().Be("GraphQL");
        adapter.ContentType.Should().Be(ContentType.GraphQL);
        adapter.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public void GraphQLAdapter_Constructor_WithNullHttpClient_ShouldThrow()
    {
        // Act & Assert
        FluentActions.Invoking(() => new GraphQLAdapter(null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task GraphQLAdapter_CanHandle_WithNullRequest_ShouldReturnFalse()
    {
        // Arrange
        var adapter = new GraphQLAdapter(_httpClient);

        // Act
        var result = await adapter.CanHandleAsync(null!);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GraphQLAdapter_CanHandle_WithGraphQLContentType_ShouldReturnTrue()
    {
        // Arrange
        var adapter = new GraphQLAdapter(_httpClient);
        var request = new Request("https://api.example.com/query")
        {
            ExpectedContentType = ContentType.GraphQL
        };

        // Act
        var result = await adapter.CanHandleAsync(request);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task GraphQLAdapter_CanHandle_WithGraphQLInUrl_ShouldReturnTrue()
    {
        // Arrange
        var adapter = new GraphQLAdapter(_httpClient);
        var request = new Request("https://api.example.com/graphql");

        // Act
        var result = await adapter.CanHandleAsync(request);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task GraphQLAdapter_CanHandle_WithGraphQLHeader_ShouldReturnTrue()
    {
        // Arrange
        var adapter = new GraphQLAdapter(_httpClient);
        var request = new Request("https://api.example.com/api")
        {
            Headers = { ["X-GraphQL-Request"] = "true" }
        };

        // Act
        var result = await adapter.CanHandleAsync(request);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task GraphQLAdapter_CanHandle_WithRegularUrl_ShouldReturnFalse()
    {
        // Arrange
        var adapter = new GraphQLAdapter(_httpClient);
        var request = new Request("https://example.com/api");

        // Act
        var result = await adapter.CanHandleAsync(request);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GraphQLAdapter_Extract_WithNullRequest_ShouldThrowArgumentNullException()
    {
        // Arrange
        var adapter = new GraphQLAdapter(_httpClient);

        // Act & Assert
        await adapter.Invoking(a => a.ExtractAsync(null!))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GraphQLAdapter_Extract_WithNullOptions_ShouldThrowArgumentNullException()
    {
        // Arrange
        var adapter = new GraphQLAdapter(_httpClient);
        var request = new Request("https://api.example.com/graphql");

        // Act & Assert
        await adapter.Invoking(a => a.ExtractAsync(request, null!))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GraphQLAdapter_Extract_WithQueryInBody_ShouldSucceed()
    {
        // Arrange
        var graphQLRequest = new { query = "{ users { id name } }" };
        var request = new Request("https://api.example.com/graphql")
        {
            Body = JsonConvert.SerializeObject(graphQLRequest)
        };

        var responseData = new
        {
            data = new { users = new[] { new { id = 1, name = "John" } } }
        };

        _mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonConvert.SerializeObject(responseData))
            });

        var adapter = new GraphQLAdapter(_httpClient);

        // Act
        var response = await adapter.ExtractAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.IsSuccess.Should().BeTrue();
        response.AdapterName.Should().Be("GraphQL");
    }

    [Fact]
    public async Task GraphQLAdapter_Extract_WithQueryInMetadata_ShouldSucceed()
    {
        // Arrange
        var request = new Request("https://api.example.com/graphql");
        request.Metadata["Query"] = "{ users { id name } }";
        request.Metadata["Variables"] = new Dictionary<string, object> { ["first"] = 10 };
        request.Metadata["OperationName"] = "GetUsers";

        var responseData = new
        {
            data = new { users = new[] { new { id = 1, name = "John" } } }
        };

        _mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonConvert.SerializeObject(responseData))
            });

        var adapter = new GraphQLAdapter(_httpClient);

        // Act
        var response = await adapter.ExtractAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task GraphQLAdapter_Extract_WithGraphQLErrors_ShouldReturnError()
    {
        // Arrange
        var request = new Request("https://api.example.com/graphql")
        {
            Body = JsonConvert.SerializeObject(new { query = "{ invalid }" })
        };

        var responseData = new
        {
            errors = new[]
            {
                new { message = "Field not found", locations = new[] { new { line = 1, column = 3 } } }
            }
        };

        _mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonConvert.SerializeObject(responseData))
            });

        var adapter = new GraphQLAdapter(_httpClient);

        // Act
        var response = await adapter.ExtractAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.IsSuccess.Should().BeFalse();
        response.Content.Error.Should().Contain("GraphQL errors");
    }

    [Fact]
    public async Task GraphQLAdapter_Extract_WithNoQuery_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var request = new Request("https://api.example.com/graphql");
        var adapter = new GraphQLAdapter(_httpClient);

        // Act
        var response = await adapter.ExtractAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.IsSuccess.Should().BeFalse();
        response.Error.Should().Contain("query");
    }

    [Fact]
    public async Task GraphQLAdapter_Extract_WithHttpError_ShouldReturnError()
    {
        // Arrange
        var request = new Request("https://api.example.com/graphql")
        {
            Body = JsonConvert.SerializeObject(new { query = "{ users { id } }" })
        };

        _mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        var adapter = new GraphQLAdapter(_httpClient);

        // Act
        var response = await adapter.ExtractAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.IsSuccess.Should().BeFalse();
        response.Error.Should().Contain("HTTP request failed");
    }

    [Fact]
    public async Task GraphQLAdapter_Extract_WithExtensions_ShouldIncludeInMetadata()
    {
        // Arrange
        var request = new Request("https://api.example.com/graphql")
        {
            Body = JsonConvert.SerializeObject(new { query = "{ users { id } }" })
        };

        var responseData = new
        {
            data = new { users = new[] { new { id = 1 } } },
            extensions = new { tracing = new { duration = 123 } }
        };

        _mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonConvert.SerializeObject(responseData))
            });

        var adapter = new GraphQLAdapter(_httpClient);

        // Act
        var response = await adapter.ExtractAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.Metadata.Should().ContainKey("GraphQL.Extensions");
    }

    [Fact]
    public async Task GraphQLAdapter_Extract_WithCustomHeaders_ShouldIncludeThem()
    {
        // Arrange
        var request = new Request("https://api.example.com/graphql")
        {
            Body = JsonConvert.SerializeObject(new { query = "{ users { id } }" }),
            Headers = { ["Authorization"] = "Bearer token123" }
        };

        HttpRequestMessage? capturedRequest = null;
        _mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonConvert.SerializeObject(new { data = new { } }))
            });

        var adapter = new GraphQLAdapter(_httpClient);

        // Act
        await adapter.ExtractAsync(request);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Headers.Should().Contain(h => h.Key == "Authorization");
    }

    [Fact]
    public void GraphQLAdapter_WithLogger_ShouldNotThrow()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<GraphQLAdapter>>();

        // Act & Assert
        var adapter = new GraphQLAdapter(_httpClient, mockLogger.Object);
        adapter.Should().NotBeNull();
    }
}
