using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text;
using System.Threading.Tasks;
using Ghost.Models;
using Ghost.Abstractions;
using Microsoft.Extensions.Logging;

namespace Ghost.Platform.Indeed.Internal;

    public class IndeedApiClient
    {
        private readonly IProxyProvider _proxyProvider;
        private readonly CountryCode _country;
        private readonly ILogger<IndeedApiClient> _logger;
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

        private static readonly CompositeFormat JobSearchQueryFormat = CompositeFormat.Parse(IndeedConstants.JobSearchQuery);

    public IndeedApiClient(Ghost.Abstractions.IProxyProvider proxyProvider, IndeedOptions options, ILogger<IndeedApiClient> logger)
    {
        _proxyProvider = proxyProvider;
        _country = options.Country;
        _logger = logger;
    }

    public async IAsyncEnumerable<JsonElement> SearchAsync(string query, string location, int limit = 50)
    {
        string? cursor = null;
        int remaining = limit;

        LogRequestStart(_logger, query, location, null);

        do
        {
            // Format the GraphQL query to match JobSpy-style query (no variables object)
            var formattedQuery = string.Format(System.Globalization.CultureInfo.InvariantCulture, JobSearchQueryFormat, query, location, Math.Min(25, remaining));
            var payload = new { query = formattedQuery };
            var json = JsonSerializer.Serialize(payload);
            LogRequestPayload(_logger, json, null);

            using var req = new HttpRequestMessage(HttpMethod.Post, IndeedConstants.ApiUrl)
            {
                Content = JsonContent.Create(payload)
            };

            LogSendingRequest(_logger, IndeedConstants.ApiUrl, null);

            // Log request headers on the HttpRequestMessage before sending so we
            // can verify presence of headers like `indeed-api-key`.
            foreach (var header in req.Headers)
            {
                var value = string.Join(",", header.Value);
                LogRequestHeader(_logger, header.Key, value, null);
            }

            // get a proxy for this request/session
            var proxy = await _proxyProvider.GetProxyAsync(_country.ToString());

            var handler = new SocketsHttpHandler();
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

            // set default headers (User-Agent, etc.)
            foreach (var kv in IndeedConstants.GetHeaders(_country))
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation(kv.Key, kv.Value);
            }

            // Log default request headers from the HttpClient so we can confirm
            // presence of headers like `indeed-api-key` before sending.
            foreach (var header in client.DefaultRequestHeaders)
            {
                LogRequestHeader(_logger, header.Key, string.Join(",", header.Value), null);
            }

            // Also log headers on the HttpRequestMessage (if any) just before send.
            foreach (var header in req.Headers)
            {
                LogRequestHeader(_logger, header.Key, string.Join(",", header.Value), null);
            }

            var resp = await client.SendAsync(req);
            if ((int)resp.StatusCode == 429)
            {
                await Task.Delay(1000);
                continue;
            }

            LogResponseStatus(_logger, resp.StatusCode.ToString(), null);

            // Read and log the response content before enforcing success status so
            // we can see error messages returned by Indeed when the status is non-success.
            var content = await resp.Content.ReadAsStringAsync();
            LogResponseContent(_logger, content, null);

            resp.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(content);
            if (doc is null) yield break;

            yield return doc.RootElement.Clone();

            // get next cursor
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
}
