using System.Net.Http;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ghost.Http;

/// <summary>
/// Security options for configuring HttpClientHandler with explicit opt-in for dangerous settings.
/// </summary>
public sealed class HttpClientSecurityOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to accept any server certificate without validation.
    /// This is a DANGEROUS setting that should ONLY be used in testing or development environments.
    /// Requires explicit opt-in via configuration.
    /// </summary>
    public bool DangerousAcceptAnyServerCertificate { get; set; }

    /// <summary>
    /// Gets or sets the reason for allowing dangerous certificate acceptance.
    /// This is logged for audit purposes.
    /// </summary>
    public string? DangerousAcceptAnyServerCertificateReason { get; set; }
}

/// <summary>
/// Structured log events for HttpClient security operations.
/// </summary>
public static partial class HttpClientSecurityEvents
{
    /// <summary>
    /// Logs when certificate validation is bypassed (security audit event).
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="reason">The reason provided for bypassing validation.</param>
    /// <param name="requestUri">The URI being requested.</param>
    [LoggerMessage(
        EventId = 9001,
        Level = LogLevel.Warning,
        Message = "SECURITY AUDIT: Server certificate validation bypassed. Reason={Reason} Uri={RequestUri}")]
    public static partial void LogCertificateValidationBypassed(
        this ILogger logger,
        string reason,
        string requestUri);

    /// <summary>
    /// Logs when certificate validation fails.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="sslPolicyErrors">The SSL policy errors that occurred.</param>
    /// <param name="requestUri">The URI being requested.</param>
    [LoggerMessage(
        EventId = 9002,
        Level = LogLevel.Warning,
        Message = "Certificate validation failed. Errors={SslPolicyErrors} Uri={RequestUri}")]
    public static partial void LogCertificateValidationFailed(
        this ILogger logger,
        string sslPolicyErrors,
        string requestUri);

    /// <summary>
    /// Logs when dangerous security options are configured.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="reason">The reason provided for the dangerous configuration.</param>
    [LoggerMessage(
        EventId = 9003,
        Level = LogLevel.Warning,
        Message = "SECURITY AUDIT: Dangerous security options configured. Reason={Reason}")]
    public static partial void LogDangerousSecurityOptionsConfigured(
        this ILogger logger,
        string reason);
}

public static class HttpClientSecurityExtensions
{
    /// <summary>
    /// Creates a certificate validation callback that logs validation failures.
    /// </summary>
    /// <param name="logger">Optional logger for audit logging.</param>
    /// <returns>A RemoteCertificateValidationCallback that validates certificates.</returns>
    public static RemoteCertificateValidationCallback CreateCertificateValidationCallback(ILogger? logger = null)
    {
        return (sender, certificate, chain, sslPolicyErrors) =>
        {
            if (sslPolicyErrors == SslPolicyErrors.None)
            {
                return true;
            }

            string requestUri = (sender as HttpRequestMessage)?.RequestUri?.ToString() ?? "unknown";
            logger?.LogCertificateValidationFailed(sslPolicyErrors.ToString(), requestUri);

            return false;
        };
    }

    /// <summary>
    /// Creates a certificate validation callback for HttpClientHandler that logs validation failures.
    /// </summary>
    /// <param name="logger">Optional logger for audit logging.</param>
    /// <returns>A Func that validates certificates for HttpClientHandler.</returns>
    public static Func<HttpRequestMessage, X509Certificate2?, X509Chain?, SslPolicyErrors, bool> CreateHttpClientCertificateValidationCallback(ILogger? logger = null)
    {
        return (requestMessage, certificate, chain, sslPolicyErrors) =>
        {
            if (sslPolicyErrors == SslPolicyErrors.None)
            {
                return true;
            }

            string requestUri = requestMessage?.RequestUri?.ToString() ?? "unknown";
            logger?.LogCertificateValidationFailed(sslPolicyErrors.ToString(), requestUri);

            return false;
        };
    }

