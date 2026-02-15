using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Ghost.Http;
using Polly;
using Polly.Retry;

namespace Ghost.Infrastructure.Session;

/// <summary>
/// Implements JobSpy-inspired session management with proxy rotation and TLS fingerprinting
/// </summary>
public class RotatingProxySession : IDisposable
{
    private readonly IProxyProvider _proxyProvider;
    private readonly RotatingProxySessionOptions _options;
    private readonly HttpClient _httpClient;
    private readonly List<ProxyInfo> _proxyPool;
    private int _currentProxyIndex;
    private bool _disposed;

    public RotatingProxySession(IProxyProvider proxyProvider, RotatingProxySessionOptions? options = null)
        : this(proxyProvider, CreateDefaultHttpClient(options ?? new RotatingProxySessionOptions()), options)
    {
    }

    public RotatingProxySession(IProxyProvider proxyProvider, HttpClient httpClient, RotatingProxySessionOptions? options = null)
    {
        _proxyProvider = proxyProvider ?? throw new ArgumentNullException(nameof(proxyProvider));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? new RotatingProxySessionOptions();

        // Initialize proxy pool
        _proxyPool = new List<ProxyInfo>();
        _currentProxyIndex = 0;

        // Set default headers
        SetDefaultHeaders();
    }

    private static HttpClient CreateDefaultHttpClient(RotatingProxySessionOptions options)
    {
        SocketsHttpHandler handler = CreateHttpMessageHandler(options);
        return new HttpClient(handler)
        {
            Timeout = options.Timeout
        };
    }

    private static SocketsHttpHandler CreateHttpMessageHandler(RotatingProxySessionOptions options)
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = options.PooledConnectionLifetime,
            PooledConnectionIdleTimeout = options.PooledConnectionIdleTimeout,
            MaxConnectionsPerServer = options.MaxConnectionsPerServer,
            UseCookies = options.UseCookies,
            UseProxy = options.EnableProxyRotation
        };

        // Configure TLS settings if enabled
        if (options.EnableTlsFingerprinting && options.TlsCipherSuitesPolicy != null)
        {
            handler.SslOptions.CipherSuitesPolicy = options.TlsCipherSuitesPolicy;
        }

        return handler;
    }

    /// <summary>
    /// Set default headers to mimic real browsers
    /// </summary>
    private void SetDefaultHeaders()
    {
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(_options.DefaultUserAgent);
        _httpClient.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        _httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
    }

    /// <summary>
    /// Execute HTTP request with retry and proxy rotation
    /// </summary>
    public async Task<HttpResponseMessage> ExecuteAsync(Func<HttpRequestMessage> requestFactory, CancellationToken cancellationToken = default)
    {
        AsyncRetryPolicy<HttpResponseMessage> policy = CreateRetryPolicy();

        return await policy.ExecuteAsync(async () =>
        {
            HttpRequestMessage request = requestFactory();

            // Rotate proxy if enabled
            if (_options.EnableProxyRotation)
            {
                await RotateProxyAsync().ConfigureAwait(false);
            }

            // Apply jitter delay
            await ApplyJitterDelayAsync(cancellationToken).ConfigureAwait(false);

            return await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Create Polly retry policy with exponential backoff
    /// </summary>
    private AsyncRetryPolicy<HttpResponseMessage> CreateRetryPolicy()
    {
        return Policy<HttpResponseMessage>
            .HandleResult(r => (int)r.StatusCode == 429 ||
                             r.StatusCode == HttpStatusCode.InternalServerError ||
                             r.StatusCode == HttpStatusCode.BadGateway ||
                             r.StatusCode == HttpStatusCode.ServiceUnavailable ||
                             r.StatusCode == HttpStatusCode.GatewayTimeout)
            .WaitAndRetryAsync(
                _options.MaxRetries,
                attempt => TimeSpan.FromSeconds(Math.Pow(_options.BackoffFactor, attempt)),
                onRetry: (result, timeSpan, retryCount, context) =>
                {
                    // Log retry information
                    _options.OnRetry?.Invoke(result.Result, timeSpan, retryCount);
                });
    }

    /// <summary>
    /// Rotate to next proxy in the pool
    /// </summary>
    private async Task RotateProxyAsync()
    {
        if (!_options.EnableProxyRotation || _proxyPool.Count == 0)
            return;

        _currentProxyIndex = (_currentProxyIndex + 1) % _proxyPool.Count;

        // Refresh proxy pool if needed
        if (_currentProxyIndex == 0 && _options.RefreshProxyPoolOnCycle)
        {
            await RefreshProxyPoolAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Refresh the proxy pool from the provider
    /// </summary>
    private async Task RefreshProxyPoolAsync()
    {
        try
        {
            ProxyInfo? proxy = await _proxyProvider.GetProxyAsync(_options.DefaultCountryCode).ConfigureAwait(false);
            _proxyPool.Clear();
            if (proxy != null)
            {
                _proxyPool.Add(proxy);
            }
        }
        catch (Exception ex)
        {
            _options.OnProxyRefreshError?.Invoke(ex);
        }
    }

    /// <summary>
    /// Apply jitter delay between requests
    /// </summary>
    private async Task ApplyJitterDelayAsync(CancellationToken cancellationToken)
    {
        if (_options.JitterMinMs > 0 && _options.JitterMaxMs > 0)
        {
            var random = new Random();
            int delay = random.Next(_options.JitterMinMs, _options.JitterMaxMs + 1);
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Dispose resources
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _httpClient?.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
