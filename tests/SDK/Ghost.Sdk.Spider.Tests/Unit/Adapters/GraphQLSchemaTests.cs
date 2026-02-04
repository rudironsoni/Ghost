using FluentAssertions;
using Ghost.Sdk.Spider.Adapters.GraphQL.Schema;
using NUnit.Framework;

namespace Ghost.Sdk.Spider.Tests.Unit.Adapters;

/// <summary>
/// Comprehensive tests for GraphQL Schema classes.
/// </summary>
[TestFixture]
public class GraphQLSchemaTests
{
    [Test]
    public void GraphQLSchema_DefaultConstructor_ShouldInitialize()
    {
        // Act
        var schema = new GraphQLSchema();

        // Assert
        schema.Should().NotBeNull();
        schema.QueryType.Should().BeNull();
        schema.MutationType.Should().BeNull();
        schema.SubscriptionType.Should().BeNull();
        schema.Types.Should().NotBeNull().And.BeEmpty();
        schema.Directives.Should().NotBeNull().And.BeEmpty();
    }

    [Test]
    public void GraphQLSchema_FindType_WithExistingType_ShouldReturnType()
    {
        // Arrange
        var schema = new GraphQLSchema
        {
            Types = new List<GraphQLType>
            {
                new() { Name = "User", Kind = "OBJECT" },
                new() { Name = "Post", Kind = "OBJECT" }
            }
        };

        // Act
        var type = schema.FindType("User");

        // Assert
        type.Should().NotBeNull();
        type!.Name.Should().Be("User");
    }

    [Test]
    public void GraphQLSchema_FindType_WithNonExistingType_ShouldReturnNull()
    {
        // Arrange
        var schema = new GraphQLSchema
        {
            Types = new List<GraphQLType>
            {
                new() { Name = "User", Kind = "OBJECT" }
            }
        };

        // Act
        var type = schema.FindType("NonExisting");

        // Assert
        type.Should().BeNull();
    }

    [Test]
    public void GraphQLSchema_GetQueryFields_WithQueryType_ShouldReturnFields()
    {
        // Arrange
        var queryFields = new List<GraphQLField>
        {
            new() { Name = "users", Type = new GraphQLType { Name = "User" } },
            new() { Name = "posts", Type = new GraphQLType { Name = "Post" } }
        };
        var schema = new GraphQLSchema
        {
            QueryType = new GraphQLType
            {
                Name = "Query",
                Fields = queryFields
            }
        };

        // Act
        var fields = schema.GetQueryFields();

        // Assert
        fields.Should().HaveCount(2);
        fields.Should().Contain(f => f.Name == "users");
        fields.Should().Contain(f => f.Name == "posts");
    }

    [Test]
    public void GraphQLSchema_GetQueryFields_WithNoQueryType_ShouldReturnEmptyList()
    {
        // Arrange
        var schema = new GraphQLSchema();

        // Act
        var fields = schema.GetQueryFields();

        // Assert
        fields.Should().NotBeNull().And.BeEmpty();
    }

    [Test]
    public void GraphQLSchema_GetMutationFields_WithMutationType_ShouldReturnFields()
    {
        // Arrange
        var mutationFields = new List<GraphQLField>
        {
            new() { Name = "createUser", Type = new GraphQLType { Name = "User" } },
            new() { Name = "updateUser", Type = new GraphQLType { Name = "User" } }
        };
        var schema = new GraphQLSchema
        {
            MutationType = new GraphQLType
            {
                Name = "Mutation",
                Fields = mutationFields
            }
        };

        // Act
        var fields = schema.GetMutationFields();

        // Assert
        fields.Should().HaveCount(2);
        fields.Should().Contain(f => f.Name == "createUser");
    }

    [Test]
    public void GraphQLSchema_GetMutationFields_WithNoMutationType_ShouldReturnEmptyList()
    {
        // Arrange
        var schema = new GraphQLSchema();

        // Act
        var fields = schema.GetMutationFields();

        // Assert
        fields.Should().NotBeNull().And.BeEmpty();
    }

    [Test]
    public void GraphQLSchema_GetSubscriptionFields_WithSubscriptionType_ShouldReturnFields()
    {
        // Arrange
        var subscriptionFields = new List<GraphQLField>
        {
            new() { Name = "messageAdded", Type = new GraphQLType { Name = "Message" } }
        };
        var schema = new GraphQLSchema
        {
            SubscriptionType = new GraphQLType
            {
                Name = "Subscription",
                Fields = subscriptionFields
            }
        };

        // Act
        var fields = schema.GetSubscriptionFields();

        // Assert
        fields.Should().HaveCount(1);
        fields[0].Name.Should().Be("messageAdded");
    }

