using System;
using System.Net;
using System.Text;

namespace Ghost.Plugin.Indeed.Internal;

public static class HtmlSanitizer
{
    public static string StripHtmlTags(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        var text = RemoveScriptsAndStyles(html);
        var sb = new StringBuilder(text.Length);
        var insideTag = false;
        var lastWasSpace = false;
        var pendingNewlines = 0;

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];

            if (insideTag)
            {
                if (ch == '>')
                {
                    insideTag = false;
                }

                continue;
            }

            if (ch == '<')
            {
                insideTag = true;

                if (TryReadTagName(text, i + 1, out var tagName, out var isClosing))
                {
                    if (IsNewlineTag(tagName, isClosing))
                    {
                        pendingNewlines = Math.Max(pendingNewlines, tagName == "p" || tagName.StartsWith('h') ? 2 : 1);
                        lastWasSpace = true;
                    }
                }

                continue;
            }

            if (pendingNewlines > 0)
            {
                AppendNewlines(sb, pendingNewlines);
                pendingNewlines = 0;
                lastWasSpace = true;
            }

            if (char.IsWhiteSpace(ch))
            {
                if (!lastWasSpace)
                {
                    sb.Append(' ');
                    lastWasSpace = true;
                }

                continue;
            }

            sb.Append(ch);
            lastWasSpace = false;
        }

        var cleaned = DecodeHtmlEntities(sb.ToString());
        cleaned = NormalizeWhitespace(cleaned);
        return cleaned;
    }

    public static string DecodeHtmlEntities(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        return WebUtility.HtmlDecode(text);
    }

    private static string RemoveScriptsAndStyles(string html)
    {
        var text = html;
        text = RemoveTagContent(text, "script");
        text = RemoveTagContent(text, "style");
        return text;
    }

    private static string RemoveTagContent(string html, string tagName)
    {
        var span = html.AsSpan();
        var result = new StringBuilder(html.Length);
        var index = 0;
        var tagOpen = "<" + tagName;
        var tagClose = "</" + tagName;

        while (index < span.Length)
        {
            var openIndex = IndexOfIgnoreCase(span, tagOpen.AsSpan(), index);
            if (openIndex < 0)
            {
                result.Append(span[index..]);
                break;
            }

            result.Append(span[index..openIndex]);
            var openEnd = IndexOf(span, '>', openIndex + tagOpen.Length);
            if (openEnd < 0)
                break;

            var closeIndex = IndexOfIgnoreCase(span, tagClose.AsSpan(), openEnd + 1);
            if (closeIndex < 0)
                break;

            var closeEnd = IndexOf(span, '>', closeIndex + tagClose.Length);
            if (closeEnd < 0)
                break;

            index = closeEnd + 1;
        }

        return result.ToString();
    }

    private static bool TryReadTagName(string text, int startIndex, out string tagName, out bool isClosing)
    {
        tagName = string.Empty;
        isClosing = false;

        if (startIndex >= text.Length)
            return false;

        var index = startIndex;
        while (index < text.Length && char.IsWhiteSpace(text[index]))
            index++;

        if (index < text.Length && text[index] == '/')
        {
            isClosing = true;
            index++;
        }

        var nameStart = index;
        while (index < text.Length && char.IsLetterOrDigit(text[index]))
            index++;

        if (index == nameStart)
            return false;

        tagName = text[nameStart..index].ToLowerInvariant();
        return true;
    }

    private static bool IsNewlineTag(string tagName, bool isClosing)
    {
        if (tagName == "br" && !isClosing)
            return true;

        if (!isClosing)
            return false;

        var isHeader = tagName.Length == 2 && tagName[0] == 'h' && tagName[1] >= '1' && tagName[1] <= '6';
        return tagName is "p" or "div" or "li" || isHeader;
    }

    private static void AppendNewlines(StringBuilder sb, int count)
    {
        var last = sb.Length > 0 ? sb[^1] : '\0';
        if (last == '\n')
        {
            count = Math.Max(0, count - 1);
        }

        for (var i = 0; i < count; i++)
            sb.Append('\n');
    }

    private static string NormalizeWhitespace(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var sb = new StringBuilder(text.Length);
        var lastWasSpace = false;
        var newlineCount = 0;

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == '\n')
            {
                if (newlineCount < 2)
                {
                    sb.Append('\n');
                }

                newlineCount++;
                lastWasSpace = true;
                continue;
            }

            if (char.IsWhiteSpace(ch))
            {
                if (!lastWasSpace)
                {
                    sb.Append(' ');
                    lastWasSpace = true;
                }

                continue;
            }

            sb.Append(ch);
            lastWasSpace = false;
            newlineCount = 0;
        }

        return sb.ToString().Trim();
    }

    private static int IndexOf(ReadOnlySpan<char> source, char value, int startIndex)
    {
        for (var i = startIndex; i < source.Length; i++)
        {
            if (source[i] == value)
                return i;
        }

        return -1;
    }

    private static int IndexOfIgnoreCase(ReadOnlySpan<char> source, ReadOnlySpan<char> value, int startIndex)
    {
        if (value.Length == 0)
            return startIndex;

        for (var i = startIndex; i <= source.Length - value.Length; i++)
        {
            var match = true;
            for (var j = 0; j < value.Length; j++)
            {
                if (char.ToLowerInvariant(source[i + j]) != char.ToLowerInvariant(value[j]))
                {
                    match = false;
                    break;
                }
            }

            if (match)
                return i;
        }

        return -1;
    }
}
