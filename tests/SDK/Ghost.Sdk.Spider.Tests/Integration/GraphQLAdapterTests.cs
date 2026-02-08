using FluentAssertions;
using Ghost.Sdk.Spider.Adapters;
using Ghost.Sdk.Spider.Adapters.Contracts;
using Ghost.Sdk.Spider.Adapters.GraphQL;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Xunit;
using WireMock.Server;
using WireMockRequest = WireMock.RequestBuilders.Request;
using WireMockResponse = WireMock.ResponseBuilders.Response;

namespace Ghost.Sdk.Spider.Tests.Integration;

/// <summary>
/// Integration tests for GraphQLAdapter using WireMock.Net for HTTP mocking.
/// </summary>
public class GraphQLAdapterTests : IDisposable
{
    private readonly WireMockServer _server;
    private readonly HttpClient _httpClient;
    private readonly GraphQLAdapter _adapter;

    public GraphQLAdapterTests()
    {
        _server = WireMockServer.Start();
        _httpClient = new HttpClient();
        _adapter = new GraphQLAdapter(_httpClient, NullLogger<GraphQLAdapter>.Instance);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _server.Stop();
        _server.Dispose();
    }

    #region Query Execution

    [Fact]
    public async Task ExtractAsync_WithSimpleQuery_ShouldExecuteSuccessfully()
    {
        // Arrange
        const string query = "{ user(id: 1) { id name email } }";
        var graphQLRequest = new GraphQLRequest(query);

        var mockResponse = new
        {
            data = new
            {
                user = new
                {
                    id = 1,
                    name = "John Doe",
                    email = "john@example.com"
                }
            }
        };

        _server
            .Given(WireMockRequest.Create()
                .WithPath("/graphql")
                .UsingPost())
            .RespondWith(WireMockResponse.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonConvert.SerializeObject(mockResponse)));

        var request = new Request
        {
            RequestId = Guid.NewGuid().ToString(),
            Url = $"{_server.Url}/graphql",
            Method = "POST",
            Body = JsonConvert.SerializeObject(graphQLRequest),
            ExpectedContentType = ContentType.GraphQL,
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Act
        var response = await _adapter.ExtractAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.IsSuccess.Should().BeTrue();
        response.StatusCode.Should().Be(200);
        response.Content.Content.Should().Contain("John Doe");
        response.Content.ContentType.Should().Be(ContentType.GraphQL);
        response.AdapterName.Should().Be("GraphQL");
    }

    [Fact]
    public async Task ExtractAsync_WithMutation_ShouldExecuteSuccessfully()
    {
        // Arrange
        const string mutation = "mutation { createUser(name: \"Jane\", email: \"jane@example.com\") { id name } }";
        var graphQLRequest = new GraphQLRequest(mutation);

        var mockResponse = new
        {
            data = new
            {
                createUser = new
                {
                    id = 2,
                    name = "Jane"
                }
            }
        };

        _server
            .Given(WireMockRequest.Create()
                .WithPath("/graphql")
                .UsingPost())
            .RespondWith(WireMockResponse.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonConvert.SerializeObject(mockResponse)));

        var request = new Request
        {
            RequestId = Guid.NewGuid().ToString(),
            Url = $"{_server.Url}/graphql",
            Method = "POST",
            Body = JsonConvert.SerializeObject(graphQLRequest),
            ExpectedContentType = ContentType.GraphQL,
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Act
        var response = await _adapter.ExtractAsync(request);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Content.Content.Should().Contain("createUser");
        response.Content.Content.Should().Contain("Jane");
    }

    #endregion

    #region Variable Substitution

