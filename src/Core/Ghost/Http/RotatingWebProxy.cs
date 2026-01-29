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
        // Synchronously obtain a proxy from the provider. Use "US" as a default country code.
        var proxy = _provider.GetProxyAsync("US").GetAwaiter().GetResult();

        if (proxy is null)
            return destination; // direct connection

        return new Uri(proxy.Server);
    }

    public bool IsBypassed(Uri host)
    {
        // Never bypass; always let GetProxy decide per-request
        return false;
    }
}
