using FluentAssertions;
using Ghost.Sdk.Spider.Adapters.GraphQL;
using NUnit.Framework;

namespace Ghost.Sdk.Spider.Tests.Unit.Adapters;

/// <summary>
/// Comprehensive tests for GraphQL request and response models.
/// </summary>
[TestFixture]
public class GraphQLModelsTests
{
    [Test]
    public void GraphQLRequest_DefaultConstructor_ShouldInitialize()
    {
        // Act
        var request = new GraphQLRequest();

        // Assert
        request.Should().NotBeNull();
        request.Query.Should().BeEmpty();
        request.OperationName.Should().BeNull();
        request.Variables.Should().BeNull();
        request.Extensions.Should().BeNull();
    }

    [Test]
    public void GraphQLRequest_WithQuery_ShouldInitialize()
    {
        // Arrange
        var query = "query { users { id name } }";

        // Act
        var request = new GraphQLRequest(query);

        // Assert
        request.Query.Should().Be(query);
        request.OperationName.Should().BeNull();
        request.Variables.Should().BeNull();
    }

    [Test]
    public void GraphQLRequest_WithQueryAndVariables_ShouldInitialize()
    {
        // Arrange
        var query = "query($id: ID!) { user(id: $id) { name } }";
        var variables = new Dictionary<string, object> { ["id"] = "123" };

        // Act
        var request = new GraphQLRequest(query, variables);

        // Assert
        request.Query.Should().Be(query);
        request.Variables.Should().BeEquivalentTo(variables);
    }

    [Test]
    public void GraphQLRequest_CreateQuery_ShouldReturnRequest()
    {
        // Arrange
        var query = "query { users { id } }";

        // Act
        var request = GraphQLRequest.CreateQuery(query);

        // Assert
        request.Should().NotBeNull();
        request.Query.Should().Be(query);
    }

    [Test]
    public void GraphQLRequest_CreateMutation_ShouldReturnRequest()
    {
        // Arrange
        var mutation = "mutation { createUser(name: \"John\") { id } }";

        // Act
        var request = GraphQLRequest.CreateMutation(mutation);

        // Assert
        request.Should().NotBeNull();
        request.Query.Should().Be(mutation);
    }

    [Test]
    public void GraphQLRequest_WithVariable_ShouldAddVariable()
    {
        // Arrange
        var request = new GraphQLRequest("query { users }");

        // Act
        var result = request.WithVariable("limit", 10);

        // Assert
        result.Should().BeSameAs(request); // Fluent API
        request.Variables.Should().NotBeNull();
        request.Variables.Should().ContainKey("limit");
        request.Variables!["limit"].Should().Be(10);
    }

    [Test]
    public void GraphQLRequest_WithMultipleVariables_ShouldAddAll()
    {
        // Arrange
        var request = new GraphQLRequest("query { users }");

        // Act
        request.WithVariable("limit", 10)
               .WithVariable("offset", 20)
               .WithVariable("search", "test");

        // Assert
        request.Variables.Should().HaveCount(3);
        request.Variables!["limit"].Should().Be(10);
        request.Variables["offset"].Should().Be(20);
        request.Variables["search"].Should().Be("test");
    }

    [Test]
    public void GraphQLRequest_WithVariable_ShouldOverwriteExisting()
    {
        // Arrange
        var request = new GraphQLRequest("query { users }")
            .WithVariable("id", "123");

        // Act
        request.WithVariable("id", "456");

        // Assert
        request.Variables!["id"].Should().Be("456");
    }

    [Test]
    public void GraphQLRequest_WithOperationName_ShouldSetName()
    {
        // Arrange
        var request = new GraphQLRequest("query GetUsers { users { id } }");

        // Act
        var result = request.WithOperationName("GetUsers");

        // Assert
        result.Should().BeSameAs(request);
        request.OperationName.Should().Be("GetUsers");
    }

    [Test]
    public void GraphQLRequest_WithExtension_ShouldAddExtension()
    {
        // Arrange
        var request = new GraphQLRequest("query { users }");

        // Act
        var result = request.WithExtension("persistedQuery", new { version = 1, sha256Hash = "abc123" });

        // Assert
        result.Should().BeSameAs(request);
        request.Extensions.Should().NotBeNull();
        request.Extensions.Should().ContainKey("persistedQuery");
    }

    [Test]
    public void GraphQLRequest_WithMultipleExtensions_ShouldAddAll()
    {
        // Arrange
        var request = new GraphQLRequest("query { users }");

        // Act
        request.WithExtension("tracing", true)
               .WithExtension("caching", new { ttl = 300 });

        // Assert
        request.Extensions.Should().HaveCount(2);
    }

    [Test]
    public void GraphQLResponse_DefaultConstructor_ShouldInitialize()
    {
        // Act
        var response = new GraphQLResponse();

        // Assert
        response.Should().NotBeNull();
        response.Data.Should().BeNull();
        response.Errors.Should().BeNull();
        response.Extensions.Should().BeNull();
    }

    [Test]
    public void GraphQLResponse_HasErrors_WithErrors_ShouldReturnTrue()
    {
        // Arrange
        var response = new GraphQLResponse
        {
            Errors = new List<GraphQLError>
            {
                new() { Message = "Test error" }
            }
        };

        // Assert
        response.HasErrors.Should().BeTrue();
    }

    [Test]
    public void GraphQLResponse_HasErrors_WithoutErrors_ShouldReturnFalse()
    {
        // Arrange
        var response = new GraphQLResponse();

        // Assert
        response.HasErrors.Should().BeFalse();
    }

