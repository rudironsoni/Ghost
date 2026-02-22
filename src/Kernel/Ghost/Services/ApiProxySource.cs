using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Ghost.Kernel;
using Microsoft.Extensions.Logging;

namespace Ghost.Services;

public class ApiProxySource : IProxySource
{
    private static readonly char[] s_lineSeparators = new[] { '\r', '\n' };

    private static readonly Action<ILogger, string, Exception?> s_logFetching =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(1, nameof(ApiProxySource)), "Fetching proxies from {Url}");

    private static readonly Action<ILogger, int, Exception?> s_logFetchedCount =
        LoggerMessage.Define<int>(LogLevel.Information, new EventId(2, nameof(ApiProxySource)), "Fetched {Count} proxies from API");

    private static readonly Action<ILogger, int, Exception?> s_logFetchFailedStatus =
        LoggerMessage.Define<int>(LogLevel.Error, new EventId(3, nameof(ApiProxySource)), "Failed to fetch proxies from API, status code: {StatusCode}");

    private static readonly Action<ILogger, Exception?> s_logFetchFailed =
        LoggerMessage.Define(LogLevel.Error, new EventId(4, nameof(ApiProxySource)), "Failed to fetch proxies from API");

    private readonly HttpClient _http;
    private readonly ProxySourceConfig _config;
    private readonly ILogger<ApiProxySource> _logger;

    public ApiProxySource(HttpClient http, ProxySourceConfig config, ILogger<ApiProxySource> logger)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(logger);
        _http = http;
        _config = config;
        _logger = logger;
    }

    public async Task<IEnumerable<ProxyInfo>> FetchProxiesAsync(CancellationToken ct)
    {
        ProxySourceConfig cfg = _config;
        if (cfg == null || !cfg.Enabled || string.IsNullOrWhiteSpace(cfg.Url))
            return Enumerable.Empty<ProxyInfo>();

        try
        {
            s_logFetching(_logger, cfg.Url!, null);

            using HttpResponseMessage resp = await _http.GetAsync(cfg.Url!, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                s_logFetchFailedStatus(_logger, (int)resp.StatusCode, null);
                return Enumerable.Empty<ProxyInfo>();
            }

            string body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(body))
                return Enumerable.Empty<ProxyInfo>();

            IEnumerable<string> lines = body.Split(s_lineSeparators, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrEmpty(l));

            List<ProxyInfo> res = [];
            foreach (string? line in lines)
            {
                ProxyInfo? p = ParseLine(line);
                if (p is not null)
                    res.Add(p);
            }

            s_logFetchedCount(_logger, res.Count, null);
            return res;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // log and swallow, but preserve cancellation behavior above
            s_logFetchFailed(_logger, ex);
            return Enumerable.Empty<ProxyInfo>();
        }
    }

    private static ProxyInfo? ParseLine(string line)
    {
        // reuse logic similar to StaticProxySource
        if (Uri.TryCreate(line, UriKind.Absolute, out Uri? uri))
        {
            string userInfo = uri.UserInfo;
            string? user = null;
            string? pass = null;
            if (!string.IsNullOrEmpty(userInfo))
            {
                string[] parts = userInfo.Split(':', 2);
                user = parts.Length > 0 ? parts[0] : null;
                pass = parts.Length > 1 ? parts[1] : null;
            }

            // Preserve scheme when present. Use HostAndPort to keep IPv6 bracket notation when needed.
            string hostAndPort = uri.GetComponents(UriComponents.HostAndPort, UriFormat.Unescaped);
            string server = uri.IsAbsoluteUri ? $"{uri.Scheme}://{hostAndPort}" : hostAndPort;
            return new ProxyInfo(server, user, pass);
        }

        Match m = Regex.Match(line, "^(?:([^:@]+):([^@]+)@)?([^:]+):(\\d+)$");
        if (m.Success)
        {
            string? user = string.IsNullOrEmpty(m.Groups[1].Value) ? null : m.Groups[1].Value;
            string? pass = string.IsNullOrEmpty(m.Groups[2].Value) ? null : m.Groups[2].Value;
            string host = m.Groups[3].Value;
            string port = m.Groups[4].Value;
            // No scheme present in this branch; default to http://
            return new ProxyInfo($"http://{host}:{port}", user, pass);
        }

        string[] simple = line.Split(':');
        if (simple.Length == 2 && int.TryParse(simple[1], out _))
            // No scheme provided -> default to http://
            return new ProxyInfo($"http://{line}", null, null);

        return null;
    }
}
