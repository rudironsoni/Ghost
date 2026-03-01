namespace Ghost.Sdk.Spider.Adapters.GraphQL;

/// <summary>
/// Represents a GraphQL request.
/// </summary>
/// <remarks>
/// This class encapsulates all components of a GraphQL request including the query,
/// variables, operation name, and extensions. It follows the GraphQL over HTTP specification.
/// </remarks>
public class GraphQLRequest
{
    /// <summary>
    /// Gets or sets the GraphQL query or mutation string.
    /// </summary>
    /// <value>The GraphQL operation as a string.</value>
    /// <remarks>
    /// This should be a valid GraphQL query, mutation, or subscription operation.
    /// The query should follow GraphQL syntax and may include operation name,
    /// variables, fragments, and directives.
    /// </remarks>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the operation name to execute.
    /// </summary>
    /// <value>
    /// The name of the operation to execute, or null if the query contains only one operation.
    /// </value>
    /// <remarks>
    /// When a query document contains multiple operations, this field specifies which
    /// operation should be executed. If the document contains only one operation,
    /// this field is optional.
    /// </remarks>
    public string? OperationName { get; set; }

    /// <summary>
    /// Gets or sets the variables for the GraphQL operation.
    /// </summary>
    /// <value>A dictionary of variable name-value pairs, or null if no variables are used.</value>
    /// <remarks>
    /// Variables allow parameterized queries and are referenced in the query using
    /// the $ syntax (e.g., $userId). Variable values should match the types defined
    /// in the query operation.
    /// </remarks>
    public Dictionary<string, object>? Variables { get; set; }

    /// <summary>
    /// Gets or sets extensions for the GraphQL request.
    /// </summary>
    /// <value>
    /// A dictionary of extension name-value pairs for protocol extensions or metadata.
    /// </value>
    /// <remarks>
    /// Extensions are a mechanism for clients to provide additional information to the server
    /// that is not part of the GraphQL query itself. Common uses include tracing, caching
    /// directives, or persisted query identifiers.
    /// </remarks>
    public Dictionary<string, object>? Extensions { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphQLRequest"/> class.
    /// </summary>
    public GraphQLRequest()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphQLRequest"/> class with the specified query.
    /// </summary>
    /// <param name="query">The GraphQL query string.</param>
    public GraphQLRequest(string query)
    {
        Query = query;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphQLRequest"/> class with the specified query and variables.
    /// </summary>
    /// <param name="query">The GraphQL query string.</param>
    /// <param name="variables">The variables for the query.</param>
    public GraphQLRequest(string query, Dictionary<string, object>? variables)
    {
        Query = query;
        Variables = variables;
    }

    /// <summary>
    /// Creates a query request.
    /// </summary>
    /// <param name="query">The GraphQL query string.</param>
    /// <returns>A new <see cref="GraphQLRequest"/> instance.</returns>
    public static GraphQLRequest CreateQuery(string query)
    {
        return new GraphQLRequest(query);
    }

    /// <summary>
    /// Creates a mutation request.
    /// </summary>
    /// <param name="mutation">The GraphQL mutation string.</param>
    /// <returns>A new <see cref="GraphQLRequest"/> instance.</returns>
    public static GraphQLRequest CreateMutation(string mutation)
    {
        return new GraphQLRequest(mutation);
    }

    /// <summary>
    /// Adds a variable to the request.
    /// </summary>
    /// <param name="name">The variable name (without the $ prefix).</param>
    /// <param name="value">The variable value.</param>
    /// <returns>This <see cref="GraphQLRequest"/> instance for method chaining.</returns>
    public GraphQLRequest WithVariable(string name, object value)
    {
        Variables ??= [];
        Variables[name] = value;
        return this;
    }

    /// <summary>
    /// Sets the operation name for the request.
    /// </summary>
    /// <param name="operationName">The operation name.</param>
    /// <returns>This <see cref="GraphQLRequest"/> instance for method chaining.</returns>
    public GraphQLRequest WithOperationName(string operationName)
    {
        OperationName = operationName;
        return this;
    }

    /// <summary>
    /// Adds an extension to the request.
    /// </summary>
    /// <param name="name">The extension name.</param>
    /// <param name="value">The extension value.</param>
    /// <returns>This <see cref="GraphQLRequest"/> instance for method chaining.</returns>
    public GraphQLRequest WithExtension(string name, object value)
    {
        Extensions ??= [];
        Extensions[name] = value;
        return this;
    }
}
