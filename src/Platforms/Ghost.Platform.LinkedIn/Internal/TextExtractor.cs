using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;


namespace Ghost.Platform.LinkedIn.Internal;

internal static class TextExtractor
{
    public static async Task<List<string>> ExtractAllUniqueTextsAsync(Ghost.IElement element, CancellationToken ct = default)
    {
        var spans = await element.QuerySelectorAllAsync("span[aria-hidden='true']", ct).ConfigureAwait(false);
        var results = new List<string>();
        foreach (var span in spans)
        {
            var text = await span.GetTextContentAsync(ct).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(text))
            {
                var t = text.Trim();
                if (!results.Contains(t)) results.Add(t);
            }
        }
        if (results.Count == 0)
        {
            var text = await element.GetTextContentAsync(ct).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(text)) results.Add(text.Trim());
        }
        return results;
    }
    public static async Task<string> ExtractCleanTextAsync(Ghost.IElement element, CancellationToken ct = default)
    {
        if (element == null) return string.Empty;

        try
        {
            // try to find span[aria-hidden="true"]
            var span = await element.QuerySelectorAsync("span[aria-hidden=\"true\"]", ct).ConfigureAwait(false);
            string? text = null;
            if (span != null)
            {
                text = await span.GetTextContentAsync(ct).ConfigureAwait(false);
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                // fallback to element's text content
                text = await element.GetTextContentAsync(ct).ConfigureAwait(false);
            }

            return (text ?? string.Empty).Trim();
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            return string.Empty;
        }
    }
}