    [Test]
    public void GraphQLSchema_GetSubscriptionFields_WithNoSubscriptionType_ShouldReturnEmptyList()
    {
        // Arrange
        var schema = new GraphQLSchema();

        // Act
        var fields = schema.GetSubscriptionFields();

        // Assert
        fields.Should().NotBeNull().And.BeEmpty();
    }

    [Test]
    public void GraphQLSchema_CreateIntrospectionQuery_ShouldReturnValidQuery()
    {
        // Act
        var query = GraphQLSchema.CreateIntrospectionQuery();

        // Assert
        query.Should().NotBeEmpty();
        query.Should().Contain("__schema");
        query.Should().Contain("queryType");
        query.Should().Contain("mutationType");
        query.Should().Contain("subscriptionType");
        query.Should().Contain("FullType");
        query.Should().Contain("TypeRef");
    }

    [Test]
    public void GraphQLType_GetNamedTypeName_WithNamedType_ShouldReturnName()
    {
        // Arrange
        var type = new GraphQLType { Name = "String", Kind = "SCALAR" };

        // Act
        var name = type.GetNamedTypeName();

        // Assert
        name.Should().Be("String");
    }

    [Test]
    public void GraphQLType_GetNamedTypeName_WithListOfNamedType_ShouldReturnInnerName()
    {
        // Arrange
        var type = new GraphQLType
        {
            Kind = "LIST",
            OfType = new GraphQLType { Name = "String", Kind = "SCALAR" }
        };

        // Act
        var name = type.GetNamedTypeName();

        // Assert
        name.Should().Be("String");
    }

    [Test]
    public void GraphQLType_GetNamedTypeName_WithNonNullOfList_ShouldReturnInnerName()
    {
        // Arrange
        var type = new GraphQLType
        {
            Kind = "NON_NULL",
            OfType = new GraphQLType
            {
                Kind = "LIST",
                OfType = new GraphQLType { Name = "String", Kind = "SCALAR" }
            }
        };

        // Act
        var name = type.GetNamedTypeName();

        // Assert
        name.Should().Be("String");
    }

    [Test]
    public void GraphQLType_IsScalar_WithScalarType_ShouldReturnTrue()
    {
        // Arrange
        var type = new GraphQLType { Name = "String", Kind = "SCALAR" };

        // Act
        var isScalar = type.IsScalar();

        // Assert
        isScalar.Should().BeTrue();
    }

    [Test]
    public void GraphQLType_IsScalar_WithObjectType_ShouldReturnFalse()
    {
        // Arrange
        var type = new GraphQLType { Name = "User", Kind = "OBJECT" };

        // Act
        var isScalar = type.IsScalar();

        // Assert
        isScalar.Should().BeFalse();
    }

    [Test]
    public void GraphQLType_IsObject_WithObjectType_ShouldReturnTrue()
    {
        // Arrange
        var type = new GraphQLType { Name = "User", Kind = "OBJECT" };

        // Act
        var isObject = type.IsObject();

        // Assert
        isObject.Should().BeTrue();
    }

    [Test]
    public void GraphQLType_IsList_WithListType_ShouldReturnTrue()
    {
        // Arrange
        var type = new GraphQLType { Kind = "LIST" };

        // Act
        var isList = type.IsList();

        // Assert
        isList.Should().BeTrue();
    }

    [Test]
    public void GraphQLType_IsNonNull_WithNonNullType_ShouldReturnTrue()
    {
        // Arrange
        var type = new GraphQLType { Kind = "NON_NULL" };

        // Act
        var isNonNull = type.IsNonNull();

        // Assert
        isNonNull.Should().BeTrue();
    }

    [Test]
    public void GraphQLType_FindField_WithExistingField_ShouldReturnField()
    {
        // Arrange
        var type = new GraphQLType
        {
            Name = "User",
            Fields = new List<GraphQLField>
            {
                new() { Name = "id", Type = new GraphQLType { Name = "ID" } },
                new() { Name = "name", Type = new GraphQLType { Name = "String" } }
            }
        };

        // Act
        var field = type.FindField("name");

        // Assert
        field.Should().NotBeNull();
        field!.Name.Should().Be("name");
    }