    [Fact]
    public async Task ExtractAsync_WithVariables_ShouldSubstituteCorrectly()
    {
        // Arrange
        const string query = "query GetUser($userId: ID!) { user(id: $userId) { id name } }";
        var variables = new Dictionary<string, object>
        {
            { "userId", 42 }
        };
        var graphQLRequest = new GraphQLRequest(query, variables);

        var mockResponse = new
        {
            data = new
            {
                user = new
                {
                    id = 42,
                    name = "Variable User"
                }
            }
        };

        _server
            .Given(WireMockRequest.Create()
                .WithPath("/graphql")
                .UsingPost()
                .WithBody("*userId*42*", matchBehaviour: WireMock.Matchers.MatchBehaviour.AcceptOnMatch))
            .RespondWith(WireMockResponse.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonConvert.SerializeObject(mockResponse)));

        var request = new Request
        {
            RequestId = Guid.NewGuid().ToString(),
            Url = $"{_server.Url}/graphql",
            Method = "POST",
            Body = JsonConvert.SerializeObject(graphQLRequest),
            ExpectedContentType = ContentType.GraphQL,
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Act
        var response = await _adapter.ExtractAsync(request);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Content.Content.Should().Contain("Variable User");
    }

    [Fact]
    public async Task ExtractAsync_WithComplexVariables_ShouldSerializeCorrectly()
    {
        // Arrange
        const string query = "mutation CreatePost($input: PostInput!) { createPost(input: $input) { id } }";
        var variables = new Dictionary<string, object>
        {
            { "input", new { title = "New Post", content = "Content here", tags = new[] { "tag1", "tag2" } } }
        };
        var graphQLRequest = new GraphQLRequest(query, variables);

        var mockResponse = new
        {
            data = new
            {
                createPost = new { id = 123 }
            }
        };

        _server
            .Given(WireMockRequest.Create()
                .WithPath("/graphql")
                .UsingPost())
            .RespondWith(WireMockResponse.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonConvert.SerializeObject(mockResponse)));

        var request = new Request
        {
            RequestId = Guid.NewGuid().ToString(),
            Url = $"{_server.Url}/graphql",
            Method = "POST",
            Body = JsonConvert.SerializeObject(graphQLRequest),
            ExpectedContentType = ContentType.GraphQL,
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Act
        var response = await _adapter.ExtractAsync(request);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Content.Content.Should().Contain("createPost");
    }

    #endregion

    #region Schema Introspection

    [Fact]
    public async Task ExtractAsync_WithIntrospectionQuery_ShouldReturnSchema()
    {
        // Arrange
        const string introspectionQuery = @"
            query IntrospectionQuery {
                __schema {
                    types {
                        name
                        kind
                    }
                }
            }";
        var graphQLRequest = new GraphQLRequest(introspectionQuery);

        var mockResponse = new
        {
            data = new
            {
                __schema = new
                {
                    types = new[]
                    {
                        new { name = "Query", kind = "OBJECT" },
                        new { name = "User", kind = "OBJECT" },
                        new { name = "String", kind = "SCALAR" }
                    }
                }
            }
        };

        _server
            .Given(WireMockRequest.Create()
                .WithPath("/graphql")
                .UsingPost())
            .RespondWith(WireMockResponse.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonConvert.SerializeObject(mockResponse)));

        var request = new Request
        {
            RequestId = Guid.NewGuid().ToString(),
            Url = $"{_server.Url}/graphql",
            Method = "POST",
            Body = JsonConvert.SerializeObject(graphQLRequest),
            ExpectedContentType = ContentType.GraphQL,
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Act
        var response = await _adapter.ExtractAsync(request);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Content.Content.Should().Contain("__schema");
        response.Content.Content.Should().Contain("OBJECT");
        response.Content.Content.Should().Contain("SCALAR");
    }

