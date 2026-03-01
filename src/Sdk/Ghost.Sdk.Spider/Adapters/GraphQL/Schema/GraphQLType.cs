using System.Text.Json.Serialization;

namespace Ghost.Sdk.Spider.Adapters.GraphQL.Schema;

/// <summary>
/// Represents a GraphQL type in the schema.
/// </summary>
/// <remarks>
/// A GraphQL type can be an object, interface, union, enum, input object, scalar, or list/non-null wrapper.
/// This class models the type structure as returned by GraphQL introspection queries.
/// </remarks>
public class GraphQLType
{
    /// <summary>
    /// Gets or sets the kind of this type.
    /// </summary>
    /// <value>
    /// The type kind (SCALAR, OBJECT, INTERFACE, UNION, ENUM, INPUT_OBJECT, LIST, NON_NULL).
    /// </value>
    [JsonPropertyName("kind")]
    public string? Kind { get; set; }

    /// <summary>
    /// Gets or sets the name of this type.
    /// </summary>
    /// <value>The type name, or null for wrapper types (LIST, NON_NULL).</value>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the description of this type.
    /// </summary>
    /// <value>Human-readable description of the type's purpose.</value>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the fields available on this type.
    /// </summary>
    /// <value>List of fields for OBJECT and INTERFACE types, null for others.</value>
    [JsonPropertyName("fields")]
    public List<GraphQLField>? Fields { get; set; }

    /// <summary>
    /// Gets or sets the interfaces implemented by this type.
    /// </summary>
    /// <value>List of interface types for OBJECT types, null for others.</value>
    [JsonPropertyName("interfaces")]
    public List<GraphQLType>? Interfaces { get; set; }

    /// <summary>
    /// Gets or sets the possible types for this union or interface.
    /// </summary>
    /// <value>List of possible types for UNION and INTERFACE, null for others.</value>
    [JsonPropertyName("possibleTypes")]
    public List<GraphQLType>? PossibleTypes { get; set; }

    /// <summary>
    /// Gets or sets the enum values for this enum type.
    /// </summary>
    /// <value>List of enum values for ENUM types, null for others.</value>
    [JsonPropertyName("enumValues")]
    public List<GraphQLEnumValue>? EnumValues { get; set; }

    /// <summary>
    /// Gets or sets the input fields for this input object type.
    /// </summary>
    /// <value>List of input fields for INPUT_OBJECT types, null for others.</value>
    [JsonPropertyName("inputFields")]
    public List<GraphQLInputValue>? InputFields { get; set; }

    /// <summary>
    /// Gets or sets the wrapped type for LIST and NON_NULL types.
    /// </summary>
    /// <value>The inner type for wrapper types, null for named types.</value>
    [JsonPropertyName("ofType")]
    public GraphQLType? OfType { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphQLType"/> class.
    /// </summary>
    public GraphQLType()
    {
    }

    /// <summary>
    /// Gets the full type name, unwrapping LIST and NON_NULL modifiers.
    /// </summary>
    /// <returns>The underlying named type, or null if this is a wrapper without a name.</returns>
    public string? GetNamedTypeName()
    {
        if (!string.IsNullOrEmpty(Name))
        {
            return Name;
        }

        return OfType?.GetNamedTypeName();
    }

    /// <summary>
    /// Checks if this type is a scalar type.
    /// </summary>
    /// <returns>True if this is a scalar type; otherwise, false.</returns>
    public bool IsScalar()
    {
        return Kind == "SCALAR";
    }

    /// <summary>
    /// Checks if this type is an object type.
    /// </summary>
    /// <returns>True if this is an object type; otherwise, false.</returns>
    public bool IsObject()
    {
        return Kind == "OBJECT";
    }

    /// <summary>
    /// Checks if this type is a list type.
    /// </summary>
    /// <returns>True if this is a list type; otherwise, false.</returns>
    public bool IsList()
    {
        return Kind == "LIST";
    }

    /// <summary>
    /// Checks if this type is a non-null type.
    /// </summary>
    /// <returns>True if this is a non-null type; otherwise, false.</returns>
    public bool IsNonNull()
    {
        return Kind == "NON_NULL";
    }

    /// <summary>
    /// Finds a field by name in this type.
    /// </summary>
    /// <param name="fieldName">The field name to search for.</param>
    /// <returns>The field if found; otherwise, null.</returns>
    public GraphQLField? FindField(string fieldName)
    {
        return Fields?.FirstOrDefault(f => f.Name == fieldName);
    }

    /// <summary>
    /// Gets a string representation of this type for debugging.
    /// </summary>
    /// <returns>A string describing this type.</returns>
    public override string ToString()
    {
        return Name ?? $"{Kind}<{OfType?.ToString() ?? "?"}>";
    }
}

/// <summary>
/// Represents an enum value in a GraphQL enum type.
/// </summary>
public class GraphQLEnumValue
{
    /// <summary>
    /// Gets or sets the name of this enum value.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the description of this enum value.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this enum value is deprecated.
    /// </summary>
    [JsonPropertyName("isDeprecated")]
    public bool IsDeprecated { get; set; }

    /// <summary>
    /// Gets or sets the deprecation reason.
    /// </summary>
    [JsonPropertyName("deprecationReason")]
    public string? DeprecationReason { get; set; }
}

/// <summary>
/// Represents an input value (argument or input field) in GraphQL.
/// </summary>
public class GraphQLInputValue
{
    /// <summary>
    /// Gets or sets the name of this input value.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the description of this input value.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the type of this input value.
    /// </summary>
    [JsonPropertyName("type")]
    public required GraphQLType Type { get; set; }

    /// <summary>
    /// Gets or sets the default value for this input (as a string).
    /// </summary>
    [JsonPropertyName("defaultValue")]
    public string? DefaultValue { get; set; }
}