    [Test]
    public void GraphQLResponse_HasErrors_WithEmptyList_ShouldReturnFalse()
    {
        // Arrange
        var response = new GraphQLResponse
        {
            Errors = new List<GraphQLError>()
        };

        // Assert
        response.HasErrors.Should().BeFalse();
    }

    [Test]
    public void GraphQLResponse_IsSuccess_WithDataAndNoErrors_ShouldReturnTrue()
    {
        // Arrange
        var response = new GraphQLResponse
        {
            Data = new { users = new[] { new { id = "1" } } }
        };

        // Assert
        response.IsSuccess.Should().BeTrue();
    }

    [Test]
    public void GraphQLResponse_IsSuccess_WithDataAndErrors_ShouldReturnFalse()
    {
        // Arrange
        var response = new GraphQLResponse
        {
            Data = new { users = new[] { new { id = "1" } } },
            Errors = new List<GraphQLError> { new() { Message = "Error" } }
        };

        // Assert
        response.IsSuccess.Should().BeFalse();
    }

    [Test]
    public void GraphQLResponse_IsSuccess_WithNoData_ShouldReturnFalse()
    {
        // Arrange
        var response = new GraphQLResponse();

        // Assert
        response.IsSuccess.Should().BeFalse();
    }

    [Test]
    public void GraphQLResponse_HasPartialData_WithDataAndErrors_ShouldReturnTrue()
    {
        // Arrange
        var response = new GraphQLResponse
        {
            Data = new { users = new[] { new { id = "1" } } },
            Errors = new List<GraphQLError> { new() { Message = "Partial error" } }
        };

        // Assert
        response.HasPartialData.Should().BeTrue();
    }

    [Test]
    public void GraphQLResponse_HasPartialData_WithOnlyData_ShouldReturnFalse()
    {
        // Arrange
        var response = new GraphQLResponse
        {
            Data = new { users = new[] { new { id = "1" } } }
        };

        // Assert
        response.HasPartialData.Should().BeFalse();
    }

    [Test]
    public void GraphQLResponse_HasPartialData_WithOnlyErrors_ShouldReturnFalse()
    {
        // Arrange
        var response = new GraphQLResponse
        {
            Errors = new List<GraphQLError> { new() { Message = "Error" } }
        };

        // Assert
        response.HasPartialData.Should().BeFalse();
    }

    [Test]
    public void GraphQLResponse_GetErrorMessage_WithErrors_ShouldConcatenate()
    {
        // Arrange
        var response = new GraphQLResponse
        {
            Errors = new List<GraphQLError>
            {
                new() { Message = "Error 1" },
                new() { Message = "Error 2" },
                new() { Message = "Error 3" }
            }
        };

        // Act
        var errorMessage = response.GetErrorMessage();

        // Assert
        errorMessage.Should().Be("Error 1; Error 2; Error 3");
    }

    [Test]
    public void GraphQLResponse_GetErrorMessage_WithoutErrors_ShouldReturnEmpty()
    {
        // Arrange
        var response = new GraphQLResponse();

        // Act
        var errorMessage = response.GetErrorMessage();

        // Assert
        errorMessage.Should().BeEmpty();
    }

    [Test]
    public void GraphQLResponse_Success_ShouldCreateSuccessResponse()
    {
        // Arrange
        var data = new { users = new[] { new { id = "1", name = "John" } } };

        // Act
        var response = GraphQLResponse.Success(data);

        // Assert
        response.Should().NotBeNull();
        response.Data.Should().Be(data);
        response.IsSuccess.Should().BeTrue();
        response.HasErrors.Should().BeFalse();
    }

    [Test]
    public void GraphQLResponse_ErrorWithErrors_ShouldCreateErrorResponse()
    {
        // Arrange
        var errors = new[]
        {
            new GraphQLError { Message = "Error 1" },
            new GraphQLError { Message = "Error 2" }
        };

        // Act
        var response = GraphQLResponse.Error(errors);

        // Assert
        response.Should().NotBeNull();
        response.Errors.Should().HaveCount(2);
        response.HasErrors.Should().BeTrue();
        response.IsSuccess.Should().BeFalse();
    }

    [Test]
    public void GraphQLResponse_ErrorWithMessage_ShouldCreateErrorResponse()
    {
        // Arrange
        var errorMessage = "Something went wrong";

        // Act
        var response = GraphQLResponse.Error(errorMessage);

        // Assert
        response.Should().NotBeNull();
        response.Errors.Should().ContainSingle();
        response.Errors![0].Message.Should().Be(errorMessage);
        response.HasErrors.Should().BeTrue();
    }

    [Test]
    public void GraphQLRequest_FluentAPI_ShouldChainCorrectly()
    {
        // Act
        var request = GraphQLRequest.CreateQuery("query { users }")
            .WithVariable("limit", 10)
            .WithVariable("offset", 0)
            .WithOperationName("GetUsers")
            .WithExtension("tracing", true);

        // Assert
        request.Query.Should().Be("query { users }");
        request.Variables.Should().HaveCount(2);
        request.OperationName.Should().Be("GetUsers");
        request.Extensions.Should().ContainKey("tracing");
    }

    [Test]
    public void GraphQLError_ShouldHaveMessage()
    {
        // Act
        var error = new GraphQLError { Message = "Test error" };

        // Assert
        error.Message.Should().Be("Test error");
    }

    [Test]
    public void GraphQLResponse_WithExtensions_ShouldRetainThem()
    {
        // Arrange
        var extensions = new Dictionary<string, object>
        {
            ["tracing"] = new { duration = 150 },
            ["complexity"] = 42
        };

        // Act
        var response = new GraphQLResponse
        {
            Data = new { test = true },
            Extensions = extensions
        };

        // Assert
        response.Extensions.Should().BeEquivalentTo(extensions);
    }
}
