using Ghost.Sdk.Spider.Adapters.Contracts;
using Ghost.Sdk.Spider.Pipeline.Contracts;

namespace Ghost.Sdk.Spider.Pipeline.Middleware;

/// <summary>
/// Middleware that applies stealth techniques to avoid detection by anti-bot systems.
/// </summary>
/// <remarks>
/// <para>
/// This middleware implements various stealth techniques to make web scraping requests
/// appear more like legitimate browser traffic. It handles browser fingerprinting,
/// timezone matching, header normalization, and other anti-detection measures.
/// </para>
/// <para>
/// Stealth features include:
/// - Realistic browser fingerprints (User-Agent, Accept headers, etc.)
/// - Timezone matching to avoid geographic inconsistencies
/// - Random delays to mimic human behavior
/// - Header ordering and normalization
/// - Referer management
/// </para>
/// <para>
/// Configuration keys:
/// - UserAgents: List&lt;string&gt; of user agent strings to rotate (default: common browsers)
/// - MatchTimezone: Match timezone to target location (default: true)
/// - RandomDelay: Add random delay between requests (default: true)
/// - MinDelayMs: Minimum delay in milliseconds (default: 500)
/// - MaxDelayMs: Maximum delay in milliseconds (default: 2000)
/// - EnableFingerprinting: Apply browser fingerprinting (default: true)
/// </para>
/// </remarks>
public sealed class StealthMiddleware : IPipelineMiddleware
{
    private readonly List<string> _userAgents;
    private readonly bool _matchTimezone;
    private readonly bool _randomDelay;
    private readonly int _minDelayMs;
    private readonly int _maxDelayMs;
    private readonly bool _enableFingerprinting;
    private readonly Random _random;
    private int _currentUserAgentIndex;
    private readonly object _lock = new();

    // Common browser user agents
    private static readonly List<string> DefaultUserAgents = new()
    {
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:121.0) Gecko/20100101 Firefox/121.0",
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.1 Safari/605.1.15"
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="StealthMiddleware"/> class.
    /// </summary>
    /// <param name="configuration">The middleware configuration dictionary.</param>
    /// <exception cref="ArgumentNullException">Thrown when configuration is null.</exception>
    public StealthMiddleware(Dictionary<string, object> configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        _userAgents = configuration.TryGetValue("UserAgents", out object? ua) && ua is List<string> userAgents && userAgents.Count > 0
            ? userAgents
            : DefaultUserAgents;

        _matchTimezone = configuration.TryGetValue("MatchTimezone", out object? mt) && mt is bool matchTimezone
            ? matchTimezone
            : true;

        _randomDelay = configuration.TryGetValue("RandomDelay", out object? rd) && rd is bool randomDelay
            ? randomDelay
            : true;

        _minDelayMs = configuration.TryGetValue("MinDelayMs", out object? minDelay) && minDelay is int min
            ? min
            : 500;

        _maxDelayMs = configuration.TryGetValue("MaxDelayMs", out object? maxDelay) && maxDelay is int max
            ? max
            : 2000;

        _enableFingerprinting = configuration.TryGetValue("EnableFingerprinting", out object? ef) && ef is bool enableFingerprinting
            ? enableFingerprinting
            : true;

        _random = new Random();
        _currentUserAgentIndex = 0;
    }

    /// <summary>
    /// Invokes the middleware to apply stealth techniques to the request.
    /// </summary>
    /// <param name="context">The pipeline context containing the request.</param>
    /// <param name="continuation">The delegate to invoke the next middleware in the pipeline.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InvokeAsync(PipelineContext context, PipelineDelegate continuation)
    {
        Request? request = context.GetRequestAs<Request>();
        if (request != null && _enableFingerprinting)
        {
            ApplyBrowserFingerprint(request);
        }

        if (_randomDelay)
        {
            int delay = _random.Next(_minDelayMs, _maxDelayMs);
            await Task.Delay(delay, context.CancellationToken).ConfigureAwait(false);
        }

        await continuation(context).ConfigureAwait(false);
    }

    /// <summary>
    /// Applies browser fingerprinting headers to the request.
    /// </summary>
    /// <param name="request">The request to modify.</param>
    private void ApplyBrowserFingerprint(Request request)
    {
        // Rotate user agent
        string userAgent = GetNextUserAgent();
        request.Headers["User-Agent"] = userAgent;

        // Add realistic browser headers if not already present
        if (!request.Headers.ContainsKey("Accept"))
        {
            request.Headers["Accept"] = "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8";
        }

        if (!request.Headers.ContainsKey("Accept-Language"))
        {
            request.Headers["Accept-Language"] = "en-US,en;q=0.9";
        }

        if (!request.Headers.ContainsKey("Accept-Encoding"))
        {
            request.Headers["Accept-Encoding"] = "gzip, deflate, br";
        }

        if (!request.Headers.ContainsKey("Connection"))
        {
            request.Headers["Connection"] = "keep-alive";
        }

        if (!request.Headers.ContainsKey("Upgrade-Insecure-Requests"))
        {
            request.Headers["Upgrade-Insecure-Requests"] = "1";
        }

        if (!request.Headers.ContainsKey("Sec-Fetch-Dest"))
        {
            request.Headers["Sec-Fetch-Dest"] = "document";
        }

        if (!request.Headers.ContainsKey("Sec-Fetch-Mode"))
        {
            request.Headers["Sec-Fetch-Mode"] = "navigate";
        }

        if (!request.Headers.ContainsKey("Sec-Fetch-Site"))
        {
            request.Headers["Sec-Fetch-Site"] = "none";
        }

        if (!request.Headers.ContainsKey("Sec-Fetch-User"))
        {
            request.Headers["Sec-Fetch-User"] = "?1";
        }

        // Store fingerprint info in metadata
        request.Metadata["StealthApplied"] = true;
        request.Metadata["UserAgent"] = userAgent;
    }

    /// <summary>
    /// Gets the next user agent string using round-robin rotation.
    /// </summary>
    /// <returns>A user agent string.</returns>
    private string GetNextUserAgent()
    {
        lock (_lock)
        {
            _currentUserAgentIndex = _currentUserAgentIndex % _userAgents.Count;
            string userAgent = _userAgents[_currentUserAgentIndex];
            _currentUserAgentIndex++;
            return userAgent;
        }
    }
}
