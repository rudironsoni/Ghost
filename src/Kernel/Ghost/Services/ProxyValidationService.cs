using System.Net;
using Microsoft.Extensions.Logging;

namespace Ghost.Services;

public class ProxyValidationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ProxyValidationService> _logger;

    private static readonly Action<ILogger, string, int, Exception?> s_logValidationSucceeded =
        LoggerMessage.Define<string, int>(LogLevel.Information, new EventId(1, nameof(ProxyValidationService)),
            "Proxy validation successful: {Host}:{Port}");

    private static readonly Action<ILogger, string, int, Exception?> s_logProxyTimeout =
        LoggerMessage.Define<string, int>(LogLevel.Warning, new EventId(2, nameof(ProxyValidationService)),
            "Proxy timeout: {Host}:{Port}");

    private static readonly Action<ILogger, string, int, string, Exception?> s_logProxyConnectionFailed =
        LoggerMessage.Define<string, int, string>(LogLevel.Warning, new EventId(3, nameof(ProxyValidationService)),
            "Proxy connection failed: {Host}:{Port} - {Message}");

    private static readonly Action<ILogger, string, int, Exception?> s_logProxyUnexpectedError =
        LoggerMessage.Define<string, int>(LogLevel.Error, new EventId(4, nameof(ProxyValidationService)),
            "Unexpected error validating proxy: {Host}:{Port}");

    public ProxyValidationService(IHttpClientFactory httpClientFactory, ILogger<ProxyValidationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<ProxyValidationResult> ValidateProxyAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        var proxy = new WebProxy($"http://{host}:{port}")
        {
            UseDefaultCredentials = true
        };

        var handler = new SocketsHttpHandler
        {
            Proxy = proxy,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            ConnectTimeout = TimeSpan.FromSeconds(30)
        };

        using var httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        try
        {
            using HttpResponseMessage response = await httpClient.GetAsync("https://httpbin.org/ip", cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return ProxyValidationResult.Failure($"HTTP {response.StatusCode}: {response.ReasonPhrase}");
            }

            s_logValidationSucceeded(_logger, host, port, null);
            return ProxyValidationResult.Success(host, port);
        }
        catch (HttpRequestException ex) when (ex.InnerException is TimeoutException)
        {
            s_logProxyTimeout(_logger, host, port, ex);
            return ProxyValidationResult.Failure("Connection timeout");
        }
        catch (HttpRequestException ex)
        {
            s_logProxyConnectionFailed(_logger, host, port, ex.Message, ex);
            return ProxyValidationResult.Failure($"Connection failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            s_logProxyUnexpectedError(_logger, host, port, ex);
            return ProxyValidationResult.Failure($"Unexpected error: {ex.Message}");
        }
    }
}

public record ProxyValidationResult(
    string Host,
    int Port,
    bool IsValid,
    string? ErrorMessage = null
)
{
    public static ProxyValidationResult Success(string host, int port) => new(host, port, true);
    public static ProxyValidationResult Failure(string error) => new(string.Empty, 0, false, error);
}