    [Fact]
    public async Task ExtractAsync_WithTypeIntrospection_ShouldReturnTypeInfo()
    {
        // Arrange
        const string query = @"
            query TypeQuery {
                __type(name: ""User"") {
                    name
                    fields {
                        name
                        type {
                            name
                        }
                    }
                }
            }";
        var graphQLRequest = new GraphQLRequest(query);

        var mockResponse = new
        {
            data = new
            {
                __type = new
                {
                    name = "User",
                    fields = new[]
                    {
                        new { name = "id", type = new { name = "ID" } },
                        new { name = "name", type = new { name = "String" } }
                    }
                }
            }
        };

        _server
            .Given(WireMockRequest.Create()
                .WithPath("/graphql")
                .UsingPost())
            .RespondWith(WireMockResponse.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonConvert.SerializeObject(mockResponse)));

        var request = new Request
        {
            RequestId = Guid.NewGuid().ToString(),
            Url = $"{_server.Url}/graphql",
            Method = "POST",
            Body = JsonConvert.SerializeObject(graphQLRequest),
            ExpectedContentType = ContentType.GraphQL,
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Act
        var response = await _adapter.ExtractAsync(request);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Content.Content.Should().Contain("__type");
        response.Content.Content.Should().Contain("User");
    }

    #endregion

    #region Relay Pagination

    [Fact]
    public async Task ExtractAsync_WithRelayPagination_ShouldHandleConnectionsAndEdges()
    {
        // Arrange
        const string query = @"
            query GetUsers($first: Int!, $after: String) {
                users(first: $first, after: $after) {
                    edges {
                        node {
                            id
                            name
                        }
                        cursor
                    }
                    pageInfo {
                        hasNextPage
                        endCursor
                    }
                }
            }";
        var variables = new Dictionary<string, object>
        {
            { "first", 10 },
            { "after", "cursor123" }
        };
        var graphQLRequest = new GraphQLRequest(query, variables);

        var mockResponse = new
        {
            data = new
            {
                users = new
                {
                    edges = new[]
                    {
                        new { node = new { id = 1, name = "User 1" }, cursor = "cursor1" },
                        new { node = new { id = 2, name = "User 2" }, cursor = "cursor2" }
                    },
                    pageInfo = new
                    {
                        hasNextPage = true,
                        endCursor = "cursor2"
                    }
                }
            }
        };

        _server
            .Given(WireMockRequest.Create()
                .WithPath("/graphql")
                .UsingPost())
            .RespondWith(WireMockResponse.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonConvert.SerializeObject(mockResponse)));

        var request = new Request
        {
            RequestId = Guid.NewGuid().ToString(),
            Url = $"{_server.Url}/graphql",
            Method = "POST",
            Body = JsonConvert.SerializeObject(graphQLRequest),
            ExpectedContentType = ContentType.GraphQL,
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Act
        var response = await _adapter.ExtractAsync(request);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Content.Content.Should().Contain("edges");
        response.Content.Content.Should().Contain("pageInfo");
        response.Content.Content.Should().Contain("hasNextPage");
        response.Content.Content.Should().Contain("endCursor");
    }

    [Fact]
    public async Task ExtractAsync_WithOffsetPagination_ShouldHandleOffsetAndLimit()
    {
        // Arrange
        const string query = @"
            query GetPosts($offset: Int!, $limit: Int!) {
                posts(offset: $offset, limit: $limit) {
                    id
                    title
                }
                postsConnection {
                    totalCount
                }
            }";
        var variables = new Dictionary<string, object>
        {
            { "offset", 20 },
            { "limit", 10 }
        };
        var graphQLRequest = new GraphQLRequest(query, variables);

        var mockResponse = new
        {
            data = new
            {
                posts = new[]
                {
                    new { id = 21, title = "Post 21" },
                    new { id = 22, title = "Post 22" }
                },
                postsConnection = new { totalCount = 100 }
            }
        };

        _server
            .Given(WireMockRequest.Create()
                .WithPath("/graphql")
                .UsingPost())
            .RespondWith(WireMockResponse.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonConvert.SerializeObject(mockResponse)));

