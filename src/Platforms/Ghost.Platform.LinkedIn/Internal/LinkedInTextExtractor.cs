using System;
using System.Collections.Generic;
using Ghost.Abstractions;

namespace Ghost.Platform.LinkedIn.Internal;

internal sealed class LinkedInTextExtractor : ITextExtractor
{
    public string ExtractText(Ghost.IElement element, string? selector = null)
    {
        if (element is null) return string.Empty;
        try
        {
            Ghost.IElement? el = null;
            if (!string.IsNullOrEmpty(selector))
            {
                el = element.QuerySelectorAsync(selector).GetAwaiter().GetResult();
            }
            el ??= element;

            var span = el.QuerySelectorAsync("span[aria-hidden=\"true\"]").GetAwaiter().GetResult();
            string? text = null;
            if (span != null)
            {
                text = span.GetTextContentAsync().GetAwaiter().GetResult();
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                text = el.GetTextContentAsync().GetAwaiter().GetResult();
            }

            return (text ?? string.Empty).Trim();
        }
        catch
        {
            return string.Empty;
        }
    }

    public string ExtractInnerText(Ghost.IElement element)
    {
        if (element is null) return string.Empty;
        try
        {
            return (element.GetTextContentAsync().GetAwaiter().GetResult() ?? string.Empty).Trim();
        }
        catch
        {
            return string.Empty;
        }
    }
}
