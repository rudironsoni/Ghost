using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Ghost.Abstractions;
using Ghost.Core;
using Microsoft.Extensions.Options;

namespace Ghost.Services;

public class ApiProxySource : IProxySource
{
        private static readonly char[] s_lineSeparators = new[] { '\r', '\n' };

        private readonly HttpClient _http;
        private readonly IOptions<ProxyOptions> _options;

        public ApiProxySource(HttpClient http, IOptions<ProxyOptions> options)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public async Task<IEnumerable<ProxyInfo>> FetchProxiesAsync(CancellationToken ct)
        {
            var cfg = _options.Value.Api;
            if (cfg == null || !cfg.Enabled || string.IsNullOrWhiteSpace(cfg.Url))
                return Enumerable.Empty<ProxyInfo>();

            try
            {
                using var resp = await _http.GetAsync(cfg.Url!, ct).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                    return Enumerable.Empty<ProxyInfo>();

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

                return res;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
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
