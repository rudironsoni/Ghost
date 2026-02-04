using System.Text.Json.Serialization;

namespace Ghost.Sdk.Spider.Adapters.GraphQL.Schema;

/// <summary>
/// Represents a field in a GraphQL type.
/// </summary>
/// <remarks>
/// Fields are the properties available on GraphQL objects and interfaces.
/// They can have arguments and return specific types.
/// </remarks>
public class GraphQLField
{
    /// <summary>
    /// Gets or sets the name of this field.
    /// </summary>
    /// <value>The field name as it appears in queries.</value>
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the description of this field.
    /// </summary>
    /// <value>Human-readable description of the field's purpose.</value>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the return type of this field.
    /// </summary>
    /// <value>The GraphQL type this field returns.</value>
    [JsonPropertyName("type")]
    public required GraphQLType Type { get; set; }

    /// <summary>
    /// Gets or sets the arguments accepted by this field.
    /// </summary>
    /// <value>List of input values that can be passed to this field.</value>
    [JsonPropertyName("args")]
    public List<GraphQLInputValue> Args { get; set; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether this field is deprecated.
    /// </summary>
    /// <value><c>true</c> if the field is deprecated; otherwise, <c>false</c>.</value>
    [JsonPropertyName("isDeprecated")]
    public bool IsDeprecated { get; set; }

    /// <summary>
    /// Gets or sets the deprecation reason.
    /// </summary>
    /// <value>Explanation of why the field is deprecated and what to use instead.</value>
    [JsonPropertyName("deprecationReason")]
    public string? DeprecationReason { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphQLField"/> class.
    /// </summary>
    public GraphQLField()
    {
    }

    /// <summary>
    /// Checks if this field accepts any arguments.
    /// </summary>
    /// <returns>True if the field has arguments; otherwise, false.</returns>
    public bool HasArguments()
    {
        return Args.Count > 0;
    }

    /// <summary>
    /// Finds an argument by name.
    /// </summary>
    /// <param name="argName">The argument name to search for.</param>
    /// <returns>The argument if found; otherwise, null.</returns>
    public GraphQLInputValue? FindArgument(string argName)
    {
        return Args.FirstOrDefault(a => a.Name == argName);
    }

    /// <summary>
    /// Gets a string representation of this field for debugging.
    /// </summary>
    /// <returns>A string describing this field.</returns>
    public override string ToString()
    {
        var args = HasArguments() ? $"({string.Join(", ", Args.Select(a => a.Name))})" : string.Empty;
        return $"{Name}{args}: {Type}";
    }
}
