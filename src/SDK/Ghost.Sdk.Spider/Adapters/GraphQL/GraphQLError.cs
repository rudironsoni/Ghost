using System.Text.Json.Serialization;

namespace Ghost.Sdk.Spider.Adapters.GraphQL;

/// <summary>
/// Represents a GraphQL error as defined in the GraphQL specification.
/// </summary>
/// <remarks>
/// GraphQL errors provide detailed information about what went wrong during execution,
/// including the location in the query where the error occurred and a path to the
/// specific field that caused the error.
/// </remarks>
public class GraphQLError
{
    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    /// <value>A human-readable description of the error.</value>
    [JsonPropertyName("message")]
    public required string Message { get; set; }

    /// <summary>
    /// Gets or sets the locations in the query where the error occurred.
    /// </summary>
    /// <value>
    /// An array of locations indicating line and column numbers in the query document.
    /// </value>
    /// <remarks>
    /// Locations help developers identify which part of their query caused the error.
    /// Each location contains a line and column number (both 1-indexed).
    /// </remarks>
    [JsonPropertyName("locations")]
    public List<GraphQLErrorLocation>? Locations { get; set; }

    /// <summary>
    /// Gets or sets the path to the field that caused the error.
    /// </summary>
    /// <value>
    /// An array of path segments (strings or integers) indicating where in the result
    /// the error occurred.
    /// </value>
    /// <remarks>
    /// The path represents the path from the root of the response to the specific
    /// field that encountered an error. String segments represent object field names,
    /// while integer segments represent array indices.
    /// </remarks>
    [JsonPropertyName("path")]
    public List<object>? Path { get; set; }

    /// <summary>
    /// Gets or sets additional error information.
    /// </summary>
    /// <value>
    /// A dictionary of extension data providing additional context about the error.
    /// </value>
    /// <remarks>
    /// Extensions may include error codes, exception details, stack traces, or other
    /// server-specific debugging information. The structure is not standardized and
    /// varies by GraphQL server implementation.
    /// </remarks>
    [JsonPropertyName("extensions")]
    public Dictionary<string, object>? Extensions { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphQLError"/> class.
    /// </summary>
    public GraphQLError()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphQLError"/> class with the specified message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public GraphQLError(string message)
    {
        Message = message;
    }

    /// <summary>
    /// Gets the error code from extensions, if available.
    /// </summary>
    /// <returns>The error code string, or null if not present.</returns>
    public string? GetErrorCode()
    {
        if (Extensions?.TryGetValue("code", out object? code) == true)
        {
            return code?.ToString();
        }

        return null;
    }

    /// <summary>
    /// Gets a formatted path string for display.
    /// </summary>
    /// <returns>A string representation of the path, or null if path is not set.</returns>
    public string? GetPathString()
    {
        if (Path == null || Path.Count == 0)
        {
            return null;
        }

        return string.Join(".", Path.Select(p => p.ToString()));
    }

    /// <summary>
    /// Returns a string representation of the error.
    /// </summary>
    /// <returns>A formatted error string including message and path if available.</returns>
    public override string ToString()
    {
        string? pathString = GetPathString();
        return pathString != null ? $"{Message} (at {pathString})" : Message;
    }
}

/// <summary>
/// Represents a location in a GraphQL query document.
/// </summary>
/// <remarks>
/// Locations are used in error reporting to indicate where in the query document
/// an error occurred. Line and column numbers are 1-indexed.
/// </remarks>
public class GraphQLErrorLocation
{
    /// <summary>
    /// Gets or sets the line number in the query document.
    /// </summary>
    /// <value>The 1-indexed line number.</value>
    [JsonPropertyName("line")]
    public int Line { get; set; }

    /// <summary>
    /// Gets or sets the column number in the query document.
    /// </summary>
    /// <value>The 1-indexed column number.</value>
    [JsonPropertyName("column")]
    public int Column { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphQLErrorLocation"/> class.
    /// </summary>
    public GraphQLErrorLocation()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphQLErrorLocation"/> class with the specified position.
    /// </summary>
    /// <param name="line">The line number.</param>
    /// <param name="column">The column number.</param>
    public GraphQLErrorLocation(int line, int column)
    {
        Line = line;
        Column = column;
    }

    /// <summary>
    /// Returns a string representation of the location.
    /// </summary>
    /// <returns>A formatted location string.</returns>
    public override string ToString()
    {
        return $"Line {Line}, Column {Column}";
    }
}
