using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Polly;

namespace Ghost.Http;

public class StealthHttpClient
{
    private readonly HttpClient _client;
    private readonly RateLimitOptions _options;
    private readonly ILogger<StealthHttpClient>? _logger;
    private readonly Random _rng;
    private readonly TimeProvider _timeProvider;
    private static readonly Action<ILogger<StealthHttpClient>, Exception?> _logJitterCancelled =
        LoggerMessage.Define(LogLevel.Debug, new EventId(1, "JitterCancelled"), "Jitter delay cancelled");

    public StealthHttpClient(HttpClient client, RateLimitOptions? options = null, ILogger<StealthHttpClient>? logger = null, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
        _options = options ?? new RateLimitOptions();
        _logger = logger;
        _rng = new Random();
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<HttpResponseMessage> GetAsync(string uri, CancellationToken ct = default)
    {
        await ApplyJitterAsync(ct).ConfigureAwait(false);

        using var req = new HttpRequestMessage(HttpMethod.Get, uri);
        AddDefaultHeaders(req);

        IAsyncPolicy<HttpResponseMessage> policy = RetryPolicy.CreatePolicy(_options.MaxRetries, _options.BackoffFactor);
        return await policy.ExecuteAsync(async () => await _client.SendAsync(await CloneRequestAsync(req).ConfigureAwait(false), HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false)).ConfigureAwait(false);
    }

    public async Task<HttpResponseMessage> PostAsync(string uri, HttpContent content, CancellationToken ct = default)
    {
        await ApplyJitterAsync(ct).ConfigureAwait(false);

        using var req = new HttpRequestMessage(HttpMethod.Post, uri) { Content = content };
        AddDefaultHeaders(req);

        IAsyncPolicy<HttpResponseMessage> policy = RetryPolicy.CreatePolicy(_options.MaxRetries, _options.BackoffFactor);
        return await policy.ExecuteAsync(async () => await _client.SendAsync(await CloneRequestAsync(req).ConfigureAwait(false), HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false)).ConfigureAwait(false);
    }

    private static void AddDefaultHeaders(HttpRequestMessage req)
    {
        if (req.Headers.UserAgent.Count == 0)
            req.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.0.0 Safari/537.36");
        req.Headers.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
        req.Headers.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
    }

    private async Task ApplyJitterAsync(CancellationToken ct)
    {
        int delay = _rng.Next(_options.DelayMinMs, _options.DelayMaxMs + 1);
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(delay), _timeProvider, ct).ConfigureAwait(false);
        }
        catch (TaskCanceledException)
        {
            if (_logger is null)
                return;

            _logJitterCancelled(_logger, null);
        }
    }

    // HttpRequestMessage cannot be sent twice; clone minimal parts
    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage req)
    {
        var clone = new HttpRequestMessage(req.Method, req.RequestUri);
        // copy headers
        foreach (KeyValuePair<string, IEnumerable<string>> h in req.Headers)
            clone.Headers.TryAddWithoutValidation(h.Key, h.Value);

        if (req.Content != null)
        {
            Stream contentStream = await req.Content.ReadAsStreamAsync().ConfigureAwait(false);
            clone.Content = new StreamContent(contentStream);
            foreach (KeyValuePair<string, IEnumerable<string>> h in req.Content.Headers)
                clone.Content.Headers.TryAddWithoutValidation(h.Key, h.Value);
        }

        return clone;
    }
}