        var request = new Request
        {
            RequestId = Guid.NewGuid().ToString(),
            Url = $"{_server.Url}/graphql",
            Method = "POST",
            Body = JsonConvert.SerializeObject(graphQLRequest),
            ExpectedContentType = ContentType.GraphQL,
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Act
        var response = await _adapter.ExtractAsync(request);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Content.Content.Should().Contain("totalCount");
    }

    #endregion

    #region Error Handling

    [Fact]
    public async Task ExtractAsync_WithGraphQLErrors_ShouldReturnErrorResponse()
    {
        // Arrange
        const string query = "{ user(id: 999) { id name } }";
        var graphQLRequest = new GraphQLRequest(query);

        var mockResponse = new
        {
            data = (object?)null,
            errors = new[]
            {
                new
                {
                    message = "User not found",
                    locations = new[] { new { line = 1, column = 3 } },
                    path = new object[] { "user" },
                    extensions = new { code = "NOT_FOUND" }
                }
            }
        };

        _server
            .Given(WireMockRequest.Create()
                .WithPath("/graphql")
                .UsingPost())
            .RespondWith(WireMockResponse.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonConvert.SerializeObject(mockResponse)));

        var request = new Request
        {
            RequestId = Guid.NewGuid().ToString(),
            Url = $"{_server.Url}/graphql",
            Method = "POST",
            Body = JsonConvert.SerializeObject(graphQLRequest),
            ExpectedContentType = ContentType.GraphQL,
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Act
        var response = await _adapter.ExtractAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.IsSuccess.Should().BeFalse();
        response.Content.Success.Should().BeFalse();
        response.Content.Error.Should().Contain("User not found");
    }

    [Fact]
    public async Task ExtractAsync_WithPartialErrors_ShouldReturnPartialData()
    {
        // Arrange
        const string query = "{ user(id: 1) { id name posts { id title } } }";
        var graphQLRequest = new GraphQLRequest(query);

        var mockResponse = new
        {
            data = new
            {
                user = new
                {
                    id = 1,
                    name = "John",
                    posts = (object?)null
                }
            },
            errors = new[]
            {
                new
                {
                    message = "Failed to fetch posts",
                    path = new object[] { "user", "posts" }
                }
            }
        };

        _server
            .Given(WireMockRequest.Create()
                .WithPath("/graphql")
                .UsingPost())
            .RespondWith(WireMockResponse.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonConvert.SerializeObject(mockResponse)));

        var request = new Request
        {
            RequestId = Guid.NewGuid().ToString(),
            Url = $"{_server.Url}/graphql",
            Method = "POST",
            Body = JsonConvert.SerializeObject(graphQLRequest),
            ExpectedContentType = ContentType.GraphQL,
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Act
        var response = await _adapter.ExtractAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.IsSuccess.Should().BeFalse();
        response.Content.Content.Should().Contain("John");
        response.Content.Error.Should().Contain("Failed to fetch posts");
    }

