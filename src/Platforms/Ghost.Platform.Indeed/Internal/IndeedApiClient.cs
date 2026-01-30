using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text;
using System.Threading.Tasks;
using System.Security.Authentication;
using Ghost.Models;
using Ghost.Abstractions;
using Ghost.Http;
using Microsoft.Extensions.Logging;

namespace Ghost.Platform.Indeed.Internal;

public class IndeedApiClient : IDisposable
{
    private readonly IProxyProvider _proxyProvider;
    private readonly CountryCode _country;
    private readonly string _apiKey;
    private readonly ILogger<IndeedApiClient> _logger;
    private readonly SemaphoreSlim _rateLimitSemaphore = new(1, 1);
    private DateTime _lastRequestTime = DateTime.MinValue;
    private readonly TimeSpan _rateLimitDelay = TimeSpan.FromSeconds(2);
    private bool _disposed;
        private static readonly Action<ILogger, string, string, Exception?> LogRequestStart =
            LoggerMessage.Define<string, string>(LogLevel.Information, new EventId(2001, "FetchingIndeedJobs"), "Fetching Indeed jobs for query '{Query}' at {Location}...");
        private static readonly Action<ILogger, string, Exception?> LogSendingRequest =
            LoggerMessage.Define<string>(LogLevel.Information, new EventId(2002, "SendingRequest"), "Sending request to {Url}");
        private static readonly Action<ILogger, string, Exception?> LogResponseStatus =
            LoggerMessage.Define<string>(LogLevel.Information, new EventId(2003, "ResponseStatus"), "Response Status: {StatusCode}");
        private static readonly Action<ILogger, string, Exception?> LogResponseContent =
            LoggerMessage.Define<string>(LogLevel.Information, new EventId(2004, "ResponseContent"), "Response Content: {Content}");
        private static readonly Action<ILogger, string, Exception?> LogRequestPayload =
            LoggerMessage.Define<string>(LogLevel.Information, new EventId(2005, "RequestPayload"), "Request Payload: {Content}");
        private static readonly Action<ILogger, string, string, Exception?> LogRequestHeader =
            LoggerMessage.Define<string, string>(LogLevel.Information, new EventId(2006, nameof(LogRequestHeader)), "Header: {Key} = {Value}");

        private static readonly Action<ILogger, CountryCode, Exception?> LogConstructedWithCountry =
            LoggerMessage.Define<CountryCode>(LogLevel.Information, new EventId(2007, "ConstructedWithCountry"), "IndeedApiClient constructed with Country={Country}");

        private static readonly Action<ILogger, CountryCode, Exception?> LogGetHeadersReturnedNull =
            LoggerMessage.Define<CountryCode>(LogLevel.Warning, new EventId(2008, "GetHeadersReturnedNull"), "IndeedConstants.GetHeaders returned null for country {Country}");

        private static readonly Action<ILogger, CountryCode, Exception?> LogUsingCountryForRequest =
            LoggerMessage.Define<CountryCode>(LogLevel.Information, new EventId(2009, "UsingCountryForRequest"), "IndeedApiClient: using country {Country} when sending request");

        private static readonly CompositeFormat JobSearchQueryFormat = CompositeFormat.Parse(IndeedConstants.JobSearchQuery);

    public IndeedApiClient(Ghost.Abstractions.IProxyProvider proxyProvider, IndeedOptions options, ILogger<IndeedApiClient> logger)
    {
        _proxyProvider = proxyProvider;
        _country = options.Country;
        _apiKey = options.ApiKey;
        _logger = logger;
        try
        {
            LogConstructedWithCountry(_logger, _country, null);
        }
        catch { }
    }

    private async Task ApplyRateLimitAsync(CancellationToken ct)
    {
        await _rateLimitSemaphore.WaitAsync(ct);
        try
        {
            var timeSinceLastRequest = DateTime.UtcNow - _lastRequestTime;
            if (timeSinceLastRequest < _rateLimitDelay)
            {
                var waitTime = _rateLimitDelay - timeSinceLastRequest;
                await Task.Delay(waitTime, ct);
            }
            _lastRequestTime = DateTime.UtcNow;
        }
        finally
        {
            _rateLimitSemaphore.Release();
        }
    }

