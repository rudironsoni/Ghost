using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Ghost.Abstractions;
using Ghost.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghost.Services;

    public class StaticProxySource : IProxySource
    {
        private readonly ProxySourceConfig _config;
        private readonly ILogger<StaticProxySource> _logger;

        private static readonly Action<ILogger, int, Exception?> s_logLoaded =
            LoggerMessage.Define<int>(LogLevel.Information, new EventId(1, nameof(StaticProxySource)), "Loaded {Count} static proxies from configuration.");

        private static readonly Action<ILogger, string, Exception?> s_logIgnored =
            LoggerMessage.Define<string>(LogLevel.Warning, new EventId(2, nameof(StaticProxySource)), "Ignoring static proxy entry: {Entry}");

        private static readonly Action<ILogger, string, Exception?> s_logParsing =
            LoggerMessage.Define<string>(LogLevel.Debug, new EventId(3, nameof(StaticProxySource)), "[DEBUG] Parsing: '{Input}'");

        private static readonly Action<ILogger, string, string, Exception?> s_logRegexMatch =
            LoggerMessage.Define<string, string>(LogLevel.Debug, new EventId(4, nameof(StaticProxySource)), "[DEBUG] Regex Match: Host='{Host}', Port='{Port}'");

        private static readonly Action<ILogger, string, Exception?> s_logSimpleMatch =
            LoggerMessage.Define<string>(LogLevel.Debug, new EventId(5, nameof(StaticProxySource)), "[DEBUG] Simple Match: '{Input}'");

        private static readonly Action<ILogger, string, Exception?> s_logParsed =
            LoggerMessage.Define<string>(LogLevel.Debug, new EventId(6, nameof(StaticProxySource)), "[DEBUG] Parsed: Server='{Server}'");

        public StaticProxySource(ProxySourceConfig config, ILogger<StaticProxySource> logger)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<IEnumerable<ProxyInfo>> FetchProxiesAsync(CancellationToken ct)
        {
            var cfg = _config;
            if (cfg == null || !cfg.Enabled || cfg.Hosts == null || cfg.Hosts.Count == 0)
                return Task.FromResult(Enumerable.Empty<ProxyInfo>());

            var list = new List<ProxyInfo>();
            foreach (var item in cfg.Hosts)
            {
                if (string.IsNullOrWhiteSpace(item))
                    continue;

                var trimmed = item.Trim();
                var parsed = ParseProxyString(trimmed);
                if (parsed is null)
                {
                    // If the item is a bare host (no scheme and no port), parsing will return null and we'll ignore
                }

                if (parsed is null)
                {
                    s_logIgnored(_logger, item, null);
                    continue;
                }

                // If original item was a bare host (no scheme and no port) but we applied a global port,
                // ensure the resulting server includes the scheme so callers can see it (e.g. http://host:port).
                if (!trimmed.Contains("://") && !trimmed.Contains(':') && !parsed.Server.Contains("://"))
                {
                    parsed = new ProxyInfo($"http://{parsed.Server}", parsed.Username, parsed.Password);
                }

                s_logParsed(_logger, parsed.Server, null);

                ProxyInfo toAdd;
                if (string.IsNullOrEmpty(parsed.Username) && !string.IsNullOrEmpty(cfg.Username))
                {
                    toAdd = new ProxyInfo(parsed.Server, cfg.Username, cfg.Password);
                }
                else
                {
                    toAdd = parsed;
                }

                list.Add(toAdd);
            }

            s_logLoaded(_logger, list.Count, null);
            return Task.FromResult<IEnumerable<ProxyInfo>>(list);
        }

        // Accepts forms:
        // scheme://user:pass@host:port
        // scheme://host:port
        // host:port
        private ProxyInfo? ParseProxyString(string input)
        {
            s_logParsing(_logger, input, null);

            // If no scheme and no port (no colon), return null to allow fallback to global port
            if (!input.Contains("://") && !input.Contains(':'))
                return null;

            // 1. Ensure scheme for parsing
            var hadScheme = input.Contains("://");
            var parsingInput = hadScheme ? input : $"http://{input}";

            if (!Uri.TryCreate(parsingInput, UriKind.Absolute, out var uri))
                return null;

            // 2. Extract User/Pass
            string? user = null;
            string? pass = null;
            if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                var parts = uri.UserInfo.Split(':', 2);
                user = parts.Length > 0 ? parts[0] : null;
                if (parts.Length > 1) pass = parts[1];
            }

            // 3. Construct server string
            // Use HostAndPort to preserve IPv6 bracket notation when needed and only include port if present/explicit
            var hostAndPort = uri.GetComponents(UriComponents.HostAndPort, UriFormat.Unescaped);

            string serverUrl;
            if (hadScheme)
                serverUrl = $"{uri.Scheme}://{hostAndPort}";
            else if (input.Contains(':'))
                // If the input included an explicit port but no scheme, preserve a default scheme
                serverUrl = $"http://{hostAndPort}";
            else
                serverUrl = hostAndPort;

            s_logRegexMatch(_logger, uri.Host, uri.Port.ToString(System.Globalization.CultureInfo.InvariantCulture), null);

            return new ProxyInfo(serverUrl, user, pass);
        }
}
