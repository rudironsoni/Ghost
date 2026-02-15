using System.Text.Json;
using System.Text.RegularExpressions;

namespace Ghost.Utilities;

public class JsonLdExtractor : IJsonLdExtractor
{
    private static readonly Regex JsonLdRegex = new("<script[^>]*type=[\"']application/ld\\+json[\"'][^>]*>(.*?)</script>", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

    public IEnumerable<T> Extract<T>(string html)
    {
        foreach (JsonElement el in ExtractRaw(html))
        {
            T? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<T>(el.GetRawText());
            }
            catch
            {
                continue;
            }

            yield return parsed!;
        }
    }

    public IEnumerable<JsonElement> ExtractRaw(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            yield break;

        MatchCollection matches = JsonLdRegex.Matches(html);
        foreach (Match m in matches)
        {
            string inner = m.Groups[1].Value.Trim();
            if (string.IsNullOrWhiteSpace(inner))
                continue;

            JsonElement root;
            try
            {
                using var doc = JsonDocument.Parse(inner);
                root = doc.RootElement.Clone();
            }
            catch
            {
                continue;
            }

            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in root.EnumerateArray())
                    yield return item;
            }
            else
            {
                yield return root;
            }
        }
    }
}
