using System;
using System.Net;
using Ghost.Abstractions;

namespace Ghost.Http;

public sealed class RotatingWebProxy : IWebProxy
{
    private readonly IProxyProvider _provider;

    public RotatingWebProxy(IProxyProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public ICredentials? Credentials { get; set; }

    public Uri GetProxy(Uri destination)
    {
        // WARNING: Synchronous blocking on async operation.
        // This is a limitation of the IWebProxy interface which doesn't support async methods.
        // Consider using a custom proxy implementation or caching proxies to avoid blocking.
        var proxy = _provider.GetProxyAsync("US").GetAwaiter().GetResult();

        if (proxy is null)
            return destination; // direct connection

        // Build the proxy Uri. If credentials are provided separately on the proxy
        // object, attach them to the URI using UriBuilder so that schemes like
        // socks5://username:password@host:port are produced. URL-encode the
        // username/password to ensure special characters are handled safely.
        var serverUri = new Uri(proxy.Server);
        var uriBuilder = new UriBuilder(serverUri);

        if (!string.IsNullOrEmpty(proxy.Username))
        {
            // UriBuilder will include the UserName/Password parts when creating the Uri.
            // Escape username/password to be safe with special characters.
            uriBuilder.UserName = Uri.EscapeDataString(proxy.Username);
            uriBuilder.Password = Uri.EscapeDataString(proxy.Password ?? string.Empty);
        }

        return uriBuilder.Uri;
    }

    public bool IsBypassed(Uri host)
    {
        // Never bypass; always let GetProxy decide per-request
        return false;
    }
}
