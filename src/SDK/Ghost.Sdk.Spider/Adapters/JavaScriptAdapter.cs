using Ghost.Sdk.Spider.Adapters.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace Ghost.Sdk.Spider.Adapters;

internal static partial class JavaScriptAdapterLogMessages
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Initializing Playwright browser")]
    public static partial void LogInitializingBrowser(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "JavaScriptAdapter extracting content from {Url}")]
    public static partial void LogExtractingContent(this ILogger logger, string url);

    [LoggerMessage(Level = LogLevel.Debug, Message = "JavaScriptAdapter completed extraction from {Url} in {Duration}ms with status {StatusCode}")]
    public static partial void LogExtractionCompleted(this ILogger logger, string url, double duration, int statusCode);

    [LoggerMessage(Level = LogLevel.Error, Message = "Playwright error extracting content from {Url}")]
    public static partial void LogPlaywrightError(this ILogger logger, Exception ex, string url);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unexpected error extracting content from {Url}")]
    public static partial void LogUnexpectedError(this ILogger logger, Exception ex, string url);
}

/// <summary>
/// Adapter for extracting content from JavaScript-rendered pages using Playwright.
/// </summary>
/// <remarks>
/// This adapter uses headless browsers to execute JavaScript and wait for dynamic content.
/// It's suitable for single-page applications and sites that rely heavily on client-side rendering.
/// </remarks>
public class JavaScriptAdapter : IContentAdapter
{
    private static readonly string[] BrowserArgs = ["--no-sandbox", "--disable-setuid-sandbox", "--disable-dev-shm-usage"];
    private readonly ILogger<JavaScriptAdapter>? _logger;
    private readonly Lazy<Task<IBrowser>> _browserLazy;
    private bool _disposed;

    /// <inheritdoc/>
    public string Name => "JavaScript";

    /// <inheritdoc/>
    public ContentType ContentType => ContentType.JavaScript;

    /// <inheritdoc/>
    public bool IsAvailable => true;

    /// <summary>
    /// Initializes a new instance of the <see cref="JavaScriptAdapter"/> class.
    /// </summary>
    /// <param name="logger">Optional logger for diagnostic information.</param>
    public JavaScriptAdapter(ILogger<JavaScriptAdapter>? logger = null)
    {
        _logger = logger;
        _browserLazy = new Lazy<Task<IBrowser>>(InitializeBrowserAsync);
    }

