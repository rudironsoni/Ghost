using System.Net.Http.Headers;
using System.Text;
using Ghost.Sdk.Spider.Adapters.Contracts;
using Ghost.Sdk.Spider.Adapters.GraphQL;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Ghost.Sdk.Spider.Adapters;

internal static partial class GraphQLAdapterLogMessages
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "GraphQLAdapter executing query at {Url}")]
    public static partial void LogExecutingQuery(this ILogger logger, string url);

    [LoggerMessage(Level = LogLevel.Debug, Message = "GraphQLAdapter completed query at {Url} in {Duration}ms")]
    public static partial void LogQueryCompleted(this ILogger logger, string url, double duration);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to parse GraphQL request/response for {Url}")]
    public static partial void LogParseError(this ILogger logger, Exception ex, string url);

    [LoggerMessage(Level = LogLevel.Error, Message = "HTTP request failed for {Url}")]
    public static partial void LogHttpRequestFailed(this ILogger logger, Exception ex, string url);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unexpected error executing GraphQL query at {Url}")]
    public static partial void LogUnexpectedError(this ILogger logger, Exception ex, string url);
}

/// <summary>
/// Adapter for extracting content from GraphQL APIs.
/// </summary>
/// <remarks>
/// This adapter handles GraphQL query execution, variable substitution,
/// and parsing of GraphQL response structures including data, errors, and extensions.
/// </remarks>
public class GraphQLAdapter : IContentAdapter
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GraphQLAdapter>? _logger;

    /// <inheritdoc/>
    public string Name => "GraphQL";

    /// <inheritdoc/>
    public ContentType ContentType => ContentType.GraphQL;

    /// <inheritdoc/>
    public bool IsAvailable => true;

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphQLAdapter"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client to use for requests.</param>
    /// <param name="logger">Optional logger for diagnostic information.</param>
    public GraphQLAdapter(HttpClient httpClient, ILogger<GraphQLAdapter>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<bool> CanHandleAsync(Request request, CancellationToken cancellationToken = default)
    {
        if (request == null)
            return Task.FromResult(false);

        // Check if it's a GraphQL request
        bool canHandle = request.ExpectedContentType == ContentType.GraphQL ||
                       request.Url.Contains("/graphql", StringComparison.OrdinalIgnoreCase) ||
                       request.Headers.ContainsKey("X-GraphQL-Request");

        return Task.FromResult(canHandle);
    }

    /// <inheritdoc/>
    public Task<Response> ExtractAsync(Request request, CancellationToken cancellationToken = default)
    {
        return ExtractAsync(request, new GraphQLAdapterOptions(), cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Response> ExtractAsync(
        Request request,
        AdapterOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(options);

        DateTimeOffset startTime = DateTimeOffset.UtcNow;

        try
        {
            _logger?.LogExecutingQuery(request.Url);

            // Parse GraphQL request from body or metadata
            GraphQLRequest graphQLRequest;
            if (!string.IsNullOrEmpty(request.Body))
            {
                graphQLRequest = JsonConvert.DeserializeObject<GraphQLRequest>(request.Body)
                    ?? throw new InvalidOperationException("Failed to parse GraphQL request from body");
            }
            else if (request.Metadata.TryGetValue("Query", out object? queryObj))
            {
                graphQLRequest = new GraphQLRequest
                {
                    Query = queryObj.ToString() ?? throw new InvalidOperationException("Query is null")
                };

                if (request.Metadata.TryGetValue("Variables", out object? varsObj))
                {
                    graphQLRequest.Variables = varsObj as Dictionary<string, object>;
                }

                if (request.Metadata.TryGetValue("OperationName", out object? opNameObj))
                {
                    graphQLRequest.OperationName = opNameObj.ToString();
                }
            }
            else
            {
                throw new InvalidOperationException("No GraphQL query provided in request body or metadata");
            }

            // Create HTTP request
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, request.Url)
            {
                Content = new StringContent(
                    JsonConvert.SerializeObject(graphQLRequest),
                    Encoding.UTF8,
                    "application/json")
            };

            // Add headers
            httpRequest.Headers.Add("User-Agent", options.UserAgent);
            foreach (KeyValuePair<string, string> header in request.Headers)
            {
                httpRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            // Execute request
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(request.Timeout);

            using HttpResponseMessage httpResponse = await _httpClient.SendAsync(httpRequest, cts.Token).ConfigureAwait(false);
            string responseContent = await httpResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            // Parse GraphQL response
            GraphQLResponse graphQLResponse = JsonConvert.DeserializeObject<GraphQLResponse>(responseContent)
                ?? throw new InvalidOperationException("Failed to parse GraphQL response");

            var contentResult = new ContentResult
            {
                Content = responseContent,
                ContentType = ContentType.GraphQL,
                MimeType = "application/json",
                Encoding = "utf-8",
                ContentLength = responseContent.Length,
                ExtractedAt = DateTimeOffset.UtcNow,
                Success = graphQLResponse.Errors == null || graphQLResponse.Errors.Count == 0
            };

            if (graphQLResponse.Errors != null && graphQLResponse.Errors.Count > 0)
            {
                string errorMessages = string.Join("; ", graphQLResponse.Errors.Select(e => e.Message));
                contentResult.Error = $"GraphQL errors: {errorMessages}";
            }

            var response = new Response(contentResult)
            {
                StatusCode = (int)httpResponse.StatusCode,
                ReasonPhrase = httpResponse.ReasonPhrase,
                FinalUrl = request.Url,
                AdapterName = Name,
                IsSuccess = httpResponse.IsSuccessStatusCode && contentResult.Success,
                RequestedAt = startTime,
                RespondedAt = DateTimeOffset.UtcNow
            };

            // Copy response headers
            foreach (KeyValuePair<string, IEnumerable<string>> header in httpResponse.Headers)
            {
                response.Headers[header.Key] = string.Join(", ", header.Value);
            }

            // Add GraphQL-specific metadata
            if (graphQLResponse.Extensions != null)
            {
                response.Metadata["GraphQL.Extensions"] = graphQLResponse.Extensions;
            }

            _logger?.LogQueryCompleted(
                request.Url,
                response.Duration.TotalMilliseconds);

            return response;
        }
        catch (JsonException ex)
        {
            _logger?.LogParseError(ex, request.Url);
            return CreateErrorResponse($"JSON parsing error: {ex.Message}", ex, startTime, request.Url);
        }
        catch (HttpRequestException ex)
        {
            if (_logger != null)
                GraphQLAdapterLogMessages.LogHttpRequestFailed(_logger, ex, request.Url);
            return CreateErrorResponse($"HTTP request failed: {ex.Message}", ex, startTime, request.Url);
        }
        catch (Exception ex)
        {
            if (_logger != null)
                GraphQLAdapterLogMessages.LogUnexpectedError(_logger, ex, request.Url);
            return CreateErrorResponse($"Unexpected error: {ex.Message}", ex, startTime, request.Url);
        }
    }

    private Response CreateErrorResponse(string error, Exception? exception, DateTimeOffset startTime, string url)
    {
        var contentResult = ContentResult.CreateFailure(error, ContentType.GraphQL);

        return new Response(contentResult)
        {
            IsSuccess = false,
            Error = error,
            Exception = exception,
            AdapterName = Name,
            FinalUrl = url,
            RequestedAt = startTime,
            RespondedAt = DateTimeOffset.UtcNow
        };
    }
}