    [Fact]
    public async Task ExtractAsync_WithHttpError_ShouldReturnErrorResponse()
    {
        // Arrange
        const string query = "{ user { id } }";
        var graphQLRequest = new GraphQLRequest(query);

        _server
            .Given(WireMockRequest.Create()
                .WithPath("/graphql")
                .UsingPost())
            .RespondWith(WireMockResponse.Create()
                .WithStatusCode(500)
                .WithBody("Internal Server Error"));

        var request = new Request
        {
            RequestId = Guid.NewGuid().ToString(),
            Url = $"{_server.Url}/graphql",
            Method = "POST",
            Body = JsonConvert.SerializeObject(graphQLRequest),
            ExpectedContentType = ContentType.GraphQL,
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Act
        var response = await _adapter.ExtractAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.IsSuccess.Should().BeFalse();
        response.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExtractAsync_WithInvalidJsonResponse_ShouldReturnErrorResponse()
    {
        // Arrange
        const string query = "{ user { id } }";
        var graphQLRequest = new GraphQLRequest(query);

        _server
            .Given(WireMockRequest.Create()
                .WithPath("/graphql")
                .UsingPost())
            .RespondWith(WireMockResponse.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("Invalid JSON {{{"));

        var request = new Request
        {
            RequestId = Guid.NewGuid().ToString(),
            Url = $"{_server.Url}/graphql",
            Method = "POST",
            Body = JsonConvert.SerializeObject(graphQLRequest),
            ExpectedContentType = ContentType.GraphQL,
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Act
        var response = await _adapter.ExtractAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.IsSuccess.Should().BeFalse();
        response.Error.Should().Contain("JSON parsing error");
    }

    #endregion

    #region Headers and Authentication

    [Fact]
    public async Task ExtractAsync_WithAuthenticationHeader_ShouldSendToken()
    {
        // Arrange
        const string query = "{ currentUser { id name } }";
        var graphQLRequest = new GraphQLRequest(query);

        _server
            .Given(WireMockRequest.Create()
                .WithPath("/graphql")
                .WithHeader("Authorization", "Bearer secret-token")
                .UsingPost())
            .RespondWith(WireMockResponse.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonConvert.SerializeObject(new
                {
                    data = new
                    {
                        currentUser = new { id = 1, name = "Authenticated User" }
                    }
                })));

        var request = new Request
        {
            RequestId = Guid.NewGuid().ToString(),
            Url = $"{_server.Url}/graphql",
            Method = "POST",
            Body = JsonConvert.SerializeObject(graphQLRequest),
            Headers = new Dictionary<string, string>
            {
                { "Authorization", "Bearer secret-token" }
            },
            ExpectedContentType = ContentType.GraphQL,
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Act
        var response = await _adapter.ExtractAsync(request);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Content.Content.Should().Contain("Authenticated User");
    }

    #endregion

    #region Extensions

    [Fact]
    public async Task ExtractAsync_WithResponseExtensions_ShouldCaptureExtensions()
    {
        // Arrange
        const string query = "{ user(id: 1) { id } }";
        var graphQLRequest = new GraphQLRequest(query);

        var mockResponse = new
        {
            data = new { user = new { id = 1 } },
            extensions = new
            {
                tracing = new
                {
                    version = 1,
                    startTime = "2026-02-04T10:00:00Z",
                    endTime = "2026-02-04T10:00:00.050Z",
                    duration = 50000000
                }
            }
        };

        _server
            .Given(WireMockRequest.Create()
                .WithPath("/graphql")
                .UsingPost())
            .RespondWith(WireMockResponse.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonConvert.SerializeObject(mockResponse)));

        var request = new Request
        {
            RequestId = Guid.NewGuid().ToString(),
            Url = $"{_server.Url}/graphql",
            Method = "POST",
            Body = JsonConvert.SerializeObject(graphQLRequest),
            ExpectedContentType = ContentType.GraphQL,
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Act
        var response = await _adapter.ExtractAsync(request);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Metadata.Should().ContainKey("GraphQL.Extensions");
    }

    #endregion

    #region CanHandle

    [Fact]
    public async Task CanHandleAsync_WithGraphQLContentType_ShouldReturnTrue()
    {
        // Arrange
        var request = new Request
        {
            RequestId = Guid.NewGuid().ToString(),
            Url = "https://api.example.com/graphql",
            Method = "POST",
            ExpectedContentType = ContentType.GraphQL,
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Act
        var canHandle = await _adapter.CanHandleAsync(request);

        // Assert
        canHandle.Should().BeTrue();
    }

    [Fact]
    public async Task CanHandleAsync_WithGraphQLUrlPath_ShouldReturnTrue()
    {
        // Arrange
        var request = new Request
        {
            RequestId = Guid.NewGuid().ToString(),
            Url = "https://api.example.com/graphql",
            Method = "POST",
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Act
        var canHandle = await _adapter.CanHandleAsync(request);

        // Assert
        canHandle.Should().BeTrue();
    }

    #endregion
}