    public async IAsyncEnumerable<JsonElement> SearchAsync(string query, string location, int limit = 50)
    {
        string? cursor = null;
        int remaining = limit;

        LogRequestStart(_logger, query, location, null);

        do
        {
            await ApplyRateLimitAsync(default);

            var formattedQuery = string.Format(System.Globalization.CultureInfo.InvariantCulture, JobSearchQueryFormat, query, location, Math.Min(25, remaining));
            var payload = new { query = formattedQuery };
            var json = JsonSerializer.Serialize(payload);
            LogRequestPayload(_logger, json, null);

            using var req = new HttpRequestMessage(HttpMethod.Post, IndeedConstants.ApiUrl)
            {
                Content = JsonContent.Create(payload)
            };

            // Ensure Content-Type header is set on the request content. Some servers
            // (including Indeed's GraphQL endpoint) require an explicit
            // "application/json" content-type for GraphQL POST requests.
            if (req.Content != null && !req.Content.Headers.Contains("Content-Type"))
            {
                req.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            }

            LogSendingRequest(_logger, IndeedConstants.ApiUrl, null);

            foreach (var header in req.Headers)
            {
                var value = string.Join(",", header.Value);
                LogRequestHeader(_logger, header.Key, value, null);
            }

            var proxy = await _proxyProvider.GetProxyAsync(_country.ToString());

            var handler = new SocketsHttpHandler
            {
                SslOptions = new System.Net.Security.SslClientAuthenticationOptions
                {
                    RemoteCertificateValidationCallback = HttpClientSecurityExtensions.CreateCertificateValidationCallback(),
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
                }
            };

            if (proxy != null)
            {
                var webProxy = new WebProxy(new Uri(proxy.Server));
                if (!string.IsNullOrEmpty(proxy.Username))
                {
                    webProxy.Credentials = new NetworkCredential(proxy.Username, proxy.Password);
                }
                handler.Proxy = webProxy;
                handler.UseProxy = true;
            }

            using var client = new HttpClient(handler);

            Dictionary<string, string> headers;
            try
            {
                headers = IndeedConstants.GetHeaders(_country, _apiKey);
            }
            catch (ArgumentException ex)
            {
                LogGetHeadersReturnedNull(_logger, _country, ex);
                if (!string.IsNullOrEmpty(_apiKey))
                {
                    headers = IndeedConstants.GetHeaders(CountryCode.US, _apiKey);
                }
                else
                {
                    throw;
                }
            }

            foreach (var kv in headers)
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation(kv.Key, kv.Value);
            }

            foreach (var header in client.DefaultRequestHeaders)
            {
                LogRequestHeader(_logger, header.Key, string.Join(",", header.Value), null);
            }

            foreach (var header in req.Headers)
            {
                LogRequestHeader(_logger, header.Key, string.Join(",", header.Value), null);
            }

            try
            {
                LogUsingCountryForRequest(_logger, _country, null);
            }
            catch { }

            HttpResponseMessage? resp = null;
            string content = string.Empty;

            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    resp = await client.SendAsync(req);

                    if ((int)resp.StatusCode == 429)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * 1000));
                        continue;
                    }

                    LogResponseStatus(_logger, resp.StatusCode.ToString(), null);

                    content = await resp.Content.ReadAsStringAsync();
                    LogResponseContent(_logger, content, null);

                    try { System.IO.File.WriteAllText($"logs/indeed_jobs_search_{attempt}.json", content); } catch { }

                    resp.EnsureSuccessStatusCode();

                    if (IsBlockedOrConsentRequired(content))
                    {
                        if (attempt < 2)
                        {
                            await Task.Delay(TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * 2000));
                            continue;
                        }
                        break;
                    }

                    break;
                }
                catch (HttpRequestException) when (attempt < 2)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * 1000));
                    continue;
                }
                catch (Exception)
                {
                    throw;
                }
            }

            if (resp == null || !resp.IsSuccessStatusCode || IsBlockedOrConsentRequired(content))
            {
                break;
            }

            using var doc = JsonDocument.Parse(content);
            if (doc is null) yield break;

            yield return doc.RootElement.Clone();

            if (!doc.RootElement.TryGetProperty("data", out var data) || !data.TryGetProperty("jobSearch", out var jobSearch) || !jobSearch.TryGetProperty("pageInfo", out var pageInfo) || !pageInfo.TryGetProperty("nextCursor", out var nextCursorEl))
            {
                break;
            }

            cursor = nextCursorEl.GetString();
            if (string.IsNullOrEmpty(cursor)) break;
            remaining -= 25;
        }
        while (remaining > 0);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _rateLimitSemaphore?.Dispose();
            _disposed = true;
        }
    }

    private static bool IsBlockedOrConsentRequired(string responseContent)
    {
        if (string.IsNullOrEmpty(responseContent))
            return true;

        return responseContent.Contains("access denied", StringComparison.OrdinalIgnoreCase) ||
               responseContent.Contains("rate limit", StringComparison.OrdinalIgnoreCase) ||
               responseContent.Contains("throttled", StringComparison.OrdinalIgnoreCase) ||
               responseContent.Contains("unauthorized", StringComparison.OrdinalIgnoreCase) ||
               responseContent.Contains("forbidden", StringComparison.OrdinalIgnoreCase) ||
               responseContent.Contains("blocked", StringComparison.OrdinalIgnoreCase) ||
               responseContent.Contains("captcha", StringComparison.OrdinalIgnoreCase) ||
               responseContent.Contains("robot", StringComparison.OrdinalIgnoreCase) ||
               responseContent.Contains("verify", StringComparison.OrdinalIgnoreCase);
    }
}
