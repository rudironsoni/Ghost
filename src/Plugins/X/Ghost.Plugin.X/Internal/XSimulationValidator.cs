using System.Reflection;
using System.Text.RegularExpressions;
using Ghost.Contracts.Simulation;
using Ghost.Contracts.Social;
using Microsoft.Extensions.Options;

namespace Ghost.Plugin.X.Internal;

// NOTE: Reviewed for CS0176 (static access via instance). Static members
// on XOptions are referenced via the type (XOptions) throughout this file.

/// <summary>
/// Validates content and simulates actions for the X platform.
/// </summary>
public sealed class XSimulationValidator : IXPlatformSimulationValidator
{
    private readonly XOptions _options;
    private XPostContentSplitter ContentSplitter { get; }

    public XSimulationValidator(IOptions<XOptions> options)
    {
        _options = options?.Value ?? new XOptions();
        ContentSplitter = new XPostContentSplitter(XOptions.MaxTweetLength);
    }

    /// <inheritdoc />
    public string PlatformName => "X";

    /// <inheritdoc />
    public int MaxContentLength => XOptions.MaxTweetLength;

    /// <inheritdoc />
    public int MaxMediaAttachments => XOptions.MaxMediaAttachments;

    /// <inheritdoc />
    public IReadOnlyList<string> SupportedMediaTypes =>
        _options.SupportedImageFormats.Concat(_options.SupportedVideoFormats).ToList().AsReadOnly();

    /// <inheritdoc />
    public async Task<ValidationResult> ValidatePostAsync(CreatePostRequest request)
    {
        List<ValidationError> errors = [];
        List<ValidationError> warnings = [];

        // Validate content is not empty
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            errors.Add(new ValidationError
            {
                Code = "CONTENT_EMPTY",
                Message = "Content cannot be empty",
                Field = nameof(request.Content),
                Severity = ValidationSeverity.Error
            });
        }

        // Validate content length per tweet
        if (!string.IsNullOrWhiteSpace(request.Content))
        {
            IReadOnlyList<string> parts = ContentSplitter.Split(request.Content);

            foreach ((string? part, int index) in parts.Select((p, i) => (p, i)))
            {
                int effectiveLength = CalculateEffectiveLength(part);

                if (effectiveLength > XOptions.MaxTweetLength)
                {
                    errors.Add(new ValidationError
                    {
                        Code = "CONTENT_TOO_LONG",
                        Message = $"Tweet {index + 1} exceeds {XOptions.MaxTweetLength} characters (actual: {effectiveLength})",
                        Field = nameof(request.Content),
                        Severity = ValidationSeverity.Error
                    });
                }
                else if (effectiveLength > XOptions.MaxTweetLength * 0.9)
                {
                    warnings.Add(new ValidationError
                    {
                        Code = "CONTENT_NEAR_LIMIT",
                        Message = $"Tweet {index + 1} is near character limit ({effectiveLength}/{XOptions.MaxTweetLength})",
                        Field = nameof(request.Content),
                        Severity = ValidationSeverity.Warning
                    });
                }
            }

            // Warn about threads
            if (parts.Count > 5)
            {
                warnings.Add(new ValidationError
                {
                    Code = "LONG_THREAD",
                    Message = $"Thread contains {parts.Count} tweets. Consider breaking into smaller threads for better engagement.",
                    Field = nameof(request.Content),
                    Severity = ValidationSeverity.Warning
                });
            }
        }

