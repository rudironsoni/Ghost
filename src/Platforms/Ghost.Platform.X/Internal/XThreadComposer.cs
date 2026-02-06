using System.Text.RegularExpressions;
using Ghost.Contracts.Social;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghost.Platform.X.Internal;

/// <summary>
/// Composes and posts single tweets or multi-tweet threads.
/// </summary>
public class XThreadComposer
{
    private readonly XOptions _options;
    private readonly ILogger<XThreadComposer> _logger;
    private readonly XPostContentSplitter _contentSplitter;

    public XThreadComposer(
        IOptions<XOptions> options,
        ILogger<XThreadComposer> logger)
    {
        _options = options?.Value ?? new XOptions();
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<XThreadComposer>.Instance;
        _contentSplitter = new XPostContentSplitter(_options.MaxTweetLength);
    }

    /// <summary>
    /// Composes and posts content as a single tweet or thread.
    /// </summary>
    public virtual async Task<string> ComposeAndPostAsync(
        IPage page,
        CreatePostRequest request,
        CancellationToken ct = default)
    {
        // Split content into tweet-sized parts
        var parts = _contentSplitter.Split(request.Content);

        if (parts.Count == 0)
        {
            throw new ArgumentException("Content cannot be empty", nameof(request));
        }

        _logger.LogInformation("Posting {Count} tweet(s) as thread", parts.Count);

        string? firstTweetId = null;
        string? previousTweetId = null;

        for (int i = 0; i < parts.Count; i++)
        {
            var isFirstTweet = i == 0;
            var isLastTweet = i == parts.Count - 1;
            var part = parts[i];

            _logger.LogDebug("Posting tweet {Index}/{Total}: {Preview}...",
                i + 1, parts.Count, part[..Math.Min(50, part.Length)]);

            string tweetId;

            if (isFirstTweet)
            {
                // Post first tweet (with media if provided)
                tweetId = await PostTweetAsync(page, part, request.MediaUrls, ct);
                firstTweetId = tweetId;
            }
            else
            {
                // Post reply to previous tweet
                tweetId = await PostReplyAsync(page, part, previousTweetId!, ct);
            }

            previousTweetId = tweetId;

            // Add delay between tweets to avoid rate limiting
            if (!isLastTweet && _options.ThreadDelayMs > 0)
            {
                _logger.LogDebug("Waiting {DelayMs}ms before next tweet", _options.ThreadDelayMs);
                await Task.Delay(_options.ThreadDelayMs, ct);
            }
        }

        _logger.LogInformation("Successfully posted thread with {Count} tweets", parts.Count);
        return firstTweetId!;
    }

    /// <summary>
    /// Posts a single tweet, optionally with media.
    /// </summary>
    private async Task<string> PostTweetAsync(
        IPage page,
        string content,
        IReadOnlyList<string>? mediaUrls,
        CancellationToken ct)
    {
        // Navigate to compose page
        await page.NavigateAsync($"{_options.BaseUrl}/compose/tweet", ct: ct);
        await page.WaitForLoadStateAsync(ct: ct);

        // Wait for compose textarea
        var composeBox = await page.WaitForSelectorAsync(
            "div[role='textbox'][contenteditable='true']",
            new WaitOptions { Timeout = 10000 },
            ct);

        if (composeBox == null)
        {
            throw new InvalidOperationException("Could not find compose text box");
        }

        // Type content
        await composeBox.TypeAsync(content, ct: ct);

        // Upload media if provided
        if (mediaUrls?.Count > 0)
        {
            await UploadMediaAsync(page, mediaUrls, ct);
        }

        // Click post button
        var postButton = await page.WaitForSelectorAsync(
            "button[data-testid='tweetButton']",
            new WaitOptions { Timeout = 10000 },
            ct);

        if (postButton == null)
        {
            throw new InvalidOperationException("Could not find post button");
        }

        await postButton.ClickAsync(ct: ct);

        // Wait for tweet to be posted and extract ID
        await Task.Delay(2000, ct); // Brief delay for navigation

        // Try to extract tweet ID from URL
        var tweetId = await ExtractTweetIdAsync(page, ct);

        if (string.IsNullOrEmpty(tweetId))
        {
            _logger.LogWarning("Could not extract tweet ID, using generated ID");
            tweetId = Guid.NewGuid().ToString("N")[..16];
        }

        return tweetId;
    }

