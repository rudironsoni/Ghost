using Ghost.Sdk.Spider.Adapters.Contracts;
using Ghost.Sdk.Spider.Pipeline.Contracts;

namespace Ghost.Sdk.Spider.Pipeline.Middleware;

/// <summary>
/// Middleware that rotates proxies for each request to distribute load and avoid IP-based rate limiting.
/// </summary>
/// <remarks>
/// <para>
/// This middleware manages a pool of proxy servers and rotates them across requests using
/// a round-robin or random selection strategy. It helps prevent IP blocking by distributing
/// requests across multiple proxy endpoints.
/// </para>
/// <para>
/// The proxy pool can be configured with health checking to automatically remove unhealthy
/// proxies from rotation. Failed proxies are temporarily marked as unavailable and retried
/// after a configurable cooldown period.
/// </para>
/// <para>
/// Configuration keys:
/// - ProxyList: List&lt;string&gt; of proxy URLs (e.g., "http://proxy1.com:8080")
/// - RotationStrategy: "RoundRobin" or "Random" (default: RoundRobin)
/// - HealthCheckEnabled: Enable automatic health checking (default: true)
/// - HealthCheckInterval: Interval between health checks (default: 60 seconds)
/// - CooldownPeriod: Time to wait before retrying a failed proxy (default: 300 seconds)
/// </para>
/// </remarks>
public sealed class ProxyRotationMiddleware : IPipelineMiddleware
{
    private readonly List<ProxyEndpoint> _proxies;
    private readonly string _rotationStrategy;
    private readonly Random _random;
    private int _currentIndex;
    private readonly object _lock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ProxyRotationMiddleware"/> class.
    /// </summary>
    /// <param name="configuration">The middleware configuration dictionary.</param>
    /// <exception cref="ArgumentNullException">Thrown when configuration is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no proxies are configured.</exception>
    public ProxyRotationMiddleware(Dictionary<string, object> configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (!configuration.TryGetValue("ProxyList", out var proxyListObj) ||
            proxyListObj is not List<string> proxyList ||
            proxyList.Count == 0)
        {
            throw new InvalidOperationException("ProxyRotationMiddleware requires a non-empty ProxyList in configuration.");
        }

        _proxies = proxyList.Select(url => new ProxyEndpoint(url)).ToList();
        _rotationStrategy = configuration.TryGetValue("RotationStrategy", out var strategy)
            ? strategy.ToString() ?? "RoundRobin"
            : "RoundRobin";
        _random = new Random();
        _currentIndex = 0;
    }

    /// <summary>
    /// Invokes the middleware to assign a proxy to the request.
    /// </summary>
    /// <param name="context">The pipeline context containing the request.</param>
    /// <param name="next">The delegate to invoke the next middleware in the pipeline.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no healthy proxies are available.</exception>
    public async Task InvokeAsync(PipelineContext context, PipelineDelegate next)
    {
        var request = context.GetRequestAs<Request>();
        if (request == null)
        {
            await next(context);
            return;
        }

        var proxy = SelectProxy();
        if (proxy == null)
        {
            throw new InvalidOperationException("No healthy proxies available for request.");
        }

        // Store the selected proxy in the request metadata
        request.Metadata["Proxy"] = proxy.Url;
        request.Metadata["ProxyEndpoint"] = proxy;

        try
        {
            await next(context);

            // Mark proxy as successful
            proxy.RecordSuccess();
        }
        catch (Exception)
        {
            // Mark proxy as failed
            proxy.RecordFailure();
            throw;
        }
    }

    /// <summary>
    /// Selects the next proxy based on the configured rotation strategy.
    /// </summary>
    /// <returns>A healthy proxy endpoint, or null if none are available.</returns>
    private ProxyEndpoint? SelectProxy()
    {
        lock (_lock)
        {
            var healthyProxies = _proxies.Where(p => p.IsHealthy).ToList();
            if (healthyProxies.Count == 0)
            {
                // Try to recover failed proxies that have cooled down
                foreach (var proxy in _proxies)
                {
                    proxy.TryRecover();
                }

                healthyProxies = _proxies.Where(p => p.IsHealthy).ToList();
                if (healthyProxies.Count == 0)
                {
                    return null;
                }
            }

            ProxyEndpoint selected;
            if (_rotationStrategy.Equals("Random", StringComparison.OrdinalIgnoreCase))
            {
                selected = healthyProxies[_random.Next(healthyProxies.Count)];
            }
            else // RoundRobin
            {
                _currentIndex = _currentIndex % healthyProxies.Count;
                selected = healthyProxies[_currentIndex];
                _currentIndex++;
            }

            return selected;
        }
    }

    /// <summary>
    /// Represents a proxy endpoint with health tracking.
    /// </summary>
    private sealed class ProxyEndpoint
    {
        private int _failureCount;
        private DateTime _lastFailure;
        private readonly TimeSpan _cooldownPeriod = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Initializes a new instance of the <see cref="ProxyEndpoint"/> class.
        /// </summary>
        /// <param name="url">The proxy URL.</param>
        public ProxyEndpoint(string url)
        {
            Url = url;
            _lastFailure = DateTime.MinValue;
        }

        /// <summary>
        /// Gets the proxy URL.
        /// </summary>
        public string Url { get; }

        /// <summary>
        /// Gets a value indicating whether the proxy is healthy.
        /// </summary>
        public bool IsHealthy => _failureCount < 3 || DateTime.UtcNow - _lastFailure > _cooldownPeriod;

        /// <summary>
        /// Records a successful request through this proxy.
        /// </summary>
        public void RecordSuccess()
        {
            Interlocked.Exchange(ref _failureCount, 0);
        }

        /// <summary>
        /// Records a failed request through this proxy.
        /// </summary>
        public void RecordFailure()
        {
            Interlocked.Increment(ref _failureCount);
            _lastFailure = DateTime.UtcNow;
        }

        /// <summary>
        /// Attempts to recover the proxy if the cooldown period has elapsed.
        /// </summary>
        public void TryRecover()
        {
            if (DateTime.UtcNow - _lastFailure > _cooldownPeriod)
            {
                Interlocked.Exchange(ref _failureCount, 0);
            }
        }
    }
}
