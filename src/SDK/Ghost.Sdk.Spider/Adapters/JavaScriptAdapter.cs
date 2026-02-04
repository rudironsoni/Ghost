using Ghost.Sdk.Spider.Adapters.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace Ghost.Sdk.Spider.Adapters;

/// <summary>
/// Adapter for extracting content from JavaScript-rendered pages using Playwright.
/// </summary>
/// <remarks>
/// This adapter uses headless browsers to execute JavaScript and wait for dynamic content.
/// It's suitable for single-page applications and sites that rely heavily on client-side rendering.
/// </remarks>
public class JavaScriptAdapter : IContentAdapter
{
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
        _logger?.LogInformation("Initializing Playwright browser");
        
        var playwright = await Playwright.CreateAsync().ConfigureAwait(false);
        var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Args = new[] { "--no-sandbox", "--disable-setuid-sandbox", "--disable-dev-shm-usage" }
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
            _logger?.LogDebug("JavaScriptAdapter extracting content from {Url}", request.Url);

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

            if (playwrightResponse == null)
            {
                throw new InvalidOperationException("Navigation did not return a response");
            }

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

            _logger?.LogDebug(
                "JavaScriptAdapter completed extraction from {Url} in {Duration}ms with status {StatusCode}",
                request.Url,
                response.Duration.TotalMilliseconds,
                response.StatusCode);

            return response;
        }
        catch (PlaywrightException ex)
        {
            _logger?.LogError(ex, "Playwright error extracting content from {Url}", request.Url);
            return CreateErrorResponse($"Browser error: {ex.Message}", ex, startTime, request.Url);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error extracting content from {Url}", request.Url);
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
    public async ValueTask DisposeAsync()
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
        GC.SuppressFinalize(this);
    }
}