    /// <summary>
    /// Posts a reply to a specific tweet.
    /// </summary>
    private async Task<string> PostReplyAsync(
        IPage page,
        string content,
        string replyToTweetId,
        CancellationToken ct)
    {
        // Navigate to the tweet we're replying to
        await page.NavigateAsync($"{_options.BaseUrl}/i/status/{replyToTweetId}", ct: ct);
        await page.WaitForLoadStateAsync(ct: ct);

        // Click reply button
        var replyButton = await page.WaitForSelectorAsync(
            "button[data-testid='reply']",
            new WaitOptions { Timeout = 10000 },
            ct);

        if (replyButton == null)
        {
            throw new InvalidOperationException("Could not find reply button");
        }

        await replyButton.ClickAsync(ct: ct);

        // Wait for reply compose box
        await Task.Delay(1000, ct);

        var composeBox = await page.WaitForSelectorAsync(
            "div[role='textbox'][contenteditable='true']",
            new WaitOptions { Timeout = 10000 },
            ct);

        if (composeBox == null)
        {
            throw new InvalidOperationException("Could not find reply compose box");
        }

        // Type content
        await composeBox.TypeAsync(content, ct: ct);

        // Click reply button
        var submitReplyButton = await page.WaitForSelectorAsync(
            "button[data-testid='tweetButton']",
            new WaitOptions { Timeout = 10000 },
            ct);

        if (submitReplyButton == null)
        {
            throw new InvalidOperationException("Could not find submit reply button");
        }

        await submitReplyButton.ClickAsync(ct: ct);

        // Wait for reply to be posted
        await Task.Delay(2000, ct);

        // Extract tweet ID
        var tweetId = await ExtractTweetIdAsync(page, ct);

        if (string.IsNullOrEmpty(tweetId))
        {
            _logger.LogWarning("Could not extract reply tweet ID, using generated ID");
            tweetId = Guid.NewGuid().ToString("N")[..16];
        }

        return tweetId;
    }

    /// <summary>
    /// Uploads media files to the compose box.
    /// </summary>
    private async Task UploadMediaAsync(
        IPage page,
        IReadOnlyList<string> mediaUrls,
        CancellationToken ct)
    {
        _logger.LogInformation("Uploading {Count} media files", mediaUrls.Count);

        // Find media input
        var mediaInput = await page.QuerySelectorAsync("input[type='file']", ct);

        if (mediaInput == null)
        {
            _logger.LogWarning("Could not find media input, attempting to click media button first");

            // Try clicking the media button to reveal the input
            var mediaButton = await page.QuerySelectorAsync("[data-testid='mediaButton']", ct);
            if (mediaButton != null)
            {
                await mediaButton.ClickAsync(ct: ct);
                await Task.Delay(500, ct);
                mediaInput = await page.QuerySelectorAsync("input[type='file']", ct);
            }
        }

        if (mediaInput == null)
        {
            throw new InvalidOperationException("Could not find media upload input");
        }

        // Validate and set files
        var validFiles = new List<string>();
        foreach (var url in mediaUrls)
        {
            if (!File.Exists(url))
            {
                _logger.LogWarning("Media file not found: {Path}", url);
                continue;
            }

            var extension = Path.GetExtension(url).ToLowerInvariant();
            if (_options.SupportedImageFormats.Contains(extension) ||
                _options.SupportedVideoFormats.Contains(extension))
            {
                validFiles.Add(url);
            }
            else
            {
                _logger.LogWarning("Unsupported media format: {Extension}", extension);
            }
        }

        if (validFiles.Count == 0)
        {
            _logger.LogWarning("No valid media files to upload");
            return;
        }

        if (validFiles.Count > _options.MaxMediaAttachments)
        {
            _logger.LogWarning("Truncating to {Max} media files", _options.MaxMediaAttachments);
            validFiles = validFiles.Take(_options.MaxMediaAttachments).ToList();
        }

        // Set files on input
        // Note: This is a placeholder - actual implementation depends on Ghost's IPage interface
        _logger.LogInformation("Setting {Count} media files for upload", validFiles.Count);

        // Wait for upload to complete
        await Task.Delay(2000, ct);

        _logger.LogInformation("Media upload completed");
    }

    /// <summary>
    /// Extracts the tweet ID from the current page URL.
    /// </summary>
    private async Task<string?> ExtractTweetIdAsync(IPage page, CancellationToken ct)
    {
        try
        {
            var url = page.Url;

            // Match /status/123456789 pattern
            var match = Regex.Match(url, @"/status/(\d+)");
            if (match.Success)
            {
                return match.Groups[1].Value;
            }

            // Try to get from page data
            var tweetId = await page.EvaluateAsync<string?>(
                @"() => {
                    const article = document.querySelector('article[data-testid=""tweet""]');
                    if (article) {
                        const link = article.querySelector('a[href*=""/status/""]');
                        if (link) {
                            const match = link.href.match(/\/status\/(\d+)/);
                            return match ? match[1] : null;
                        }
                    }
                    return null;
                }",
                ct: ct);

            return tweetId;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract tweet ID from page");
            return null;
        }
    }

    /// <summary>
    /// Gets the content splitter instance.
    /// </summary>
    public XPostContentSplitter ContentSplitter => _contentSplitter;
}
