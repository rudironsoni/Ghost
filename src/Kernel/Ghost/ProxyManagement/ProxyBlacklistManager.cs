using System.Collections.Concurrent;
using Ghost.ProxyConfiguration;
using Microsoft.Extensions.Logging;

namespace Ghost.ProxyManagement;

/// <summary>
/// Manages proxy blacklist and whitelist.
/// Single responsibility: Tracks which proxies are blacklisted or whitelisted.
/// </summary>
public sealed class ProxyBlacklistManager
{
    private readonly ConcurrentDictionary<string, bool> _blacklist = new();
    private readonly HashSet<string> _whitelist = [];
    private readonly ILogger? _logger;

    private static readonly Action<ILogger, string, Exception?> s_logProxyBlacklisted =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(1, "ProxyBlacklisted"), "Proxy {Proxy} blacklisted");

    private static readonly Action<ILogger, string, Exception?> s_logProxyRemovedFromBlacklist =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(2, "ProxyRemovedFromBlacklist"), "Proxy {Proxy} removed from blacklist");

    private static readonly Action<ILogger, string, Exception?> s_logProxyWhitelisted =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(3, "ProxyWhitelisted"), "Proxy {Proxy} added to whitelist");

    public ProxyBlacklistManager(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Adds a proxy to the blacklist.
    /// </summary>
    public void Blacklist(ProxyInfo proxy)
    {
        if (proxy == null)
            return;

        string key = GetProxyKey(proxy);
        _blacklist.TryAdd(key, true);

        if (_logger != null)
        {
            s_logProxyBlacklisted(_logger, proxy.Server, null);
        }
    }

    /// <summary>
    /// Removes a proxy from the blacklist.
    /// </summary>
    public void RemoveFromBlacklist(ProxyInfo proxy)
    {
        if (proxy == null)
            return;

        string key = GetProxyKey(proxy);
        _blacklist.TryRemove(key, out _);

        if (_logger != null)
        {
            s_logProxyRemovedFromBlacklist(_logger, proxy.Server, null);
        }
    }

    /// <summary>
    /// Checks if a proxy is blacklisted.
    /// </summary>
    public bool IsBlacklisted(ProxyInfo proxy)
    {
        if (proxy == null)
            return true;

        string key = GetProxyKey(proxy);
        return _blacklist.ContainsKey(key);
    }

    /// <summary>
    /// Adds a proxy to the whitelist for priority usage.
    /// </summary>
    public void Whitelist(ProxyInfo proxy)
    {
        if (proxy == null)
            return;

        string key = GetProxyKey(proxy);
        lock (_whitelist)
        {
            _whitelist.Add(key);
        }

        if (_logger != null)
        {
            s_logProxyWhitelisted(_logger, proxy.Server, null);
        }
    }

    /// <summary>
    /// Removes a proxy from the whitelist.
    /// </summary>
    public void RemoveFromWhitelist(ProxyInfo proxy)
    {
        if (proxy == null)
            return;

        string key = GetProxyKey(proxy);
        lock (_whitelist)
        {
            _whitelist.Remove(key);
        }
    }

    /// <summary>
    /// Checks if a proxy is whitelisted.
    /// </summary>
    public bool IsWhitelisted(ProxyInfo proxy)
    {
        if (proxy == null)
            return false;

        string key = GetProxyKey(proxy);
        lock (_whitelist)
        {
            return _whitelist.Contains(key);
        }
    }

    /// <summary>
    /// Gets all whitelisted proxies from the pool.
    /// </summary>
    public List<ProxyInfo> GetWhitelistedProxies(IEnumerable<KeyValuePair<string, ProxyInfo>> proxyPool)
    {
        List<ProxyInfo> whitelisted = [];

        lock (_whitelist)
        {
            foreach (string whitelistedKey in _whitelist)
            {
                if (TryGetProxyByKey(proxyPool, whitelistedKey, out ProxyInfo? proxy))
                {
                    whitelisted.Add(proxy!);
                }
            }
        }

        return whitelisted;
    }

    /// <summary>
    /// Gets all blacklisted proxy keys.
    /// </summary>
    public IEnumerable<string> GetBlacklistedKeys()
    {
        return _blacklist.Keys;
    }

    /// <summary>
    /// Gets all whitelisted proxy keys.
    /// </summary>
    public IEnumerable<string> GetWhitelistedKeys()
    {
        lock (_whitelist)
        {
            return _whitelist.ToList();
        }
    }

    /// <summary>
    /// Clears all entries from the blacklist.
    /// </summary>
    public void ClearBlacklist()
    {
        _blacklist.Clear();
    }

    /// <summary>
    /// Clears all entries from the whitelist.
    /// </summary>
    public void ClearWhitelist()
    {
        lock (_whitelist)
        {
            _whitelist.Clear();
        }
    }

    /// <summary>
    /// Gets the count of blacklisted proxies.
    /// </summary>
    public int BlacklistCount => _blacklist.Count;

    /// <summary>
    /// Gets the count of whitelisted proxies.
    /// </summary>
    public int WhitelistCount
    {
        get
        {
            lock (_whitelist)
            {
                return _whitelist.Count;
            }
        }
    }

    private static string GetProxyKey(ProxyInfo proxy)
    {
        return $"{proxy.Server}|{proxy.Username ?? ""}";
    }

    private static bool TryGetProxyByKey(IEnumerable<KeyValuePair<string, ProxyInfo>> proxyPool, string key, out ProxyInfo? proxy)
    {
        foreach (KeyValuePair<string, ProxyInfo> kvp in proxyPool)
        {
            if (kvp.Key == key)
            {
                proxy = kvp.Value;
                return true;
            }
        }

        proxy = null;
        return false;
    }
}