    /// <summary>
    /// Creates a certificate validation callback that bypasses validation for testing purposes.
    /// This should ONLY be used in controlled testing environments.
    /// All bypass events are logged for audit purposes.
    /// </summary>
    /// <param name="logger">Logger for audit logging of bypass events.</param>
    /// <param name="reason">The reason for bypassing validation (required for audit).</param>
    /// <returns>A Func that logs but bypasses validation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when logger is null.</exception>
    /// <exception cref="ArgumentException">Thrown when reason is null or empty.</exception>
    public static Func<HttpRequestMessage, X509Certificate2?, X509Chain?, SslPolicyErrors, bool> CreateDangerousBypassCallback(ILogger logger, string reason)
    {
        ArgumentNullException.ThrowIfNull(logger);
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A reason must be provided for certificate validation bypass for audit purposes.", nameof(reason));
        }

        logger.LogDangerousSecurityOptionsConfigured(reason);

        return (requestMessage, certificate, chain, sslPolicyErrors) =>
        {
            string requestUri = requestMessage?.RequestUri?.ToString() ?? "unknown";
            logger.LogCertificateValidationBypassed(reason, requestUri);
            return true;
        };
    }

    /// <summary>
    /// Configures an HttpClientHandler with secure defaults.
    /// </summary>
    /// <param name="handler">The handler to configure.</param>
    /// <returns>The configured handler.</returns>
    public static HttpClientHandler ConfigureSecureHttpClientHandler(HttpClientHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        handler.SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;
        handler.AutomaticDecompression = System.Net.DecompressionMethods.All;
        handler.MaxAutomaticRedirections = 10;
        handler.MaxConnectionsPerServer = 100;

        return handler;
    }

    /// <summary>
    /// Configures an HttpClientHandler with secure defaults and optional security settings.
    /// </summary>
    /// <param name="handler">The handler to configure.</param>
    /// <param name="options">Security options. If null, defaults to secure configuration.</param>
    /// <param name="logger">Optional logger for audit logging of security events.</param>
    /// <returns>The configured handler.</returns>
    public static HttpClientHandler ConfigureSecureHttpClientHandler(
        HttpClientHandler handler,
        HttpClientSecurityOptions? options,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(handler);

        ConfigureSecureHttpClientHandler(handler);

        if (options?.DangerousAcceptAnyServerCertificate == true)
        {
            string reason = options.DangerousAcceptAnyServerCertificateReason
                ?? "No reason provided";

            ILogger effectiveLogger = logger ?? NullLogger.Instance;
            handler.ServerCertificateCustomValidationCallback = CreateDangerousBypassCallback(effectiveLogger, reason);
        }

        return handler;
    }

    /// <summary>
    /// Configures an HttpClientHandler with secure defaults and client certificate.
    /// </summary>
    /// <param name="handler">The handler to configure.</param>
    /// <param name="clientCertificate">Optional client certificate for mutual TLS.</param>
    /// <returns>The configured handler.</returns>
    public static HttpClientHandler ConfigureSecureHttpClientHandler(
        HttpClientHandler handler,
        X509Certificate2? clientCertificate)
    {
        ArgumentNullException.ThrowIfNull(handler);

        ConfigureSecureHttpClientHandler(handler);

        if (clientCertificate != null)
        {
            handler.ClientCertificateOptions = ClientCertificateOption.Manual;
            handler.ClientCertificates.Add(clientCertificate);
        }

        return handler;
    }

    /// <summary>
    /// Configures an HttpClientHandler with secure defaults and explicit opt-in for dangerous certificate bypass.
    /// </summary>
    /// <param name="handler">The handler to configure.</param>
    /// <param name="dangerousAcceptAnyServerCertificate">If true, requires explicit confirmation via security options.</param>
    /// <param name="logger">Optional logger for audit logging.</param>
    /// <returns>The configured handler.</returns>
    /// <exception cref="InvalidOperationException">Thrown when dangerous bypass is requested without proper configuration.</exception>
    public static HttpClientHandler ConfigureSecureHttpClientHandler(
        HttpClientHandler handler,
        bool dangerousAcceptAnyServerCertificate,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(handler);

        if (dangerousAcceptAnyServerCertificate)
        {
            throw new InvalidOperationException(
                "Dangerous certificate bypass requires using the HttpClientSecurityOptions overload with explicit reason. " +
                "This ensures proper audit logging. Use ConfigureSecureHttpClientHandler(handler, options, logger) instead.");
        }

        return ConfigureSecureHttpClientHandler(handler);
    }
}
