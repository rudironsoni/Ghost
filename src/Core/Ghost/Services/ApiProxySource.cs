using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Ghost.Abstractions;
using Ghost.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
        private readonly IOptions<ProxyOptions> _options;
        private readonly ILogger<ApiProxySource> _logger;

        public ApiProxySource(HttpClient http, IOptions<ProxyOptions> options, ILogger<ApiProxySource> logger)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IEnumerable<ProxyInfo>> FetchProxiesAsync(CancellationToken ct)
        {
            var cfg = _options.Value.Api;
            if (cfg == null || !cfg.Enabled || string.IsNullOrWhiteSpace(cfg.Url))
                return Enumerable.Empty<ProxyInfo>();

            try
            {
                s_logFetching(_logger, cfg.Url!, null);

                using var resp = await _http.GetAsync(cfg.Url!, ct).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                {
                    s_logFetchFailedStatus(_logger, (int)resp.StatusCode, null);
                    return Enumerable.Empty<ProxyInfo>();
                }

                var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(body))
                    return Enumerable.Empty<ProxyInfo>();

                var lines = body.Split(s_lineSeparators, StringSplitOptions.RemoveEmptyEntries)
                    .Select(l => l.Trim())
                    .Where(l => !string.IsNullOrEmpty(l));

                var res = new List<ProxyInfo>();
                foreach (var line in lines)
                {
                    var p = ParseLine(line);
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
            if (Uri.TryCreate(line, UriKind.Absolute, out var uri))
            {
                var userInfo = uri.UserInfo;
                string? user = null;
                string? pass = null;
                if (!string.IsNullOrEmpty(userInfo))
                {
                    var parts = userInfo.Split(':', 2);
                    user = parts.Length > 0 ? parts[0] : null;
                    pass = parts.Length > 1 ? parts[1] : null;
                }

                var hostPort = uri.IsDefaultPort ? uri.Host : $"{uri.Host}:{uri.Port}";
                return new ProxyInfo(hostPort, user, pass);
            }

            var m = Regex.Match(line, "^(?:([^:@]+):([^@]+)@)?([^:]+):(\\d+)$");
            if (m.Success)
            {
                var user = string.IsNullOrEmpty(m.Groups[1].Value) ? null : m.Groups[1].Value;
                var pass = string.IsNullOrEmpty(m.Groups[2].Value) ? null : m.Groups[2].Value;
                var host = m.Groups[3].Value;
                var port = m.Groups[4].Value;
                return new ProxyInfo($"{host}:{port}", user, pass);
            }

            var simple = line.Split(':');
            if (simple.Length == 2 && int.TryParse(simple[1], out _))
                return new ProxyInfo(line, null, null);

            return null;
    }
}
