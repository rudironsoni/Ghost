using System.Text;
using System.Text.RegularExpressions;

namespace Ghost.Plugin.X.Internal;

/// <summary>
/// Splits content into tweet-sized parts for thread support.
/// </summary>
public sealed class XPostContentSplitter
{
    private readonly string _urlPlaceholder;
    public int MaxLength { get; }

    public XPostContentSplitter(int maxLength = 280)
    {
        MaxLength = maxLength;
        _urlPlaceholder = "https://t.co/XXXXXXXXXX"; // X's URL shortener format
    }

    /// <summary>
    /// Splits content into tweet-sized parts.
    /// </summary>
    /// <param name="content">The content to split.</param>
    /// <returns>A list of tweet parts.</returns>
    public IReadOnlyList<string> Split(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return Array.Empty<string>();
        }

        // If content fits in one tweet, return it as-is
        if (EstimateLength(content) <= MaxLength)
        {
            return new[] { content };
        }

        // Split into sentences while respecting URLs
        List<string> sentences = ExtractSentences(content);
        List<string> parts = [];
        var currentPart = new StringBuilder();

        foreach (string sentence in sentences)
        {
            int estimatedLength = EstimateLength(currentPart.ToString() + sentence);

            if (estimatedLength <= MaxLength)
            {
                // Sentence fits, add it
                if (currentPart.Length > 0)
                {
                    currentPart.Append(' ');
                }
                currentPart.Append(sentence);
            }
            else
            {
                // Sentence doesn't fit, finalize current part
                if (currentPart.Length > 0)
                {
                    parts.Add(currentPart.ToString());
                    currentPart.Clear();
                }

                // Check if sentence itself is too long
                if (EstimateLength(sentence) > MaxLength)
                {
                    // Split long sentence at word boundaries
                    List<string> chunks = SplitLongSentence(sentence);
                    parts.AddRange(chunks);
                }
                else
                {
                    currentPart.Append(sentence);
                }
            }
        }

        // Add remaining content
        if (currentPart.Length > 0)
        {
            parts.Add(currentPart.ToString());
        }

        // Add thread numbering (1/N, 2/N, etc.)
        if (parts.Count > 1)
        {
            parts = AddThreadNumbering(parts);
        }

        return parts.AsReadOnly();
    }

    /// <summary>
    /// Extracts sentences from content while preserving URLs.
    /// </summary>
    private static List<string> ExtractSentences(string content)
    {
        List<string> sentences = [];
        string urlPattern = @"https?://[^\s]+";
        var urls = Regex.Matches(content, urlPattern).Select(m => m.Value).ToList();

        // Replace URLs with placeholders
        string tempContent = content;
        int urlIndex = 0;
        Dictionary<string, string> urlMap = [];

        foreach (string? url in urls)
        {
            string placeholder = $"{{URL{urlIndex}}}";
            urlMap[placeholder] = url;
            tempContent = tempContent.Replace(url, placeholder);
            urlIndex++;
        }

        // Split by sentence boundaries
        string sentencePattern = @"(?<=[.!?])\s+";
        var rawSentences = Regex.Split(tempContent, sentencePattern)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        // Restore URLs
        foreach (string? sentence in rawSentences)
        {
            string restored = sentence;
            foreach (KeyValuePair<string, string> kvp in urlMap)
            {
                restored = restored.Replace(kvp.Key, kvp.Value);
            }
            sentences.Add(restored);
        }

        return sentences;
    }

    /// <summary>
    /// Estimates the character length of content, treating URLs as fixed length.
    /// </summary>
    private int EstimateLength(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return 0;
        }

        string urlPattern = @"https?://[^\s]+";
        MatchCollection urls = Regex.Matches(content, urlPattern);
        int urlLength = urls.Count * _urlPlaceholder.Length;
        string nonUrlContent = Regex.Replace(content, urlPattern, "");

        return urlLength + nonUrlContent.Length;
    }

    /// <summary>
    /// Splits a long sentence at word boundaries.
    /// </summary>
    private List<string> SplitLongSentence(string sentence)
    {
        List<string> chunks = [];
        string[] words = sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var currentChunk = new StringBuilder();

        foreach (string word in words)
        {
            string testChunk = currentChunk.Length > 0
                ? currentChunk + " " + word
                : word;

            if (EstimateLength(testChunk) <= MaxLength)
            {
                if (currentChunk.Length > 0)
                {
                    currentChunk.Append(' ');
                }
                currentChunk.Append(word);
            }
            else
            {
                if (currentChunk.Length > 0)
                {
                    chunks.Add(currentChunk.ToString());
                    currentChunk.Clear();
                }

                // If single word is too long, split it into chunks
                if (EstimateLength(word) > MaxLength)
                {
                    List<string> wordChunks = SplitLongWord(word);
                    chunks.AddRange(wordChunks);
                }
                else
                {
                    currentChunk.Append(word);
                }
            }
        }

        if (currentChunk.Length > 0)
        {
            chunks.Add(currentChunk.ToString());
        }

        return chunks;
    }

    /// <summary>
    /// Splits a very long word into chunks of maxLength size.
    /// </summary>
    private List<string> SplitLongWord(string word)
    {
        List<string> chunks = [];
        int position = 0;

        while (position < word.Length)
        {
            int remainingLength = word.Length - position;
            int chunkSize = Math.Min(MaxLength, remainingLength);

            chunks.Add(word.Substring(position, chunkSize));
            position += chunkSize;
        }

        return chunks;
    }

    /// <summary>
    /// Adds thread numbering to parts.
    /// </summary>
    private List<string> AddThreadNumbering(List<string> parts)
    {
        List<string> numbered = [];
        int totalParts = parts.Count;

        for (int i = 0; i < parts.Count; i++)
        {
            int partNumber = i + 1;
            string suffix = $" ({partNumber}/{totalParts})";

            // Check if we need to trim content to fit the numbering
            string content = parts[i];
            if (EstimateLength(content + suffix) > MaxLength)
            {
                int maxContentLength = MaxLength - suffix.Length - 3; // -3 for "..."
                content = content[..maxContentLength] + "...";
            }

            numbered.Add(content + suffix);
        }

        return numbered;
    }



    /// <summary>
    /// Checks if content requires a thread (multiple tweets).
    /// </summary>
    public bool RequiresThread(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        return EstimateLength(content) > MaxLength;
    }

    /// <summary>
    /// Gets the estimated number of tweets required.
    /// </summary>
    public int GetEstimatedTweetCount(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return 0;
        }

        IReadOnlyList<string> parts = Split(content);
        return parts.Count;
    }
}
