using System.Net;
using System.Net.Http.Headers;
using Ghost.Sdk.Spider.Adapters.Contracts;
using Microsoft.Extensions.Logging;

namespace Ghost.Sdk.Spider.Adapters;

internal static partial class StaticHtmlAdapterLogMessages
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "StaticHtmlAdapter extracting content from {Url}")]
    public static partial void LogExtractingContent(this ILogger logger, string url);

    [LoggerMessage(Level = LogLevel.Debug, Message = "StaticHtmlAdapter completed extraction from {Url} in {Duration}ms with status {StatusCode}")]
    public static partial void LogExtractionCompleted(this ILogger logger, string url, double duration, int statusCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Extraction from {Url} was canceled")]
    public static partial void LogExtractionCanceled(this ILogger logger, Exception ex, string url);

    [LoggerMessage(Level = LogLevel.Error, Message = "HTTP request failed for {Url}")]
    public static partial void LogHttpRequestFailed(this ILogger logger, Exception ex, string url);

    [LoggerMessage(Level = LogLevel.Error, Message = "Request to {Url} timed out after {Timeout}")]
    public static partial void LogRequestTimedOut(this ILogger logger, Exception ex, string url, TimeSpan timeout);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unexpected error extracting content from {Url}")]
    public static partial void LogUnexpectedError(this ILogger logger, Exception ex, string url);
}

