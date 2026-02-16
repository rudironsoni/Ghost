using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Ghost.ProxyManagement;

/// <summary>
/// Scrapes free proxies from multiple public sources.
/// Targets 1000+ proxies daily with $0 cost.
/// </summary>
public class FreeProxyScraper : IProxySource
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FreeProxyScraper> _logger;

    private static readonly Action<ILogger, string, int, Exception?> s_logProxiesScraped =
        LoggerMessage.Define<string, int>(LogLevel.Information, new EventId(1, "ProxiesScraped"),
            "Scraped {Count} proxies from {Source}");

    private static readonly Action<ILogger, string, Exception?> s_logScrapeFailed =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(2, "ScrapeFailed"),
            "Failed to scrape proxies from {Source}");

    private static readonly char[] s_lineSeparators = new[] { '\n', '\r' };

    public FreeProxyScraper(ILogger<FreeProxyScraper> logger)
        : this(new HttpClient { Timeout = TimeSpan.FromSeconds(30) }, logger)
    {
    }

    public FreeProxyScraper(HttpClient httpClient, ILogger<FreeProxyScraper> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Fetches proxies from all configured sources.
    /// </summary>
    public virtual async Task<IEnumerable<ProxyInfo>> FetchProxiesAsync(CancellationToken ct)
    {
        List<ProxyInfo> allProxies = [];

        // Scrape from all sources in parallel
        Task<List<ProxyInfo>>[] tasks = new[]
        {
            ScrapeFromFreeProxyListAsync(ct),
            ScrapeFromProxyListDownloadAsync(ct),
            ScrapeFromProxyScrapeAsync(ct),
            ScrapeFromProxyScanAsync(ct)
        };

        List<ProxyInfo>[] results = await Task.WhenAll(tasks).ConfigureAwait(false);

        foreach (List<ProxyInfo>? proxies in results)
        {
            allProxies.AddRange(proxies);
        }

        // Remove duplicates based on Server address
        var uniqueProxies = allProxies
            .GroupBy(p => p.Server)
            .Select(g => g.First())
            .ToList();

        return uniqueProxies;
    }

    /// <summary>
    /// Scrapes proxies from free-proxy-list.net
    /// </summary>
    private async Task<List<ProxyInfo>> ScrapeFromFreeProxyListAsync(CancellationToken ct)
    {
        const string source = "free-proxy-list.net";
        List<ProxyInfo> proxies = [];

        try
        {
            // Note: This is a simplified implementation. In production, you'd need HTML parsing.
            // For now, we'll use the API endpoint if available, or return empty list.
            // The actual implementation would use AngleSharp or HtmlAgilityPack to parse the HTML table.

            HttpResponseMessage response = await _httpClient.GetAsync("https://www.free-proxy-list.net/", ct).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                // Placeholder: In production, parse HTML table and extract proxy data
                // For now, return empty to avoid parsing complexity in initial implementation
                s_logProxiesScraped(_logger, source, 0, null);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            s_logScrapeFailed(_logger, source, ex);
        }

        return proxies;
    }

    /// <summary>
    /// Scrapes proxies from proxy-list.download
    /// </summary>
    private async Task<List<ProxyInfo>> ScrapeFromProxyListDownloadAsync(CancellationToken ct)
    {
        const string source = "proxy-list.download";
        List<ProxyInfo> proxies = [];

        try
        {
            // HTTP proxies
            string httpUrl = "https://www.proxy-list.download/api/v1/get?type=http";
            string httpResponse = await _httpClient.GetStringAsync(httpUrl, ct).ConfigureAwait(false);

            List<ProxyInfo> httpProxies = ParseProxyList(httpResponse);
            proxies.AddRange(httpProxies);

            // HTTPS proxies
            string httpsUrl = "https://www.proxy-list.download/api/v1/get?type=https";
            string httpsResponse = await _httpClient.GetStringAsync(httpsUrl, ct).ConfigureAwait(false);

            List<ProxyInfo> httpsProxies = ParseProxyList(httpsResponse);
            proxies.AddRange(httpsProxies);

            s_logProxiesScraped(_logger, source, proxies.Count, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            s_logScrapeFailed(_logger, source, ex);
        }

        return proxies;
    }

    /// <summary>
    /// Scrapes proxies from api.proxyscrape.com (free tier)
    /// </summary>
    private async Task<List<ProxyInfo>> ScrapeFromProxyScrapeAsync(CancellationToken ct)
    {
        const string source = "api.proxyscrape.com";
        List<ProxyInfo> proxies = [];

        try
        {
            string url = "https://api.proxyscrape.com/v2/?request=get&protocol=http&timeout=5000&country=all&ssl=all&anonymity=all&format=textplain";
            string response = await _httpClient.GetStringAsync(url, ct).ConfigureAwait(false);

            List<ProxyInfo> scrapedProxies = ParseProxyList(response);
            proxies.AddRange(scrapedProxies);

            s_logProxiesScraped(_logger, source, proxies.Count, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            s_logScrapeFailed(_logger, source, ex);
        }

        return proxies;
    }

    /// <summary>
    /// Scrapes proxies from proxyscan.io
    /// </summary>
    private async Task<List<ProxyInfo>> ScrapeFromProxyScanAsync(CancellationToken ct)
    {
        const string source = "proxyscan.io";
        List<ProxyInfo> proxies = [];

        try
        {
            string url = "https://www.proxyscan.io/api/proxy?format=json&limit=100&type=http,https";
            string response = await _httpClient.GetStringAsync(url, ct).ConfigureAwait(false);

            List<ProxyScanProxy>? proxyArray = JsonSerializer.Deserialize<List<ProxyScanProxy>>(response);
            if (proxyArray != null)
            {
                foreach (ProxyScanProxy proxy in proxyArray)
                {
                    string server = $"http://{proxy.Ip}:{proxy.Port}";
                    proxies.Add(new ProxyInfo(server, null, null));
                }
            }

            s_logProxiesScraped(_logger, source, proxies.Count, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            s_logScrapeFailed(_logger, source, ex);
        }

        return proxies;
    }

    /// <summary>
    /// Parses a newline-separated list of proxies in format "ip:port"
    /// </summary>
    private static List<ProxyInfo> ParseProxyList(string content)
    {
        List<ProxyInfo> proxies = [];

        if (string.IsNullOrWhiteSpace(content))
            return proxies;

        string[] lines = content.Split(s_lineSeparators, StringSplitOptions.RemoveEmptyEntries);

        foreach (string line in lines)
        {
            string trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed))
                continue;

            // Expected format: ip:port
            string[] parts = trimmed.Split(':');
            if (parts.Length == 2 && int.TryParse(parts[1], out int port))
            {
                string server = $"http://{parts[0]}:{port}";
                proxies.Add(new ProxyInfo(server, null, null));
            }
        }

        return proxies;
    }

    /// <summary>
    /// DTO for ProxyScan.io API response
    /// </summary>
    private sealed class ProxyScanProxy
    {
        public string Ip { get; set; } = string.Empty;
        public string Port { get; set; } = string.Empty;
    }
}
