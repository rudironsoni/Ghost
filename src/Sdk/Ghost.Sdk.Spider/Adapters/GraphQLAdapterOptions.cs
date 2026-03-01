using Ghost.Sdk.Spider.Adapters.Contracts;

namespace Ghost.Sdk.Spider.Adapters;

/// <summary>
/// Configuration options specific to the GraphQLAdapter.
/// </summary>
/// <remarks>
/// This class extends the base <see cref="AdapterOptions"/> with GraphQL-specific
/// configuration options for making GraphQL requests.
/// </remarks>
public class GraphQLAdapterOptions : AdapterOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to include the query in error messages.
    /// </summary>
    /// <value>
    /// <c>true</c> to include the query in error messages; otherwise, <c>false</c>.
    /// Defaults to <c>false</c>.
    /// </value>
    /// <remarks>
    /// For security reasons, it's recommended to keep this disabled in production
    /// to avoid exposing sensitive query information in logs or error messages.
    /// </remarks>
    public bool IncludeQueryInErrors { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to include extensions in the request.
    /// </summary>
    /// <value>
    /// <c>true</c> to include extensions; otherwise, <c>false</c>.
    /// Defaults to <c>true</c>.
    /// </value>
    public bool IncludeExtensions { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum query depth allowed.
    /// </summary>
    /// <value>The maximum query depth, or null for no limit. Defaults to null.</value>
    /// <remarks>
    /// Some GraphQL servers limit query depth to prevent complex queries that
    /// could cause performance issues. Check your server's documentation.
    /// </remarks>
    public int? MaxQueryDepth { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to batch multiple queries.
    /// </summary>
    /// <value>
    /// <c>true</c> to enable query batching; otherwise, <c>false</c>.
    /// Defaults to <c>false</c>.
    /// </value>
    /// <remarks>
    /// Query batching is only supported by servers that implement the batching extension.
    /// </remarks>
    public bool EnableBatching { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphQLAdapterOptions"/> class.
    /// </summary>
    public GraphQLAdapterOptions()
    {
    }

    /// <summary>
    /// Validates the GraphQLAdapter-specific options.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when configuration values are invalid.</exception>
    public override void Validate()
    {
        base.Validate();

        if (MaxQueryDepth.HasValue && MaxQueryDepth.Value <= 0)
        {
            throw new ArgumentException("MaxQueryDepth must be greater than zero when specified.", nameof(MaxQueryDepth));
        }
    }
}