    [Test]
    public void GraphQLType_FindField_WithNonExistingField_ShouldReturnNull()
    {
        // Arrange
        var type = new GraphQLType
        {
            Name = "User",
            Fields = new List<GraphQLField>
            {
                new() { Name = "id", Type = new GraphQLType { Name = "ID" } }
            }
        };

        // Act
        var field = type.FindField("nonExisting");

        // Assert
        field.Should().BeNull();
    }

    [Test]
    public void GraphQLType_ToString_WithNamedType_ShouldReturnName()
    {
        // Arrange
        var type = new GraphQLType { Name = "String", Kind = "SCALAR" };

        // Act
        var str = type.ToString();

        // Assert
        str.Should().Be("String");
    }

    [Test]
    public void GraphQLType_ToString_WithListType_ShouldIncludeOfType()
    {
        // Arrange
        var type = new GraphQLType
        {
            Kind = "LIST",
            OfType = new GraphQLType { Name = "String", Kind = "SCALAR" }
        };

        // Act
        var str = type.ToString();

        // Assert
        str.Should().Contain("LIST");
        str.Should().Contain("String");
    }

    [Test]
    public void GraphQLField_HasArguments_WithArguments_ShouldReturnTrue()
    {
        // Arrange
        var field = new GraphQLField
        {
            Name = "user",
            Type = new GraphQLType { Name = "User" },
            Args = new List<GraphQLInputValue>
            {
                new() { Name = "id", Type = new GraphQLType { Name = "ID" } }
            }
        };

        // Act
        var hasArgs = field.HasArguments();

        // Assert
        hasArgs.Should().BeTrue();
    }

    [Test]
    public void GraphQLField_HasArguments_WithNoArguments_ShouldReturnFalse()
    {
        // Arrange
        var field = new GraphQLField
        {
            Name = "users",
            Type = new GraphQLType { Name = "User" }
        };

        // Act
        var hasArgs = field.HasArguments();

        // Assert
        hasArgs.Should().BeFalse();
    }

    [Test]
    public void GraphQLField_FindArgument_WithExistingArgument_ShouldReturnArgument()
    {
        // Arrange
        var field = new GraphQLField
        {
            Name = "user",
            Type = new GraphQLType { Name = "User" },
            Args = new List<GraphQLInputValue>
            {
                new() { Name = "id", Type = new GraphQLType { Name = "ID" } },
                new() { Name = "name", Type = new GraphQLType { Name = "String" } }
            }
        };

        // Act
        var arg = field.FindArgument("id");

        // Assert
        arg.Should().NotBeNull();
        arg!.Name.Should().Be("id");
    }

    [Test]
    public void GraphQLField_FindArgument_WithNonExistingArgument_ShouldReturnNull()
    {
        // Arrange
        var field = new GraphQLField
        {
            Name = "user",
            Type = new GraphQLType { Name = "User" },
            Args = new List<GraphQLInputValue>
            {
                new() { Name = "id", Type = new GraphQLType { Name = "ID" } }
            }
        };

        // Act
        var arg = field.FindArgument("nonExisting");

        // Assert
        arg.Should().BeNull();
    }

    [Test]
    public void GraphQLField_ToString_WithoutArguments_ShouldShowNameAndType()
    {
        // Arrange
        var field = new GraphQLField
        {
            Name = "users",
            Type = new GraphQLType { Name = "User" }
        };

        // Act
        var str = field.ToString();

        // Assert
        str.Should().Contain("users");
        str.Should().NotContain("(");
    }

    [Test]
    public void GraphQLField_ToString_WithArguments_ShouldShowArgumentNames()
    {
        // Arrange
        var field = new GraphQLField
        {
            Name = "user",
            Type = new GraphQLType { Name = "User" },
            Args = new List<GraphQLInputValue>
            {
                new() { Name = "id", Type = new GraphQLType { Name = "ID" } }
            }
        };

        // Act
        var str = field.ToString();

        // Assert
        str.Should().Contain("user");
        str.Should().Contain("(id)");
    }

    [Test]
    public void GraphQLDirective_ShouldHaveNameAndLocations()
    {
        // Arrange & Act
        var directive = new GraphQLDirective
        {
            Name = "deprecated",
            Description = "Marks field as deprecated",
            Locations = new List<string> { "FIELD_DEFINITION", "ENUM_VALUE" }
        };

        // Assert
        directive.Name.Should().Be("deprecated");
        directive.Description.Should().Be("Marks field as deprecated");
        directive.Locations.Should().HaveCount(2);
    }

