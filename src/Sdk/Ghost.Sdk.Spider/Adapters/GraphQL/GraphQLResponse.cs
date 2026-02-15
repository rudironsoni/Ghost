using System.Text.Json.Serialization;

namespace Ghost.Sdk.Spider.Adapters.GraphQL;

/// <summary>
/// Represents a GraphQL response.
/// </summary>
/// <remarks>
/// This class follows the GraphQL specification for response format, containing
/// the data, errors, and extensions fields as defined in the GraphQL over HTTP spec.
/// </remarks>
public class GraphQLResponse
{
    /// <summary>
    /// Gets or sets the data returned by the GraphQL operation.
    /// </summary>
    /// <value>
    /// The result data as a JSON object, or null if the operation had no data or failed completely.
    /// </value>
    /// <remarks>
    /// The data field contains the result of the requested operation. If the operation
    /// encountered errors, this field may still contain partial data for fields that
    /// were successfully resolved.
    /// </remarks>
    [JsonPropertyName("data")]
    public object? Data { get; set; }

    /// <summary>
    /// Gets or sets the errors encountered during execution.
    /// </summary>
    /// <value>
    /// An array of errors, or null if the operation completed without errors.
    /// </value>
    /// <remarks>
    /// According to the GraphQL specification, errors may be present even when data
    /// is returned, indicating partial success. Each error provides details about
    /// what went wrong and where in the query it occurred.
    /// </remarks>
    [JsonPropertyName("errors")]
    public List<GraphQLError>? Errors { get; set; }

    /// <summary>
    /// Gets or sets extensions for the GraphQL response.
    /// </summary>
    /// <value>
    /// A dictionary of extension data, typically used for metadata, tracing, or custom information.
    /// </value>
    /// <remarks>
    /// Extensions are optional and server-specific. Common uses include execution timing,
    /// query complexity metrics, or debugging information. The structure of extension
    /// data is defined by the server implementation.
    /// </remarks>
    [JsonPropertyName("extensions")]
    public Dictionary<string, object>? Extensions { get; set; }

    /// <summary>
    /// Gets a value indicating whether the response has errors.
    /// </summary>
    /// <value><c>true</c> if the response contains errors; otherwise, <c>false</c>.</value>
    [JsonIgnore]
    public bool HasErrors => Errors?.Count > 0;

    /// <summary>
    /// Gets a value indicating whether the response is successful.
    /// </summary>
    /// <value><c>true</c> if the response has data and no errors; otherwise, <c>false</c>.</value>
    [JsonIgnore]
    public bool IsSuccess => Data != null && !HasErrors;

    /// <summary>
    /// Gets a value indicating whether the response has partial data.
    /// </summary>
    /// <value><c>true</c> if the response has both data and errors; otherwise, <c>false</c>.</value>
    /// <remarks>
    /// Partial data occurs when some fields resolve successfully while others fail.
    /// This is a common scenario in GraphQL where errors don't necessarily prevent
    /// the entire response from being useful.
    /// </remarks>
    [JsonIgnore]
    public bool HasPartialData => Data != null && HasErrors;

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphQLResponse"/> class.
    /// </summary>
    public GraphQLResponse()
    {
    }

    /// <summary>
    /// Gets all error messages concatenated.
    /// </summary>
    /// <returns>A single string containing all error messages, or an empty string if no errors.</returns>
    public string GetErrorMessage()
    {
        if (!HasErrors)
        {
            return string.Empty;
        }

        return string.Join("; ", Errors!.Select(e => e.Message));
    }

    /// <summary>
    /// Creates a successful response with the specified data.
    /// </summary>
    /// <param name="data">The response data.</param>
    /// <returns>A new <see cref="GraphQLResponse"/> instance.</returns>
    public static GraphQLResponse Success(object data)
    {
        return new GraphQLResponse { Data = data };
    }

    /// <summary>
    /// Creates an error response with the specified errors.
    /// </summary>
    /// <param name="errors">The errors that occurred.</param>
    /// <returns>A new <see cref="GraphQLResponse"/> instance.</returns>
    public static GraphQLResponse Error(params GraphQLError[] errors)
    {
        return new GraphQLResponse { Errors = errors.ToList() };
    }

    /// <summary>
    /// Creates an error response with the specified error message.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <returns>A new <see cref="GraphQLResponse"/> instance.</returns>
    public static GraphQLResponse Error(string message)
    {
        return new GraphQLResponse
        {
            Errors = new List<GraphQLError>
            {
                new() { Message = message }
            }
        };
    }
}
