using FluentAssertions;
using Ghost.Sdk.Spider.Adapters;
using Ghost.Sdk.Spider.Adapters.Contracts;
using Ghost.Sdk.Spider.Adapters.GraphQL;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using Xunit;
using System.Net;
using Ghost.Testing.Reliability;
using Xunit.Abstractions;

namespace Ghost.Sdk.Spider.Tests.Unit.Adapters;

/// <summary>
/// Unit tests for GraphQL pagination functionality.
/// </summary>
public class GraphQLPaginationTests : ReliabilityTestBase
{
    public GraphQLPaginationTests(ITestOutputHelper output) : base(output) { }
    [Fact]
    public void GraphQLRequest_ShouldSerializeCorrectly()
    {
        // Arrange
        var request = new GraphQLRequest
        {
            Query = "query { users { id name } }",
            Variables = new Dictionary<string, object>
            {
                ["first"] = 10,
                ["after"] = "cursor123"
            },
            OperationName = "GetUsers"
        };

        // Act
        var json = JsonConvert.SerializeObject(request);
        var deserialized = JsonConvert.DeserializeObject<GraphQLRequest>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Query.Should().Be(request.Query);
        deserialized.OperationName.Should().Be(request.OperationName);
        deserialized.Variables.Should().ContainKey("first");
        deserialized.Variables.Should().ContainKey("after");
    }

    [Fact]
    public void GraphQLResponse_WithData_ShouldDeserializeCorrectly()
    {
        // Arrange
        var json = @"{
            ""data"": {
                ""users"": [
                    { ""id"": 1, ""name"": ""John"" },
                    { ""id"": 2, ""name"": ""Jane"" }
                ]
            }
        }";

        // Act
        var response = JsonConvert.DeserializeObject<GraphQLResponse>(json);

        // Assert
        response.Should().NotBeNull();
        response!.Data.Should().NotBeNull();
        response.Errors.Should().BeNullOrEmpty();
    }

    [Fact]
    public void GraphQLResponse_WithErrors_ShouldDeserializeCorrectly()
    {
        // Arrange
        var json = @"{
            ""errors"": [
                {
                    ""message"": ""Field 'invalidField' not found"",
                    ""locations"": [{""line"": 1, ""column"": 10}],
                    ""path"": [""users"", ""invalidField""]
                }
            ]
        }";

        // Act
        var response = JsonConvert.DeserializeObject<GraphQLResponse>(json);

        // Assert
        response.Should().NotBeNull();
        response!.Errors.Should().HaveCount(1);
        response.Errors![0].Message.Should().Contain("invalidField");
        response.Errors[0].Locations.Should().HaveCount(1);
        response.Errors[0].Path.Should().HaveCount(2);
    }

    [Fact]
    public void GraphQLResponse_WithExtensions_ShouldDeserializeCorrectly()
    {
        // Arrange
        var json = @"{
            ""data"": { ""result"": ""ok"" },
            ""extensions"": {
                ""tracing"": {
                    ""version"": 1,
                    ""startTime"": ""2024-01-01T00:00:00Z"",
                    ""duration"": 123456789
                }
            }
        }";

        // Act
        var response = JsonConvert.DeserializeObject<GraphQLResponse>(json);

        // Assert
        response.Should().NotBeNull();
        response!.Extensions.Should().NotBeNull();
        response.Extensions.Should().ContainKey("tracing");
    }

    [Fact]
    public void GraphQLError_ShouldContainAllProperties()
    {
        // Arrange
        var error = new GraphQLError
        {
            Message = "Test error",
            Locations = new List<GraphQLErrorLocation>
            {
                new() { Line = 1, Column = 5 }
            },
            Path = new List<object> { "query", "field" },
            Extensions = new Dictionary<string, object>
            {
                ["code"] = "INTERNAL_ERROR"
            }
        };

        // Act
        var json = JsonConvert.SerializeObject(error);
        var deserialized = JsonConvert.DeserializeObject<GraphQLError>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Message.Should().Be("Test error");
        deserialized.Locations.Should().HaveCount(1);
        deserialized.Path.Should().HaveCount(2);
        deserialized.Extensions.Should().ContainKey("code");
    }

    [Fact]
    public void GraphQLRequest_WithoutVariables_ShouldBeValid()
    {
        // Arrange
        var request = new GraphQLRequest
        {
            Query = "query { __typename }"
        };

        // Act
        var json = JsonConvert.SerializeObject(request);

        // Assert
        json.Should().Contain("__typename");
        request.Variables.Should().BeNull();
    }

    [Fact]
    public void GraphQLRequest_WithComplexVariables_ShouldSerializeCorrectly()
    {
        // Arrange
        var request = new GraphQLRequest
        {
            Query = "mutation CreateUser($input: CreateUserInput!) { createUser(input: $input) { id } }",
            Variables = new Dictionary<string, object>
            {
                ["input"] = new
                {
                    name = "John Doe",
                    email = "john@example.com",
                    age = 30,
                    tags = new[] { "developer", "remote" }
                }
            }
        };

        // Act
        var json = JsonConvert.SerializeObject(request);
        var deserialized = JsonConvert.DeserializeObject<GraphQLRequest>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Variables.Should().ContainKey("input");
    }

    [Fact]
    public void GraphQLResponse_WithNestedData_ShouldDeserializeCorrectly()
    {
        // Arrange
        var json = @"{
            ""data"": {
                ""repository"": {
                    ""issues"": {
                        ""edges"": [
                            {
                                ""node"": {
                                    ""id"": ""issue1"",
                                    ""title"": ""Test Issue""
                                },
                                ""cursor"": ""cursor1""
                            }
                        ],
                        ""pageInfo"": {
                            ""hasNextPage"": true,
                            ""endCursor"": ""cursor1""
                        }
                    }
                }
            }
        }";

        // Act
        var response = JsonConvert.DeserializeObject<GraphQLResponse>(json);

        // Assert
        response.Should().NotBeNull();
        response!.Data.Should().NotBeNull();
    }

    [Fact]
    public void GraphQLError_WithoutOptionalFields_ShouldBeValid()
    {
        // Arrange
        var error = new GraphQLError
        {
            Message = "Simple error"
        };

        // Act
        var json = JsonConvert.SerializeObject(error);
        var deserialized = JsonConvert.DeserializeObject<GraphQLError>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Message.Should().Be("Simple error");
        deserialized.Locations.Should().BeNullOrEmpty();
        deserialized.Path.Should().BeNullOrEmpty();
    }

    [Fact]
    public void GraphQLErrorLocation_ShouldHaveLineAndColumn()
    {
        // Arrange
        var location = new GraphQLErrorLocation
        {
            Line = 10,
            Column = 25
        };

        // Act
        var json = JsonConvert.SerializeObject(location);
        var deserialized = JsonConvert.DeserializeObject<GraphQLErrorLocation>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Line.Should().Be(10);
        deserialized.Column.Should().Be(25);
    }
}