        // Validate media
        if (request.MediaUrls?.Count > 0)
        {
            // Check media count
            int imageCount = 0;
            int videoCount = 0;

            foreach (string url in request.MediaUrls)
            {
                if (!File.Exists(url))
                {
                    errors.Add(new ValidationError
                    {
                        Code = "MEDIA_FILE_NOT_FOUND",
                        Message = $"Media file not found: {url}",
                        Field = nameof(request.MediaUrls),
                        Severity = ValidationSeverity.Error
                    });
                    continue;
                }

                string extension = Path.GetExtension(url).ToLowerInvariant();
                var fileInfo = new FileInfo(url);

                // Check supported formats
                if (_options.SupportedImageFormats.Contains(extension))
                {
                    imageCount++;

                    // Check image size
                    if (fileInfo.Length > _options.MaxImageSizeMB * 1024 * 1024)
                    {
                        errors.Add(new ValidationError
                        {
                            Code = "IMAGE_TOO_LARGE",
                            Message = $"Image exceeds {_options.MaxImageSizeMB}MB limit: {url}",
                            Field = nameof(request.MediaUrls),
                            Severity = ValidationSeverity.Error
                        });
                    }
                }
                else if (_options.SupportedVideoFormats.Contains(extension))
                {
                    videoCount++;

                    // Check video size
                    if (fileInfo.Length > _options.MaxVideoSizeMB * 1024 * 1024)
                    {
                        errors.Add(new ValidationError
                        {
                            Code = "VIDEO_TOO_LARGE",
                            Message = $"Video exceeds {_options.MaxVideoSizeMB}MB limit: {url}",
                            Field = nameof(request.MediaUrls),
                            Severity = ValidationSeverity.Error
                        });
                    }
                }
                else
                {
                    errors.Add(new ValidationError
                    {
                        Code = "UNSUPPORTED_MEDIA_FORMAT",
                        Message = $"Unsupported media format '{extension}' for file: {url}",
                        Field = nameof(request.MediaUrls),
                        Severity = ValidationSeverity.Error
                    });
                }
            }

            // Check media limits
            if (imageCount > XOptions.MaxMediaAttachments)
            {
                errors.Add(new ValidationError
                {
                    Code = "TOO_MANY_IMAGES",
                    Message = $"Maximum {XOptions.MaxMediaAttachments} images allowed per tweet",
                    Field = nameof(request.MediaUrls),
                    Severity = ValidationSeverity.Error
                });
            }

            if (videoCount > XOptions.MaxVideoAttachments)
            {
                errors.Add(new ValidationError
                {
                    Code = "TOO_MANY_VIDEOS",
                    Message = $"Maximum {XOptions.MaxVideoAttachments} video allowed per tweet",
                    Field = nameof(request.MediaUrls),
                    Severity = ValidationSeverity.Error
                });
            }

            if (imageCount > 0 && videoCount > 0)
            {
                warnings.Add(new ValidationError
                {
                    Code = "MIXED_MEDIA",
                    Message = "Mixing images and videos may not be supported. Video will be prioritized.",
                    Field = nameof(request.MediaUrls),
                    Severity = ValidationSeverity.Warning
                });
            }
        }

        // Check for potentially problematic content
        if (!string.IsNullOrWhiteSpace(request.Content))
        {
            string content = request.Content.ToLowerInvariant();

            // Check for excessive hashtags
            int hashtagCount = Regex.Count(request.Content, @"#\w+");
            if (hashtagCount > 5)
            {
                warnings.Add(new ValidationError
                {
                    Code = "TOO_MANY_HASHTAGS",
                    Message = $"Using {hashtagCount} hashtags may appear spammy. Consider using 2-3 relevant hashtags.",
                    Field = nameof(request.Content),
                    Severity = ValidationSeverity.Warning
                });
            }

            // Check for excessive mentions
            int mentionCount = Regex.Count(request.Content, @"@\w+");
            if (mentionCount > 5)
            {
                warnings.Add(new ValidationError
                {
                    Code = "TOO_MANY_MENTIONS",
                    Message = $"Using {mentionCount} mentions may appear spammy.",
                    Field = nameof(request.Content),
                    Severity = ValidationSeverity.Warning
                });
            }
        }

        if (errors.Count > 0)
        {
            return ValidationResult.Failure(errors);
        }

        var result = ValidationResult.Success();
        if (warnings.Count > 0)
        {
            // Use reflection to set warnings since Success() creates a valid result
            Type resultType = result.GetType();
            PropertyInfo? warningsProperty = resultType.GetProperty("Warnings");
            warningsProperty?.SetValue(result, warnings.AsReadOnly());
        }

