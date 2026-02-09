using Ghost.Core;

namespace Ghost.Testing.Helpers;

/// <summary>
/// Builder pattern helper for creating test data with fluent API.
/// Reduces test boilerplate and improves readability.
/// </summary>
public static class TestDataBuilder
{
    public static SessionOptionsBuilder SessionOptions() => new();

    public static PageOptionsBuilder PageOptions() => new();
}

public class SessionOptionsBuilder
{
    private string? _userAgent;
    private int _viewportWidth = 1280;
    private int _viewportHeight = 720;
    private string? _proxyServer;

    public SessionOptionsBuilder WithUserAgent(string userAgent)
    {
        _userAgent = userAgent;
        return this;
    }

    public SessionOptionsBuilder WithViewport(int width, int height)
    {
        _viewportWidth = width;
        _viewportHeight = height;
        return this;
    }

    public SessionOptionsBuilder WithProxy(string proxyServer)
    {
        _proxyServer = proxyServer;
        return this;
    }

    public SessionOptions Build() => new()
    {
        UserAgent = _userAgent,
        ViewportWidth = _viewportWidth,
        ViewportHeight = _viewportHeight,
        Proxy = _proxyServer != null ? new SessionOptions.ProxySettings(_proxyServer) : null
    };
}

public class PageOptionsBuilder
{
    private string? _userAgent;
    private int _width = 1280;
    private int _height = 720;

    public PageOptionsBuilder WithUserAgent(string userAgent)
    {
        _userAgent = userAgent;
        return this;
    }

    public PageOptionsBuilder WithViewport(int width, int height)
    {
        _width = width;
        _height = height;
        return this;
    }

    public PageOptions Build() => new()
    {
        UserAgent = _userAgent,
        Width = _width,
        Height = _height
    };
}
