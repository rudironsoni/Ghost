using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Ghost.Abstractions;
using Ghost.Core;
using Microsoft.Extensions.Options;

namespace Ghost.Services;

public class StaticProxySource : IProxySource
{
        private readonly IOptions<ProxyOptions> _options;

        public StaticProxySource(IOptions<ProxyOptions> options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public Task<IEnumerable<ProxyInfo>> FetchProxiesAsync(CancellationToken ct)
        {
            var cfg = _options.Value.Static;
            if (cfg == null || !cfg.Enabled || cfg.Items == null || cfg.Items.Count == 0)
                return Task.FromResult(Enumerable.Empty<ProxyInfo>());

            var list = new List<ProxyInfo>();
            foreach (var item in cfg.Items)
            {
                if (string.IsNullOrWhiteSpace(item))
                    continue;

                var parsed = ParseProxyString(item.Trim());
                if (parsed is not null)
                    list.Add(parsed);
            }

            return Task.FromResult<IEnumerable<ProxyInfo>>(list);
        }

        // Accepts forms:
        // scheme://user:pass@host:port
        // scheme://host:port
        // host:port
    private static ProxyInfo? ParseProxyString(string input)
    {
            // Try full URI parse first
            if (Uri.TryCreate(input, UriKind.Absolute, out var uri))
            {
                var userInfo = uri.UserInfo; // "user:pass" or empty
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

            // Fallback: host:port or host
            var m = Regex.Match(input, "^(?:([^:@]+):([^@]+)@)?([^:]+):(\\d+)$");
            if (m.Success)
            {
                var user = string.IsNullOrEmpty(m.Groups[1].Value) ? null : m.Groups[1].Value;
                var pass = string.IsNullOrEmpty(m.Groups[2].Value) ? null : m.Groups[2].Value;
                var host = m.Groups[3].Value;
                var port = m.Groups[4].Value;
                return new ProxyInfo($"{host}:{port}", user, pass);
            }

            // As last resort, if it looks like host:port
            var simple = input.Split(':');
            if (simple.Length == 2 && int.TryParse(simple[1], out _))
                return new ProxyInfo(input, null, null);

            return null;
    }
}