        return await Task.FromResult(result).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ValidationResult> ValidateSelectorsAsync(object page)
    {
        List<ValidationError> errors = [];

        if (page is not IPage browserPage)
        {
            errors.Add(new ValidationError
            {
                Code = "INVALID_PAGE",
                Message = "Page object is not a valid browser page",
                Severity = ValidationSeverity.Error
            });
            return ValidationResult.Failure(errors);
        }

        // Essential selectors for X
        var requiredSelectors = new Dictionary<string, string>
        {
            ["Compose Box"] = "div[role='textbox'][contenteditable='true']",
            ["Post Button"] = "button[data-testid='tweetButton']",
            ["Account Menu"] = "[data-testid='AppTabBar_More_Menu']"
        };

        foreach ((string? name, string? selector) in requiredSelectors)
        {
            try
            {
                IElement? element = await browserPage.QuerySelectorAsync(selector).ConfigureAwait(false);
                if (element == null)
                {
                    errors.Add(new ValidationError
                    {
                        Code = "SELECTOR_NOT_FOUND",
                        Message = $"Required element '{name}' not found on page (selector: {selector})",
                        Field = name,
                        Severity = ValidationSeverity.Error
                    });
                }
            }
            catch (Exception ex)
            {
                errors.Add(new ValidationError
                {
                    Code = "SELECTOR_VALIDATION_ERROR",
                    Message = $"Error validating selector '{name}': {ex.Message}",
                    Field = name,
                    Severity = ValidationSeverity.Error
                });
            }
        }

        if (errors.Count > 0)
        {
            return ValidationResult.Failure(errors);
        }

        return ValidationResult.Success();
    }

