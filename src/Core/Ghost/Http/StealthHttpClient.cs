using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;

namespace Ghost.Http;

public class StealthHttpClient
{
    private readonly HttpClient _client;
    private readonly RateLimitOptions _options;
    private readonly ILogger<StealthHttpClient>? _logger;
    private readonly Random _rng = new();
    private static readonly Action<ILogger<StealthHttpClient>, Exception?> _logJitterCancelled =
        LoggerMessage.Define(LogLevel.Debug, new EventId(1, "JitterCancelled"), "Jitter delay cancelled");

    public StealthHttpClient(HttpClient client, RateLimitOptions? options = null, ILogger<StealthHttpClient>? logger = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options ?? new RateLimitOptions();
        _logger = logger;
    }

    public async Task<HttpResponseMessage> GetAsync(string uri, CancellationToken ct = default)
    {
        await ApplyJitter(ct).ConfigureAwait(false);

        using var req = new HttpRequestMessage(HttpMethod.Get, uri);
        AddDefaultHeaders(req);

        var policy = RetryPolicy.CreatePolicy(_options.MaxRetries, _options.BackoffFactor);
        return await policy.ExecuteAsync(async () => await _client.SendAsync(CloneRequest(req), HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false)).ConfigureAwait(false);
    }

    public async Task<HttpResponseMessage> PostAsync(string uri, HttpContent content, CancellationToken ct = default)
    {
        await ApplyJitter(ct).ConfigureAwait(false);

        using var req = new HttpRequestMessage(HttpMethod.Post, uri) { Content = content };
        AddDefaultHeaders(req);

        var policy = RetryPolicy.CreatePolicy(_options.MaxRetries, _options.BackoffFactor);
        return await policy.ExecuteAsync(async () => await _client.SendAsync(CloneRequest(req), HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false)).ConfigureAwait(false);
    }

    private static void AddDefaultHeaders(HttpRequestMessage req)
    {
        if (req.Headers.UserAgent.Count == 0)
            req.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.0.0 Safari/537.36");
        req.Headers.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
        req.Headers.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
    }

    private async Task ApplyJitter(CancellationToken ct)
    {
        var delay = _rng.Next(_options.DelayMinMs, _options.DelayMaxMs + 1);
        try
        {
            await Task.Delay(delay, ct).ConfigureAwait(false);
        }
        catch (TaskCanceledException)
        {
            if (_logger is null)
                return;

            _logJitterCancelled(_logger, null);
        }
    }

    // HttpRequestMessage cannot be sent twice; clone minimal parts
    private static HttpRequestMessage CloneRequest(HttpRequestMessage req)
    {
        var clone = new HttpRequestMessage(req.Method, req.RequestUri);
        // copy headers
        foreach (var h in req.Headers)
            clone.Headers.TryAddWithoutValidation(h.Key, h.Value);

        if (req.Content != null)
        {
            clone.Content = new StreamContent(req.Content.ReadAsStreamAsync().GetAwaiter().GetResult());
            foreach (var h in req.Content.Headers)
                clone.Content.Headers.TryAddWithoutValidation(h.Key, h.Value);
        }

        return clone;
    }
}
