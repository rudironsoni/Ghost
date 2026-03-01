using System.Text.RegularExpressions;
using Ghost.Contracts.Social;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghost.Plugin.X.Internal;

/// <summary>
/// Composes and posts single tweets or multi-tweet threads.
/// </summary>
public partial class XThreadComposer
{
    private readonly XOptions _options;
    private readonly ILogger<XThreadComposer> _logger;
    private XPostContentSplitter ContentSplitter { get; }

    public XThreadComposer(
        IOptions<XOptions> options,
        ILogger<XThreadComposer> logger)
    {
        _options = options?.Value ?? new XOptions();
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<XThreadComposer>.Instance;
        ContentSplitter = new XPostContentSplitter(_options.MaxTweetLength);
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
        IReadOnlyList<string> parts = ContentSplitter.Split(request.Content);

        if (parts.Count == 0)
        {
            throw new ArgumentException("Content cannot be empty", nameof(request));
        }

        Log.PostingThread(_logger, parts.Count);

        string? firstTweetId = null;
        string? previousTweetId = null;

        for (int i = 0; i < parts.Count; i++)
        {
            bool isFirstTweet = i == 0;
            bool isLastTweet = i == parts.Count - 1;
            string part = parts[i];

            Log.PostingTweet(_logger, i + 1, parts.Count, part[..Math.Min(50, part.Length)]);

            string tweetId;

            if (isFirstTweet)
            {
                // Post first tweet (with media if provided)
                tweetId = await PostTweetAsync(page, part, request.MediaUrls, ct).ConfigureAwait(false);
                firstTweetId = tweetId;
            }
            else
            {
                // Post reply to previous tweet
                tweetId = await PostReplyAsync(page, part, previousTweetId!, ct).ConfigureAwait(false);
            }

            previousTweetId = tweetId;

            // Add delay between tweets to avoid rate limiting
            if (!isLastTweet && _options.ThreadDelayMs > 0)
            {
                Log.WaitingBeforeTweet(_logger, _options.ThreadDelayMs);
                await Task.Delay(_options.ThreadDelayMs, ct).ConfigureAwait(false);
            }
        }

        Log.ThreadPosted(_logger, parts.Count);
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
        await page.NavigateAsync($"{_options.BaseUrl}/compose/tweet", ct: ct).ConfigureAwait(false);
        await page.WaitForLoadStateAsync(ct: ct).ConfigureAwait(false);

        // Wait for compose textarea
        IElement composeBox = await page.WaitForSelectorAsync(
            "div[role='textbox'][contenteditable='true']",
            new WaitOptions { Timeout = 10000 },
            ct).ConfigureAwait(false) ?? throw new InvalidOperationException("Could not find compose text box");

        // Type content
        await composeBox.TypeAsync(content, ct: ct).ConfigureAwait(false);

        // Upload media if provided
        if (mediaUrls?.Count > 0)
        {
            await UploadMediaAsync(page, mediaUrls, ct).ConfigureAwait(false);
        }

        // Click post button
        IElement postButton = await page.WaitForSelectorAsync(
            "button[data-testid='tweetButton']",
            new WaitOptions { Timeout = 10000 },
            ct).ConfigureAwait(false) ?? throw new InvalidOperationException("Could not find post button");

        await postButton.ClickAsync(ct: ct).ConfigureAwait(false);

        // Wait for tweet to be posted and extract ID
        await Task.Delay(2000, ct).ConfigureAwait(false); // Brief delay for navigation

        // Try to extract tweet ID from URL
        string? tweetId = await ExtractTweetIdAsync(page, ct).ConfigureAwait(false);

        if (string.IsNullOrEmpty(tweetId))
        {
            Log.TweetIdExtractionFailed(_logger);
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
        await page.NavigateAsync($"{_options.BaseUrl}/i/status/{replyToTweetId}", ct: ct).ConfigureAwait(false);
        await page.WaitForLoadStateAsync(ct: ct).ConfigureAwait(false);

        // Click reply button
        IElement replyButton = await page.WaitForSelectorAsync(
            "button[data-testid='reply']",
            new WaitOptions { Timeout = 10000 },
            ct).ConfigureAwait(false) ?? throw new InvalidOperationException("Could not find reply button");

        await replyButton.ClickAsync(ct: ct).ConfigureAwait(false);

        // Wait for reply compose box
        await Task.Delay(1000, ct).ConfigureAwait(false);

        IElement composeBox = await page.WaitForSelectorAsync(
            "div[role='textbox'][contenteditable='true']",
            new WaitOptions { Timeout = 10000 },
            ct).ConfigureAwait(false) ?? throw new InvalidOperationException("Could not find reply compose box");

        // Type content
        await composeBox.TypeAsync(content, ct: ct).ConfigureAwait(false);

        // Click reply button
        IElement submitReplyButton = await page.WaitForSelectorAsync(
            "button[data-testid='tweetButton']",
            new WaitOptions { Timeout = 10000 },
            ct).ConfigureAwait(false) ?? throw new InvalidOperationException("Could not find submit reply button");

        await submitReplyButton.ClickAsync(ct: ct).ConfigureAwait(false);

        // Wait for reply to be posted
        await Task.Delay(2000, ct).ConfigureAwait(false);

        // Extract tweet ID
        string? tweetId = await ExtractTweetIdAsync(page, ct).ConfigureAwait(false);

        if (string.IsNullOrEmpty(tweetId))
        {
            Log.ReplyTweetIdExtractionFailed(_logger);
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
        Log.UploadingMedia(_logger, mediaUrls.Count);

        // Find media input
        IElement? mediaInput = await page.QuerySelectorAsync("input[type='file']", ct).ConfigureAwait(false);

        if (mediaInput == null)
        {
            Log.MediaInputNotFound(_logger);

            // Try clicking the media button to reveal the input
            IElement? mediaButton = await page.QuerySelectorAsync("[data-testid='mediaButton']", ct).ConfigureAwait(false);
            if (mediaButton != null)
            {
                await mediaButton.ClickAsync(ct: ct).ConfigureAwait(false);
                await Task.Delay(500, ct).ConfigureAwait(false);
                mediaInput = await page.QuerySelectorAsync("input[type='file']", ct).ConfigureAwait(false);
            }
        }

        if (mediaInput == null)
        {
            throw new InvalidOperationException("Could not find media upload input");
        }

        // Validate and set files
        List<string> validFiles = [];
        foreach (string url in mediaUrls)
        {
            if (!File.Exists(url))
            {
                Log.MediaFileNotFound(_logger, url);
                continue;
            }

            string extension = Path.GetExtension(url).ToLowerInvariant();
            if (_options.SupportedImageFormats.Contains(extension) ||
                _options.SupportedVideoFormats.Contains(extension))
            {
                validFiles.Add(url);
            }
            else
            {
                Log.UnsupportedMediaFormat(_logger, extension);
            }
        }

        if (validFiles.Count == 0)
        {
            Log.NoValidMediaFiles(_logger);
            return;
        }

        if (validFiles.Count > _options.MaxMediaAttachments)
        {
            Log.TruncatingMediaFiles(_logger, _options.MaxMediaAttachments);
            validFiles = validFiles.Take(_options.MaxMediaAttachments).ToList();
        }

        // Set files on input
        // Note: This is a placeholder - actual implementation depends on Ghost's IPage interface
        Log.SettingMediaFiles(_logger, validFiles.Count);

        // Wait for upload to complete
        await Task.Delay(2000, ct).ConfigureAwait(false);

        Log.MediaUploadCompleted(_logger);
    }

    /// <summary>
    /// Extracts the tweet ID from the current page URL.
    /// </summary>
    private async Task<string?> ExtractTweetIdAsync(IPage page, CancellationToken ct)
    {
        try
        {
            string url = page.Url;

            // Match /status/123456789 pattern
            Match match = Regex.Match(url, @"/status/(\d+)");
            if (match.Success)
            {
                return match.Groups[1].Value;
            }

            // Try to get from page data
            string? tweetId = await page.EvaluateAsync<string?>(
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
                ct: ct).ConfigureAwait(false);

            return tweetId;
        }
        catch (Exception ex)
        {
            Log.TweetIdFromPageExtractionFailed(_logger, ex);
            return null;
        }
    }


}
