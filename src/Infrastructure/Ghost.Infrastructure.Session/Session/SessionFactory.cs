using System;
using Ghost.Abstractions;

namespace Ghost.Infrastructure.Session;

/// <summary>
/// Factory for creating consistent RotatingProxySession instances
/// </summary>
public class SessionFactory
{
    private readonly IProxyProvider _proxyProvider;
    private readonly RotatingProxySessionOptions _defaultOptions;

    public SessionFactory(IProxyProvider proxyProvider, RotatingProxySessionOptions? defaultOptions = null)
    {
        _proxyProvider = proxyProvider ?? throw new ArgumentNullException(nameof(proxyProvider));
        _defaultOptions = defaultOptions ?? new RotatingProxySessionOptions();
    }

    /// <summary>
    /// Create a session with default options
    /// </summary>
    public RotatingProxySession CreateSession()
    {
        return new RotatingProxySession(_proxyProvider, _defaultOptions);
    }

    /// <summary>
    /// Create a session with custom options
    /// </summary>
    public RotatingProxySession CreateSession(RotatingProxySessionOptions options)
    {
        return new RotatingProxySession(_proxyProvider, options);
    }

    /// <summary>
    /// Create a session optimized for a specific platform
    /// </summary>
    public RotatingProxySession CreatePlatformSession(string platformName)
    {
        RotatingProxySessionOptions options = CreatePlatformSpecificOptions(platformName);
        return CreateSession(options);
    }

    /// <summary>
    /// Create platform-specific options
    /// </summary>
    private static RotatingProxySessionOptions CreatePlatformSpecificOptions(string platformName)
    {
        var options = new RotatingProxySessionOptions
        {
            EnableProxyRotation = true,
            EnableTlsFingerprinting = true,
            UseCookies = true
        };

        // Platform-specific optimizations
        switch (platformName.ToLowerInvariant())
        {
            case "glassdoor":
                options.Timeout = TimeSpan.FromSeconds(45);
                options.MaxRetries = 5;
                options.JitterMinMs = 2000;
                options.JitterMaxMs = 8000;
                break;

            case "indeed":
                options.Timeout = TimeSpan.FromSeconds(30);
                options.MaxRetries = 3;
                options.JitterMinMs = 1000;
                options.JitterMaxMs = 4000;
                break;

            case "google":
                options.Timeout = TimeSpan.FromSeconds(60);
                options.MaxRetries = 4;
                options.JitterMinMs = 3000;
                options.JitterMaxMs = 10000;
                break;

            default:
                // Use default options
                break;
        }

        return options;
    }
}
