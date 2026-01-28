using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Ghost.Abstractions;
using Microsoft.Extensions.Logging;

namespace Ghost.Services;

public class FreeProxyProvider : IProxyProvider
{
    private readonly HttpClient _http;
    private readonly ILogger<FreeProxyProvider> _logger;

    // Static helpers to satisfy CA1861/CA1848 analyzers
    private static readonly char[] s_lineSeparators = new[] { '\r', '\n' };

    private static readonly Action<ILogger, System.Net.HttpStatusCode, Exception?> s_logNonSuccess =
        LoggerMessage.Define<System.Net.HttpStatusCode>(LogLevel.Warning, new EventId(1, nameof(FreeProxyProvider)), "Proxy provider returned non-success status {StatusCode}");

    private static readonly Action<ILogger, string, Exception?> s_logCancelled =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(2, nameof(FreeProxyProvider)), "Proxy fetch cancelled for country {Country}");

    private static readonly Action<ILogger, string, Exception?> s_logFailed =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(3, nameof(FreeProxyProvider)), "Failed to fetch proxy for country {Country}");

    private static readonly Action<ILogger, string, Exception?> s_logFetched =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(4, nameof(FreeProxyProvider)), "Fetched proxy: {Proxy}");

    public FreeProxyProvider(HttpClient http, ILogger<FreeProxyProvider> logger)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

        public async Task<ProxyInfo?> GetProxyAsync(string countryCode, CancellationToken token = default)
        {
            // proxyscrape API returns ip:port per-line
            // example: https://api.proxyscrape.com/v2/?request=getproxies&protocol=http&timeout=1000&country=US&ssl=1&anonymity=elite
            var url = $"https://api.proxyscrape.com/v2/?request=getproxies&protocol=http&timeout=1000&country={Uri.EscapeDataString(countryCode)}&ssl=1&anonymity=elite";

            try
            {
                using var resp = await _http.GetAsync(url, token).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                {
                    s_logNonSuccess(_logger, resp.StatusCode, null);
                    return null;
                }

                var body = await resp.Content.ReadAsStringAsync(token).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(body))
                    return null;

                // take first non-empty line
                var lines = body.Split(s_lineSeparators, StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length == 0)
                    return null;

                var first = lines[0].Trim();
                // log when we fetched a proxy
                s_logFetched(_logger, first, null);
                // proxyscrape returns host:port; no auth info
                return new ProxyInfo(first, null, null);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                s_logCancelled(_logger, countryCode, null);
                throw;
            }
            catch (Exception ex)
            {
                s_logFailed(_logger, countryCode, ex);
                return null;
            }
        }
    }