/// <summary>
/// Adapter for extracting content from static HTML pages using HttpClient.
/// </summary>
/// <remarks>
/// This adapter is optimized for pages that don't require JavaScript execution.
/// It uses HttpClient for efficient HTTP communication with support for:
/// <list type="bullet">
/// <item>Connection pooling and reuse</item>
/// <item>Automatic decompression</item>
/// <item>Cookie management</item>
/// <item>Proxy support</item>
/// <item>HTTP/2 and HTTP/3</item>
/// </list>
/// </remarks>
public class StaticHtmlAdapter : IContentAdapter
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<StaticHtmlAdapter>? _logger;
    private readonly StaticHtmlAdapterOptions _defaultOptions;

    /// <inheritdoc/>
    public string Name => "StaticHtml";

    /// <inheritdoc/>
    public ContentType ContentType => ContentType.StaticHtml;

    /// <inheritdoc/>
    public bool IsAvailable => true;

    /// <summary>
    /// Initializes a new instance of the <see cref="StaticHtmlAdapter"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client to use for requests.</param>
    /// <param name="logger">Optional logger for diagnostic information.</param>
    public StaticHtmlAdapter(HttpClient httpClient, ILogger<StaticHtmlAdapter>? logger = null)
        : this(httpClient, new StaticHtmlAdapterOptions(), logger)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StaticHtmlAdapter"/> class with custom options.
    /// </summary>
    /// <param name="httpClient">The HTTP client to use for requests.</param>
    /// <param name="options">The default adapter options.</param>
    /// <param name="logger">Optional logger for diagnostic information.</param>
    public StaticHtmlAdapter(
        HttpClient httpClient,
        StaticHtmlAdapterOptions options,
        ILogger<StaticHtmlAdapter>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        _httpClient = httpClient;
        _defaultOptions = options;
        _logger = logger;

        ConfigureHttpClient(options);
    }

    /// <inheritdoc/>
    public Task<bool> CanHandleAsync(Request request, CancellationToken cancellationToken = default)
    {
        if (request == null)
            return Task.FromResult(false);

        // Can handle HTTP/HTTPS URLs
        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out Uri? uri))
            return Task.FromResult(false);

        string scheme = uri.Scheme.ToLowerInvariant();
        bool canHandle = scheme is "http" or "https";

        // Prefer handling static HTML content
        if (canHandle && request.ExpectedContentType != ContentType.Unknown)
        {
            canHandle = request.ExpectedContentType == ContentType.StaticHtml ||
                        request.ExpectedContentType == ContentType.PlainText ||
                        request.ExpectedContentType == ContentType.Xml;
        }

        return Task.FromResult(canHandle);
    }

    /// <inheritdoc/>
    public Task<Response> ExtractAsync(Request request, CancellationToken cancellationToken = default)
    {
        return ExtractAsync(request, _defaultOptions, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Response> ExtractAsync(
        Request request,
        AdapterOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(options);

        StaticHtmlAdapterOptions staticOptions = options as StaticHtmlAdapterOptions ?? _defaultOptions;
        DateTimeOffset startTime = DateTimeOffset.UtcNow;

        try
        {
            if (_logger != null)
                StaticHtmlAdapterLogMessages.LogExtractingContent(_logger, request.Url);

            using HttpRequestMessage httpRequest = CreateHttpRequestMessage(request, staticOptions);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(request.Timeout);

            using HttpResponseMessage httpResponse = await _httpClient.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseContentRead,
                cts.Token).ConfigureAwait(false);

            Response response = await CreateResponseAsync(
                httpResponse,
                request,
                startTime,
                cancellationToken).ConfigureAwait(false);

            if (_logger != null)
                StaticHtmlAdapterLogMessages.LogExtractionCompleted(
                    _logger,
                    request.Url,
                    response.Duration.TotalMilliseconds,
                    response.StatusCode ?? 0);

            return response;
        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            _logger?.LogExtractionCanceled(ex, request.Url);
            return CreateErrorResponse("Request was canceled", ex, startTime, request.Url);
        }
        catch (HttpRequestException ex)
        {
            if (_logger != null)
                StaticHtmlAdapterLogMessages.LogHttpRequestFailed(_logger, ex, request.Url);
            return CreateErrorResponse($"HTTP request failed: {ex.Message}", ex, startTime, request.Url);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger?.LogRequestTimedOut(ex, request.Url, request.Timeout);
            return CreateErrorResponse($"Request timed out after {request.Timeout}", ex, startTime, request.Url);
        }
        catch (Exception ex)
        {
            if (_logger != null)
                StaticHtmlAdapterLogMessages.LogUnexpectedError(_logger, ex, request.Url);
            return CreateErrorResponse($"Unexpected error: {ex.Message}", ex, startTime, request.Url);
        }
    }

    private void ConfigureHttpClient(StaticHtmlAdapterOptions options)
    {
        // Set default timeout
        _httpClient.Timeout = options.Timeout;

        // Set default headers
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", options.UserAgent);
        _httpClient.DefaultRequestHeaders.Add("Accept", options.AcceptHeader);
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", options.AcceptLanguage);
        _httpClient.DefaultRequestHeaders.Add("Accept-Encoding", options.AcceptEncoding);

        // Add custom headers
        foreach (KeyValuePair<string, string> header in options.CustomHeaders)
        {
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
        }
    }

    private static HttpRequestMessage CreateHttpRequestMessage(Request request, StaticHtmlAdapterOptions options)
    {
        var httpRequest = new HttpRequestMessage(new HttpMethod(request.Method), request.Url);

        // Add request-specific headers (override defaults)
        foreach (KeyValuePair<string, string> header in request.Headers)
        {
            httpRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        // Add cookies
        if (options.Cookies.Count > 0)
        {
            string cookieHeader = string.Join("; ", options.Cookies.Select(c => $"{c.Key}={c.Value}"));
            httpRequest.Headers.TryAddWithoutValidation("Cookie", cookieHeader);
        }

        // Add body for POST/PUT/PATCH requests
        if (!string.IsNullOrEmpty(request.Body) &&
            (request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase) ||
            request.Method.Equals("PUT", StringComparison.OrdinalIgnoreCase) ||
            request.Method.Equals("PATCH", StringComparison.OrdinalIgnoreCase)))
        {
            httpRequest.Content = new StringContent(request.Body);

            // Set content type if specified in headers
            if (request.Headers.TryGetValue("Content-Type", out string? contentType))
            {
                httpRequest.Content!.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
            }
        }

        return httpRequest;
    }

    private async Task<Response> CreateResponseAsync(
        HttpResponseMessage httpResponse,
        Request request,
        DateTimeOffset startTime,
        CancellationToken cancellationToken)
    {
        string content = await httpResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        string contentType = httpResponse.Content.Headers.ContentType?.MediaType ?? "text/html";
        string encoding = httpResponse.Content.Headers.ContentType?.CharSet ?? "utf-8";

        var contentResult = new ContentResult
        {
            Content = content,
            ContentType = ContentTypeExtensions.FromMimeType(contentType),
            MimeType = contentType,
            Encoding = encoding,
            ContentLength = content.Length,
            ExtractedAt = DateTimeOffset.UtcNow,
            Success = httpResponse.IsSuccessStatusCode
        };

        if (!httpResponse.IsSuccessStatusCode)
        {
            contentResult.Error = $"HTTP {(int)httpResponse.StatusCode} {httpResponse.ReasonPhrase}";
        }

        var response = new Response(contentResult)
        {
            StatusCode = (int)httpResponse.StatusCode,
            ReasonPhrase = httpResponse.ReasonPhrase,
            FinalUrl = httpResponse.RequestMessage?.RequestUri?.ToString() ?? request.Url,
            AdapterName = Name,
            IsSuccess = httpResponse.IsSuccessStatusCode,
            RequestedAt = startTime,
            RespondedAt = DateTimeOffset.UtcNow
        };

        // Copy response headers
        foreach (KeyValuePair<string, IEnumerable<string>> header in httpResponse.Headers)
        {
            response.Headers[header.Key] = string.Join(", ", header.Value);
        }

        foreach (KeyValuePair<string, IEnumerable<string>> header in httpResponse.Content.Headers)
        {
            response.Headers[header.Key] = string.Join(", ", header.Value);
        }

        // Track redirect count
        if (response.FinalUrl != request.Url)
        {
            response.RedirectCount = 1; // Simplified - HttpClient handles redirects internally
        }

        return response;
    }

    private Response CreateErrorResponse(string error, Exception? exception, DateTimeOffset startTime, string url)
    {
        var contentResult = ContentResult.CreateFailure(error, ContentType.StaticHtml);

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
