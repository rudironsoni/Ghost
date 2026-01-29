using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Ghost.Platform.Glassdoor.Internal;

internal sealed class GlassdoorApiClient
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
            var res = await _http.GetAsync("https://www.glassdoor.com", ct);
            var html = await res.Content.ReadAsStringAsync(ct);
            // look for "token": "..." pattern
            var m = Regex.Match(html, "\"token\"\s*:\s*\"(?<t>[^\"]+)\"");
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

        using var payloadDoc = new JsonDocumentBuilder();
        // Construct a small Graph array payload: operationName and variables are enough for many guest endpoints
        var payload = JsonSerializer.Serialize(new[]
        {
            new
            {
                operationName = GlassdoorConstants.QueryTemplate,
                variables = new { keywords = keyword, location = location }
            }
        });

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        if (!string.IsNullOrEmpty(token)) content.Headers.Add("gd-csrf-token", token);
        foreach (var h in GlassdoorConstants.Headers)
        {
            content.Headers.Add(h.Name, h.Value);
        }

        var res = await _http.PostAsync(GlassdoorConstants.ApiUrl, content, ct);
        if (!res.IsSuccessStatusCode) return null;
        return await res.Content.ReadAsStringAsync(ct);
    }
}

// lightweight JsonDocument builder placeholder for potential extension (keeps code flexible)
internal sealed class JsonDocumentBuilder : IDisposable
{
    public void Dispose() { }
}
