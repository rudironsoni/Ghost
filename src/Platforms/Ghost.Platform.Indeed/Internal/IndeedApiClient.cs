using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Ghost.Models;

namespace Ghost.Platform.Indeed.Internal;

public class IndeedApiClient
{
    private readonly HttpClient _http;
    private readonly CountryCode _country;

    public IndeedApiClient(HttpClient http, IndeedOptions options)
    {
        _http = http;
        _country = options.Country;
    }

    public async IAsyncEnumerable<JsonElement> SearchAsync(string query, string location, int limit = 50)
    {
        string? cursor = null;
        int remaining = limit;

        do
        {
            var variables = new Dictionary<string, object?>
            {
                ["what"] = query,
                ["where"] = location,
                ["pageSize"] = Math.Min(25, remaining),
                ["cursor"] = cursor
            };

            var payload = new { query = IndeedConstants.JobSearchQuery, variables };

            using var req = new HttpRequestMessage(HttpMethod.Post, IndeedConstants.ApiUrl)
            {
                Content = JsonContent.Create(payload)
            };

            foreach (var kv in IndeedConstants.GetHeaders(_country))
            {
                if (!req.Headers.Contains(kv.Key)) req.Headers.Add(kv.Key, kv.Value);
            }

            var resp = await _http.SendAsync(req);
            if ((int)resp.StatusCode == 429)
            {
                await Task.Delay(1000);
                continue;
            }

            resp.EnsureSuccessStatusCode();
            var doc = await JsonSerializer.DeserializeAsync<JsonDocument>(await resp.Content.ReadAsStreamAsync());
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