    [Test]
    public void GraphQLEnumValue_ShouldHaveProperties()
    {
        // Arrange & Act
        var enumValue = new GraphQLEnumValue
        {
            Name = "ACTIVE",
            Description = "Active status",
            IsDeprecated = false
        };

        // Assert
        enumValue.Name.Should().Be("ACTIVE");
        enumValue.Description.Should().Be("Active status");
        enumValue.IsDeprecated.Should().BeFalse();
    }

    [Test]
    public void GraphQLEnumValue_Deprecated_ShouldHaveReason()
    {
        // Arrange & Act
        var enumValue = new GraphQLEnumValue
        {
            Name = "OLD_STATUS",
            IsDeprecated = true,
            DeprecationReason = "Use NEW_STATUS instead"
        };

        // Assert
        enumValue.IsDeprecated.Should().BeTrue();
        enumValue.DeprecationReason.Should().Be("Use NEW_STATUS instead");
    }

    [Test]
    public void GraphQLInputValue_ShouldHaveTypeAndDefaultValue()
    {
        // Arrange & Act
        var inputValue = new GraphQLInputValue
        {
            Name = "limit",
            Description = "Maximum number of items",
            Type = new GraphQLType { Name = "Int", Kind = "SCALAR" },
            DefaultValue = "10"
        };

        // Assert
        inputValue.Name.Should().Be("limit");
        inputValue.Type.Name.Should().Be("Int");
        inputValue.DefaultValue.Should().Be("10");
    }

    [Test]
    public void GraphQLType_WithInterfaces_ShouldRetainThem()
    {
        // Arrange
        var type = new GraphQLType
        {
            Name = "User",
            Kind = "OBJECT",
            Interfaces = new List<GraphQLType>
            {
                new() { Name = "Node", Kind = "INTERFACE" },
                new() { Name = "Timestamped", Kind = "INTERFACE" }
            }
        };

        // Act & Assert
        type.Interfaces.Should().HaveCount(2);
        type.Interfaces.Should().Contain(i => i.Name == "Node");
    }

    [Test]
    public void GraphQLType_WithPossibleTypes_ShouldRetainThem()
    {
        // Arrange
        var type = new GraphQLType
        {
            Name = "SearchResult",
            Kind = "UNION",
            PossibleTypes = new List<GraphQLType>
            {
                new() { Name = "User", Kind = "OBJECT" },
                new() { Name = "Post", Kind = "OBJECT" }
            }
        };

        // Act & Assert
        type.PossibleTypes.Should().HaveCount(2);
        type.PossibleTypes.Should().Contain(t => t.Name == "User");
    }

    [Test]
    public void GraphQLType_WithEnumValues_ShouldRetainThem()
    {
        // Arrange
        var type = new GraphQLType
        {
            Name = "Status",
            Kind = "ENUM",
            EnumValues = new List<GraphQLEnumValue>
            {
                new() { Name = "ACTIVE" },
                new() { Name = "INACTIVE" },
                new() { Name = "PENDING" }
            }
        };

        // Act & Assert
        type.EnumValues.Should().HaveCount(3);
        type.EnumValues.Should().Contain(e => e.Name == "ACTIVE");
    }

    [Test]
    public void GraphQLType_WithInputFields_ShouldRetainThem()
    {
        // Arrange
        var type = new GraphQLType
        {
            Name = "UserInput",
            Kind = "INPUT_OBJECT",
            InputFields = new List<GraphQLInputValue>
            {
                new() { Name = "name", Type = new GraphQLType { Name = "String" } },
                new() { Name = "email", Type = new GraphQLType { Name = "String" } }
            }
        };

        // Act & Assert
        type.InputFields.Should().HaveCount(2);
        type.InputFields.Should().Contain(f => f.Name == "name");
    }

    [Test]
    public void GraphQLField_WithDeprecation_ShouldHaveReasonAndFlag()
    {
        // Arrange & Act
        var field = new GraphQLField
        {
            Name = "oldField",
            Type = new GraphQLType { Name = "String" },
            IsDeprecated = true,
            DeprecationReason = "Use newField instead"
        };

        // Assert
        field.IsDeprecated.Should().BeTrue();
        field.DeprecationReason.Should().Be("Use newField instead");
    }
}
