using Ghost.Sdk.Spider.Pipeline.Contracts;

namespace Ghost.Sdk.Spider.Pipeline.Middleware;

/// <summary>
/// Middleware that applies token bucket rate limiting to control request throughput.
/// </summary>
/// <remarks>
/// <para>
/// This middleware uses the token bucket algorithm to enforce rate limits on requests.
/// It allows burst traffic while maintaining an average rate limit over time, making it
/// more flexible than simple fixed-window rate limiting.
/// </para>
/// <para>
/// The middleware can be configured per-domain or globally. When configured per-domain,
/// separate rate limiters are maintained for each unique domain to prevent cross-domain
/// rate limit interference.
/// </para>
/// <para>
/// Configuration keys:
/// - Capacity: Maximum burst size (number of tokens, default: 10)
/// - TokensPerSecond: Rate limit in requests per second (default: 1.0)
/// - PerDomain: Apply rate limiting per domain vs globally (default: true)
/// - WaitWhenExceeded: Wait for token vs throw exception (default: true)
/// </para>
/// </remarks>
public sealed class RateLimitMiddleware : IPipelineMiddleware
{
    private readonly int _capacity;
    private readonly double _tokensPerSecond;
    private readonly bool _perDomain;
    private readonly bool _waitWhenExceeded;
    private readonly Dictionary<string, TokenBucketRateLimiter> _domainLimiters;
    private readonly TokenBucketRateLimiter _globalLimiter;
    private readonly object _lock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="RateLimitMiddleware"/> class.
    /// </summary>
    /// <param name="configuration">The middleware configuration dictionary.</param>
    /// <exception cref="ArgumentNullException">Thrown when configuration is null.</exception>
    public RateLimitMiddleware(Dictionary<string, object> configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        _capacity = configuration.TryGetValue("Capacity", out var cap) && cap is int capacity
            ? capacity
            : 10;

        _tokensPerSecond = configuration.TryGetValue("TokensPerSecond", out var tps) && tps is double tokensPerSecond
            ? tokensPerSecond
            : 1.0;

        _perDomain = configuration.TryGetValue("PerDomain", out var pd) && pd is bool perDomain
            ? perDomain
            : true;

        _waitWhenExceeded = configuration.TryGetValue("WaitWhenExceeded", out var wwe) && wwe is bool waitWhenExceeded
            ? waitWhenExceeded
            : true;

        _domainLimiters = new Dictionary<string, TokenBucketRateLimiter>();
        _globalLimiter = new TokenBucketRateLimiter(_capacity, _tokensPerSecond);
    }

    /// <summary>
    /// Invokes the middleware to apply rate limiting to the request.
    /// </summary>
    /// <param name="context">The pipeline context containing the request.</param>
    /// <param name="continuation">The delegate to invoke the next middleware in the pipeline.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when rate limit is exceeded and WaitWhenExceeded is false.
    /// </exception>
    public async Task InvokeAsync(PipelineContext context, PipelineDelegate continuation)
    {
        var limiter = GetLimiter(context);

        if (_waitWhenExceeded)
        {
            // Wait for a token to become available
            await limiter.AcquireAsync(context.CancellationToken);
        }
        else
        {
            // Try to acquire immediately or throw
            if (!limiter.TryAcquire())
            {
                throw new InvalidOperationException(
                    $"Rate limit exceeded. Current rate: {_tokensPerSecond} requests/second, burst: {_capacity}");
            }
        }

        await continuation(context);
    }

    /// <summary>
    /// Gets the appropriate rate limiter for the request based on configuration.
    /// </summary>
    /// <param name="context">The pipeline context.</param>
    /// <returns>The rate limiter to use for this request.</returns>
    private TokenBucketRateLimiter GetLimiter(PipelineContext context)
    {
        if (!_perDomain)
        {
            return _globalLimiter;
        }

        var request = context.GetRequestAs<Adapters.Contracts.Request>();
        if (request == null)
        {
            return _globalLimiter;
        }

        var domain = ExtractDomain(request.Url);
        if (string.IsNullOrEmpty(domain))
        {
            return _globalLimiter;
        }

        lock (_lock)
        {
            if (!_domainLimiters.TryGetValue(domain, out var limiter))
            {
                limiter = new TokenBucketRateLimiter(_capacity, _tokensPerSecond);
                _domainLimiters[domain] = limiter;
            }

            return limiter;
        }
    }

    /// <summary>
    /// Extracts the domain from a URL.
    /// </summary>
    /// <param name="url">The URL to parse.</param>
    /// <returns>The domain, or empty string if parsing fails.</returns>
    private static string ExtractDomain(string url)
    {
        try
        {
            var uri = new Uri(url);
            return uri.Host;
        }
        catch
        {
            return string.Empty;
        }
    }
}
