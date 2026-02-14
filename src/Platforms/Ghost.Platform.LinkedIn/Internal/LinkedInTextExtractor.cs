using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ghost.Abstractions;

namespace Ghost.Platform.LinkedIn.Internal;

internal sealed class LinkedInTextExtractor : ITextExtractor
{
    public async Task<string> ExtractTextAsync(Ghost.IElement element, string? selector = null)
    {
        if (element is null) return string.Empty;
        try
        {
            Ghost.IElement? el = null;
            if (!string.IsNullOrEmpty(selector))
            {
                el = await element.QuerySelectorAsync(selector);
            }
            el ??= element;

            var span = await el.QuerySelectorAsync("span[aria-hidden=\"true\"]");
            string? text = null;
            if (span != null)
            {
                text = await span.GetTextContentAsync();
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                text = await el.GetTextContentAsync();
            }

            return (text ?? string.Empty).Trim();
        }
        catch
        {
            return string.Empty;
        }
    }

    public async Task<string> ExtractInnerTextAsync(Ghost.IElement element)
    {
        if (element is null) return string.Empty;
        try
        {
            return (await element.GetTextContentAsync() ?? string.Empty).Trim();
        }
        catch
        {
            return string.Empty;
        }
    }
}
