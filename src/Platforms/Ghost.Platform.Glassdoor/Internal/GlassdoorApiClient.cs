using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Ghost.Platform.Glassdoor.Internal;

public sealed class GlassdoorApiClient
{
    private readonly HttpClient _http;

    public GlassdoorApiClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<string?> GetCsrfTokenAsync(CancellationToken ct = default)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "https://www.glassdoor.com");
            foreach (var header in GlassdoorConstants.CsrfHeaders)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            var res = await _http.SendAsync(request, ct);
            var html = await res.Content.ReadAsStringAsync(ct);

            // DEBUG: Write raw HTML to file
            try { System.IO.File.WriteAllText("logs/glassdoor_csrf.html", html); } catch { }

            var m = Regex.Match(html, "\"token\"\\s*:\\s*\"(?<t>[^\"]+)\"");
            if (m.Success)
            {
                return m.Groups["t"].Value;
            }
        }
        catch { }
        return GlassdoorConstants.FallbackToken;
    }

    public async Task<string?> SearchAsync(string keyword, string? location = null, string? csrfToken = null, CancellationToken ct = default)
    {
        var token = csrfToken ?? await GetCsrfTokenAsync(ct);

        var payload = JsonSerializer.Serialize(new[]
        {
            new
            {
                operationName = GlassdoorConstants.QueryTemplate,
                variables = new { keywords = keyword, location = location }
            }
        });

        var request = new HttpRequestMessage(HttpMethod.Post, GlassdoorConstants.ApiUrl)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        foreach (var header in GlassdoorConstants.GraphHeaders)
        {
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.TryAddWithoutValidation("gd-csrf-token", token);
        }

        var res = await _http.SendAsync(request, ct);
        if (!res.IsSuccessStatusCode) return null;
        var json = await res.Content.ReadAsStringAsync(ct);

        // DEBUG: Write raw JSON to file
        try { System.IO.File.WriteAllText("logs/glassdoor_search.json", json); } catch { }

        return json;
    }
}

// lightweight JsonDocument builder placeholder for potential extension (keeps code flexible)
internal sealed class JsonDocumentBuilder : IDisposable
{
    public void Dispose() { }
}
