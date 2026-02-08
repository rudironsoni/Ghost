using FluentAssertions;
using Ghost.Sdk.Spider.Adapters;
using Ghost.Sdk.Spider.Adapters.Contracts;
using Ghost.Sdk.Spider.Adapters.GraphQL;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Newtonsoft.Json;
using Xunit;
using System.Net;

namespace Ghost.Sdk.Spider.Tests.Integration;

/// <summary>
/// Comprehensive tests for GraphQL pagination scenarios including Relay-style cursor pagination.
/// </summary>
public class GraphQLPaginationTests : IDisposable
{
    private readonly Mock<ILogger<GraphQLAdapter>> _mockLogger;
    private readonly Mock<HttpMessageHandler> _mockHttpHandler;
    private readonly HttpClient _httpClient;

    public GraphQLPaginationTests()
    {
        _mockLogger = new Mock<ILogger<GraphQLAdapter>>();
        _mockHttpHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpHandler.Object);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    [Fact]
    public async Task ExtractAsync_WithRelayStylePagination_ShouldHandlePageInfo()
    {
        // Arrange
        var graphQLResponse = new GraphQLResponse
        {
            Data = new
            {
                users = new
                {
                    edges = new[]
                    {
                        new { node = new { id = "1", name = "User 1" }, cursor = "cursor1" }
                    },
                    pageInfo = new
                    {
                        hasNextPage = true,
                        endCursor = "cursor1"
                    }
                }
            }
        };

        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonConvert.SerializeObject(graphQLResponse))
            });

        var adapter = new GraphQLAdapter(_httpClient, _mockLogger.Object);
        var query = "query { users(first: 10) { edges { node { id name } cursor } pageInfo { hasNextPage endCursor } } }";
        var request = new Request("https://api.example.com/graphql")
        {
            Body = JsonConvert.SerializeObject(new GraphQLRequest(query))
        };

        // Act
        var response = await adapter.ExtractAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.IsSuccess.Should().BeTrue();
        response.Content.Content.Should().Contain("pageInfo");
        response.Content.Content.Should().Contain("hasNextPage");
    }

    [Fact]
    public async Task ExtractAsync_WithCursorBasedPagination_ShouldSupportAfterParameter()
    {
        // Arrange
        var graphQLResponse = GraphQLResponse.Success(new
        {
            items = new[]
            {
                new { id = "2", name = "Item 2" }
            }
        });

        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonConvert.SerializeObject(graphQLResponse))
            });

        var adapter = new GraphQLAdapter(_httpClient, _mockLogger.Object);
        var query = "query($after: String) { items(first: 10, after: $after) { id name } }";
        var graphQLRequest = new GraphQLRequest(query)
            .WithVariable("after", "cursor1");

        var request = new Request("https://api.example.com/graphql")
        {
            Body = JsonConvert.SerializeObject(graphQLRequest)
        };

        // Act
        var response = await adapter.ExtractAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ExtractAsync_WithOffsetBasedPagination_ShouldSupportLimitOffset()
    {
        // Arrange
        var graphQLResponse = GraphQLResponse.Success(new
        {
            users = new[]
            {
                new { id = "1", name = "User 1" },
                new { id = "2", name = "User 2" }
            },
            totalCount = 100
        });

        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonConvert.SerializeObject(graphQLResponse))
            });

        var adapter = new GraphQLAdapter(_httpClient, _mockLogger.Object);
        var query = "query($limit: Int, $offset: Int) { users(limit: $limit, offset: $offset) { id name } totalCount }";
        var graphQLRequest = new GraphQLRequest(query)
            .WithVariable("limit", 10)
            .WithVariable("offset", 20);

        var request = new Request("https://api.example.com/graphql")
        {
            Body = JsonConvert.SerializeObject(graphQLRequest)
        };

        // Act
        var response = await adapter.ExtractAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.IsSuccess.Should().BeTrue();
        response.Content.Content.Should().Contain("totalCount");
    }

    [Fact]
    public async Task ExtractAsync_WithPageNumberPagination_ShouldSupportPageAndPageSize()
    {
        // Arrange
        var graphQLResponse = GraphQLResponse.Success(new
        {
            items = new[]
            {
                new { id = "1", name = "Item 1" }
            },
            pageInfo = new
            {
                currentPage = 2,
                totalPages = 10,
                pageSize = 10
            }
        });

        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonConvert.SerializeObject(graphQLResponse))
            });

        var adapter = new GraphQLAdapter(_httpClient, _mockLogger.Object);
        var query = "query($page: Int, $pageSize: Int) { items(page: $page, pageSize: $pageSize) { id name } pageInfo { currentPage totalPages } }";
        var graphQLRequest = new GraphQLRequest(query)
            .WithVariable("page", 2)
            .WithVariable("pageSize", 10);

        var request = new Request("https://api.example.com/graphql")
        {
            Body = JsonConvert.SerializeObject(graphQLRequest)
        };

        // Act
        var response = await adapter.ExtractAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ExtractAsync_WithEmptyPage_ShouldReturnEmptyData()
    {
        // Arrange
        var graphQLResponse = GraphQLResponse.Success(new
        {
            items = Array.Empty<object>(),
            pageInfo = new
            {
                hasNextPage = false
            }
        });

        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonConvert.SerializeObject(graphQLResponse))
            });

        var adapter = new GraphQLAdapter(_httpClient, _mockLogger.Object);
        var request = new Request("https://api.example.com/graphql")
        {
            Body = JsonConvert.SerializeObject(new GraphQLRequest("query { items { id } }"))
        };

        // Act
        var response = await adapter.ExtractAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ExtractAsync_WithMultiplePaginatedFields_ShouldHandleComplex()
    {
        // Arrange
        var graphQLResponse = GraphQLResponse.Success(new
        {
            users = new
            {
                edges = new[] { new { node = new { id = "1" } } },
                pageInfo = new { hasNextPage = true }
            },
            posts = new
            {
                edges = new[] { new { node = new { id = "p1" } } },
                pageInfo = new { hasNextPage = false }
            }
        });

        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonConvert.SerializeObject(graphQLResponse))
            });

        var adapter = new GraphQLAdapter(_httpClient, _mockLogger.Object);
        var query = @"
            query {
                users(first: 10) { edges { node { id } } pageInfo { hasNextPage } }
                posts(first: 10) { edges { node { id } } pageInfo { hasNextPage } }
            }";
        var request = new Request("https://api.example.com/graphql")
        {
            Body = JsonConvert.SerializeObject(new GraphQLRequest(query))
        };

        // Act
        var response = await adapter.ExtractAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.IsSuccess.Should().BeTrue();
        response.Content.Content.Should().Contain("users");
        response.Content.Content.Should().Contain("posts");
    }

    [Fact]
    public async Task ExtractAsync_WithPartialErrorsInPagination_ShouldHandleGracefully()
    {
        // Arrange
        var graphQLResponse = new GraphQLResponse
        {
            Data = new
            {
                items = new[]
                {
                    new { id = "1", name = "Item 1" }
                }
            },
            Errors = new List<GraphQLError>
            {
                new() { Message = "Field 'deprecated' is deprecated" }
            }
        };

        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonConvert.SerializeObject(graphQLResponse))
            });

        var adapter = new GraphQLAdapter(_httpClient, _mockLogger.Object);
        var request = new Request("https://api.example.com/graphql")
        {
            Body = JsonConvert.SerializeObject(new GraphQLRequest("query { items { id name } }"))
        };

        // Act
        var response = await adapter.ExtractAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.IsSuccess.Should().BeFalse();
        response.Content.Error.Should().Contain("deprecated");
    }

    [Fact]
    public async Task ExtractAsync_WithInfiniteScrollPattern_ShouldSupportContinuousLoading()
    {
        // Arrange
        var graphQLResponse = GraphQLResponse.Success(new
        {
            feed = new
            {
                items = new[] { new { id = "1" }, new { id = "2" }, new { id = "3" } },
                hasMore = true,
                nextCursor = "cursor_abc123"
            }
        });

        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonConvert.SerializeObject(graphQLResponse))
            });

        var adapter = new GraphQLAdapter(_httpClient, _mockLogger.Object);
        var query = "query($cursor: String) { feed(cursor: $cursor) { items { id } hasMore nextCursor } }";
        var graphQLRequest = new GraphQLRequest(query)
            .WithVariable("cursor", "previous_cursor");

        var request = new Request("https://api.example.com/graphql")
        {
            Body = JsonConvert.SerializeObject(graphQLRequest)
        };

        // Act
        var response = await adapter.ExtractAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.IsSuccess.Should().BeTrue();
        response.Content.Content.Should().Contain("hasMore");
        response.Content.Content.Should().Contain("nextCursor");
    }
}