    /// <inheritdoc />
    public async Task<string> GeneratePreviewAsync(CreatePostRequest request)
    {
        IReadOnlyList<string> parts = ContentSplitter.Split(request.Content);
        var html = new System.Text.StringBuilder();

        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html>");
        html.AppendLine("<head>");
        html.AppendLine("<style>");
        html.AppendLine(@"
            body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; max-width: 600px; margin: 20px auto; padding: 20px; }
            .tweet { border: 1px solid #e1e8ed; border-radius: 12px; padding: 16px; margin-bottom: 16px; background: white; }
            .thread-indicator { color: #1d9bf0; font-size: 13px; margin-bottom: 8px; }
            .content { font-size: 15px; line-height: 1.5; color: #0f1419; white-space: pre-wrap; }
            .media { margin-top: 12px; }
            .media img { max-width: 100%; border-radius: 12px; }
            .stats { color: #536471; font-size: 13px; margin-top: 12px; }
            .header { display: flex; align-items: center; margin-bottom: 12px; }
            .avatar { width: 48px; height: 48px; border-radius: 50%; background: #e1e8ed; margin-right: 12px; }
            .user-info { flex: 1; }
            .name { font-weight: 700; color: #0f1419; }
            .handle { color: #536471; font-size: 15px; }
            .warning { background: #ffad1f; color: #000; padding: 8px 12px; border-radius: 8px; margin: 12px 0; font-size: 13px; }
        ");
        html.AppendLine("</style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");

        html.AppendLine("<h2>X Post Preview</h2>");

        if (parts.Count > 1)
        {
            html.Append("<p><strong>Thread with ").Append(parts.Count).AppendLine(" tweets</strong></p>");
        }

        for (int i = 0; i < parts.Count; i++)
        {
            html.AppendLine("<div class='tweet'>");

            if (parts.Count > 1)
            {
                html.Append("<div class='thread-indicator'>🧵 Tweet ").Append(i + 1).Append(" of ").Append(parts.Count).AppendLine("</div>");
            }

            html.AppendLine("<div class='header'>");
            html.AppendLine("<div class='avatar'></div>");
            html.AppendLine("<div class='user-info'>");
            html.AppendLine("<div class='name'>Your Name</div>");
            html.AppendLine("<div class='handle'>@yourhandle</div>");
            html.AppendLine("</div>");
            html.AppendLine("</div>");

            html.AppendLine("<div class='content'>");
            html.AppendLine(System.Net.WebUtility.HtmlEncode(parts[i]).Replace("\n", "<br/>"));
            html.AppendLine("</div>");

            // Show media only on first tweet
            if (i == 0 && request.MediaUrls?.Count > 0)
            {
                html.AppendLine("<div class='media'>");
                foreach (string? url in request.MediaUrls.Take(4))
                {
                    if (File.Exists(url))
                    {
                        string extension = Path.GetExtension(url).ToLowerInvariant();
                if (_options.SupportedImageFormats.Contains(extension))
                {
                    html.Append("<img src='file://").Append(url).AppendLine("' alt='Media' />");
                }
                else
                {
                    html.Append("<p>📹 Video: ").Append(Path.GetFileName(url)).AppendLine("</p>");
                }
                    }
                }
                html.AppendLine("</div>");
            }

            html.AppendLine("<div class='stats'>💬 0 🔁 0 ❤️ 0 👁️ 0</div>");
            html.AppendLine("</div>");
        }

        // Add character count info
        int totalChars = parts.Sum(p => CalculateEffectiveLength(p));
        html.Append("<p><strong>Total character count: ").Append(totalChars).AppendLine("</strong></p>");

        if (request.MediaUrls?.Count > 0)
        {
            html.Append("<p>Media attachments: ").Append(request.MediaUrls.Count).AppendLine("</p>");
        }

        html.AppendLine("</body>");
        html.AppendLine("</html>");

        return await Task.FromResult(html.ToString()).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<SimulationResult> SimulatePostAsync(CreatePostRequest request)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Validate content
        ValidationResult validationResult = await ValidatePostAsync(request).ConfigureAwait(false);

        if (!validationResult.IsValid)
        {
            stopwatch.Stop();
            return SimulationResult.Failure(PlatformName, "CreatePost",
                validationResult.Errors.Select(e => e.Message));
        }

        // Simulate the posting process
        IReadOnlyList<string> parts = ContentSplitter.Split(request.Content);
        List<string> simulatedIds = [];

        // Generate simulated tweet IDs
        for (int i = 0; i < parts.Count; i++)
        {
            simulatedIds.Add($"sim_{Guid.NewGuid().ToString("N")[..16]}");
        }

        // Generate warnings
        var warnings = validationResult.Warnings.Select(w => w.Message).ToList();

        if (parts.Count > 1)
        {
            warnings.Add($"This will create a thread with {parts.Count} tweets");
        }

        stopwatch.Stop();

        // Generate preview HTML
        string previewHtml = await GeneratePreviewAsync(request).ConfigureAwait(false);

        return new SimulationResult
        {
            WouldSucceed = true,
            Platform = PlatformName,
            Action = "CreatePost",
            SimulatedPostId = simulatedIds.First(),
            SimulatedAt = DateTime.UtcNow,
            SimulatedDuration = stopwatch.Elapsed,
            PreviewHtml = previewHtml,
            Warnings = warnings.AsReadOnly(),
            Metadata = new Dictionary<string, object>
            {
                ["TweetCount"] = parts.Count,
                ["SimulatedIds"] = simulatedIds,
                ["TotalCharacters"] = parts.Sum(p => CalculateEffectiveLength(p)),
                ["MediaCount"] = request.MediaUrls?.Count ?? 0
            }
        };
    }

    /// <summary>
    /// Calculates the effective character length of content, treating URLs as 23 characters.
    /// </summary>
    private static int CalculateEffectiveLength(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return 0;
        }

        // X treats all URLs as 23 characters (https://t.co/XXXXXXXXXX)
        string urlPattern = @"https?://[^\s]+";
        MatchCollection urls = Regex.Matches(content, urlPattern);
        int urlPlaceholderLength = 23;
        int urlTotalLength = urls.Count * urlPlaceholderLength;

        // Remove URLs and calculate remaining content length
        string contentWithoutUrls = Regex.Replace(content, urlPattern, "");

        return urlTotalLength + contentWithoutUrls.Length;
    }
}