    private async Task<IBrowser> InitializeBrowserAsync()
    {
        _logger?.LogInitializingBrowser();

        var playwright = await Playwright.CreateAsync().ConfigureAwait(false);
        var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Args = BrowserArgs
        }).ConfigureAwait(false);

        return browser;
    }

    /// <inheritdoc/>
    public Task<bool> CanHandleAsync(Request request, CancellationToken cancellationToken = default)
    {
        if (request == null)
            return Task.FromResult(false);

        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var uri))
            return Task.FromResult(false);

        var scheme = uri.Scheme.ToLowerInvariant();
        var canHandle = scheme is "http" or "https";

        if (canHandle && request.ExpectedContentType != ContentType.Unknown)
        {
            canHandle = request.ExpectedContentType == ContentType.JavaScript;
        }

        return Task.FromResult(canHandle);
    }

    /// <inheritdoc/>
    public Task<Response> ExtractAsync(Request request, CancellationToken cancellationToken = default)
    {
        return ExtractAsync(request, new JavaScriptAdapterOptions(), cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Response> ExtractAsync(
        Request request,
        AdapterOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(options);

        var startTime = DateTimeOffset.UtcNow;
        IPage? page = null;

        try
        {
            if (_logger != null)
                JavaScriptAdapterLogMessages.LogExtractingContent(_logger, request.Url);

            var browser = await _browserLazy.Value.ConfigureAwait(false);
            var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                UserAgent = options.UserAgent,
                IgnoreHTTPSErrors = !options.ValidateSslCertificate,
                Locale = "en-US"
            }).ConfigureAwait(false);

            page = await context.NewPageAsync().ConfigureAwait(false);

            // Set timeout
            page.SetDefaultTimeout((float)options.Timeout.TotalMilliseconds);

            // Set custom headers
            if (request.Headers.Count > 0)
            {
                await page.SetExtraHTTPHeadersAsync(request.Headers).ConfigureAwait(false);
            }

            // Navigate to URL
            var navigationOptions = new PageGotoOptions
            {
                Timeout = (float)request.Timeout.TotalMilliseconds,
                WaitUntil = WaitUntilState.NetworkIdle
            };

            var playwrightResponse = await page.GotoAsync(request.Url, navigationOptions).ConfigureAwait(false);

            ArgumentNullException.ThrowIfNull(playwrightResponse, nameof(playwrightResponse));

            // Wait for content to be ready
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle).ConfigureAwait(false);

            // Extract content
            var content = await page.ContentAsync().ConfigureAwait(false);
            var finalUrl = page.Url;

            var contentResult = new ContentResult
            {
                Content = content,
                ContentType = ContentType.JavaScript,
                MimeType = "text/html",
                Encoding = "utf-8",
                ContentLength = content.Length,
                ExtractedAt = DateTimeOffset.UtcNow,
                Success = playwrightResponse.Ok
            };

            if (!playwrightResponse.Ok)
            {
                contentResult.Error = $"HTTP {playwrightResponse.Status} {playwrightResponse.StatusText}";
            }

            var response = new Response(contentResult)
            {
                StatusCode = playwrightResponse.Status,
                ReasonPhrase = playwrightResponse.StatusText,
                FinalUrl = finalUrl,
                AdapterName = Name,
                IsSuccess = playwrightResponse.Ok,
                RequestedAt = startTime,
                RespondedAt = DateTimeOffset.UtcNow
            };

            // Get response headers
            var headers = await playwrightResponse.AllHeadersAsync().ConfigureAwait(false);
            foreach (var header in headers)
            {
                response.Headers[header.Key] = header.Value;
            }

            if (_logger != null)
                JavaScriptAdapterLogMessages.LogExtractionCompleted(
                    _logger,
                    request.Url,
                    response.Duration.TotalMilliseconds,
                    response.StatusCode ?? 0);

            return response;
        }
        catch (PlaywrightException ex)
        {
            _logger?.LogPlaywrightError(ex, request.Url);
            return CreateErrorResponse($"Browser error: {ex.Message}", ex, startTime, request.Url);
        }
        catch (Exception ex)
        {
            if (_logger != null)
                JavaScriptAdapterLogMessages.LogUnexpectedError(_logger, ex, request.Url);
            return CreateErrorResponse($"Unexpected error: {ex.Message}", ex, startTime, request.Url);
        }
        finally
        {
            if (page != null)
            {
                await page.CloseAsync().ConfigureAwait(false);
            }
        }
    }

    private Response CreateErrorResponse(string error, Exception? exception, DateTimeOffset startTime, string url)
    {
        var contentResult = ContentResult.CreateFailure(error, ContentType.JavaScript);

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

    /// <summary>
    /// Releases resources used by the adapter.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA1816:Dispose methods should call SuppressFinalize", Justification = "This is an async-only disposable class following the IAsyncDisposable pattern. GC.SuppressFinalize is appropriately called in DisposeAsync.")]
    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases unmanaged resources and performs async cleanup.
    /// </summary>
    protected virtual async ValueTask DisposeAsyncCore()
    {
        if (_disposed)
            return;

        if (_browserLazy.IsValueCreated)
        {
            var browser = await _browserLazy.Value.ConfigureAwait(false);
            await browser.CloseAsync().ConfigureAwait(false);
            await browser.DisposeAsync().ConfigureAwait(false);
        }

        _disposed = true;
    }
}
