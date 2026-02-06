using System;
using System.Collections.Generic;
using System.Net.Security;

namespace Ghost.Platform.Common.Session;

/// <summary>
/// Configuration options for RotatingProxySession
/// </summary>
public class RotatingProxySessionOptions
{
    /// <summary>
    /// Enable proxy rotation
    /// </summary>
    public bool EnableProxyRotation { get; set; } = true;

    /// <summary>
    /// Enable TLS fingerprinting
    /// </summary>
    public bool EnableTlsFingerprinting { get; set; } = true;

    /// <summary>
    /// Default country code for proxy selection
    /// </summary>
    public string DefaultCountryCode { get; set; } = "US";

    /// <summary>
    /// Default user agent string
    /// </summary>
    public string DefaultUserAgent { get; set; } = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.0.0 Safari/537.36";

    /// <summary>
    /// HTTP request timeout
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Maximum number of retry attempts
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Exponential backoff factor
    /// </summary>
    public double BackoffFactor { get; set; } = 2.0;

    /// <summary>
    /// Minimum jitter delay in milliseconds
    /// </summary>
    public int JitterMinMs { get; set; } = 1000;

    /// <summary>
    /// Maximum jitter delay in milliseconds
    /// </summary>
    public int JitterMaxMs { get; set; } = 5000;

    /// <summary>
    /// Pooled connection lifetime
    /// </summary>
    public TimeSpan PooledConnectionLifetime { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Pooled connection idle timeout
    /// </summary>
    public TimeSpan PooledConnectionIdleTimeout { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Maximum connections per server
    /// </summary>
    public int MaxConnectionsPerServer { get; set; } = 10;

    /// <summary>
    /// Enable cookie usage
    /// </summary>
    public bool UseCookies { get; set; } = true;

    /// <summary>
    /// Refresh proxy pool on each cycle
    /// </summary>
    public bool RefreshProxyPoolOnCycle { get; set; }

    /// <summary>
    /// TLS cipher suites policy
    /// </summary>
    public CipherSuitesPolicy? TlsCipherSuitesPolicy { get; set; }

    /// <summary>
    /// TLS application protocols
    /// </summary>
    public List<SslApplicationProtocol>? TlsExtensions { get; set; }

    /// <summary>
    /// Callback for retry events
    /// </summary>
    public Action<HttpResponseMessage?, TimeSpan, int>? OnRetry { get; set; }

    /// <summary>
    /// Callback for proxy refresh errors
    /// </summary>
    public Action<Exception>? OnProxyRefreshError { get; set; }
}
