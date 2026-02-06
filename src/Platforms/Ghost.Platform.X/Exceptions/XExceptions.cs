namespace Ghost.Platform.X.Exceptions;

/// <summary>
/// Base exception for all X platform errors.
/// </summary>
public abstract class XException : Exception
{
    /// <summary>
    /// Gets the error code for programmatic handling.
    /// </summary>
    public string ErrorCode { get; }

    /// <summary>
    /// Gets the timestamp when the error occurred.
    /// </summary>
    public DateTime Timestamp { get; }

    protected XException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
        Timestamp = DateTime.UtcNow;
    }

    protected XException(string errorCode, string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Exception thrown when authentication to X fails.
/// </summary>
public class XAuthenticationException : XException
{
    /// <summary>
    /// Gets the authentication step that failed.
    /// </summary>
    public string AuthStep { get; }

    /// <summary>
    /// Gets remediation steps for the user.
    /// </summary>
    public string Remediation { get; }

    public XAuthenticationException(string authStep, string message)
        : base("AUTH_FAILED", $"Authentication failed at step '{authStep}': {message}")
    {
        AuthStep = authStep;
        Remediation = "Please ensure you are logged into X.com and have valid authentication cookies. " +
                     "Navigate to X.com in a browser, log in, and save the storage state.";
    }

    public XAuthenticationException(string authStep, string message, Exception innerException)
        : base("AUTH_FAILED", $"Authentication failed at step '{authStep}': {message}", innerException)
    {
        AuthStep = authStep;
        Remediation = "Please ensure you are logged into X.com and have valid authentication cookies.";
    }
}

/// <summary>
/// Exception thrown when X rate limits are exceeded.
/// </summary>
public class XRateLimitException : XException
{
    /// <summary>
    /// Gets the time to wait before retrying.
    /// </summary>
    public TimeSpan RetryAfter { get; }

    /// <summary>
    /// Gets the number of remaining requests.
    /// </summary>
    public int? RemainingRequests { get; }

    public XRateLimitException(TimeSpan retryAfter, int? remainingRequests = null)
        : base("RATE_LIMITED",
               $"X rate limit exceeded. Retry after {retryAfter.TotalMinutes:F1} minutes.")
    {
        RetryAfter = retryAfter;
        RemainingRequests = remainingRequests;
    }

    /// <summary>
    /// Gets the remediation message.
    /// </summary>
    public string Remediation => $"Wait {RetryAfter.TotalMinutes:F1} minutes before retrying. " +
                                "Consider implementing exponential backoff.";
}

/// <summary>
/// Exception thrown when content validation fails.
/// </summary>
public class XValidationException : XException
{
    /// <summary>
    /// Gets the validation errors.
    /// </summary>
    public IReadOnlyList<ValidationError> ValidationErrors { get; }

    public XValidationException(IEnumerable<ValidationError> errors)
        : base("VALIDATION_FAILED",
               $"Content validation failed with {errors.Count()} error(s).")
    {
        ValidationErrors = errors.ToList().AsReadOnly();
    }

    /// <summary>
    /// Represents a single validation error.
    /// </summary>
    public class ValidationError
    {
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? Field { get; set; }
    }
}

/// <summary>
/// Exception thrown when browser automation fails.
/// </summary>
public class XBrowserException : XException
{
    /// <summary>
    /// Gets the URL where the error occurred.
    /// </summary>
    public string? Url { get; }

    /// <summary>
    /// Gets the selector that caused the error (if applicable).
    /// </summary>
    public string? Selector { get; }

    /// <summary>
    /// Gets the action being performed when the error occurred.
    /// </summary>
    public string Action { get; }

    public XBrowserException(string action, string message, string? url = null, string? selector = null)
        : base("BROWSER_ERROR", $"Browser automation failed during '{action}': {message}")
    {
        Action = action;
        Url = url;
        Selector = selector;
    }

    public XBrowserException(string action, string message, Exception innerException, string? url = null, string? selector = null)
        : base("BROWSER_ERROR", $"Browser automation failed during '{action}': {message}", innerException)
    {
        Action = action;
        Url = url;
        Selector = selector;
    }

    /// <summary>
    /// Gets the remediation message.
    /// </summary>
    public string Remediation
    {
        get
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("Browser automation failed. ");

            if (!string.IsNullOrEmpty(Selector))
            {
                sb.Append($"The selector '{Selector}' was not found. ");
                sb.Append("X may have changed their DOM structure or you may need to update selectors. ");
            }

            sb.Append("Try enabling stealth mode or waiting for the page to fully load.");
            return sb.ToString();
        }
    }
}

/// <summary>
/// Exception thrown when media upload fails.
/// </summary>
public class XMediaException : XException
{
    /// <summary>
    /// Gets the media file path.
    /// </summary>
    public string? MediaPath { get; }

    /// <summary>
    /// Gets the media type (image/video).
    /// </summary>
    public string? MediaType { get; }

    public XMediaException(string message, string? mediaPath = null, string? mediaType = null)
        : base("MEDIA_ERROR", message)
    {
        MediaPath = mediaPath;
        MediaType = mediaType;
    }

    /// <summary>
    /// Gets the remediation message.
    /// </summary>
    public string Remediation
    {
        get
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("Media upload failed. ");

            if (!string.IsNullOrEmpty(MediaPath))
            {
                sb.Append($"Check that '{MediaPath}' exists and is accessible. ");
            }

            sb.Append("Images must be under 5MB (.jpg, .png, .gif, .webp). ");
            sb.Append("Videos must be under 512MB (.mp4, .mov, .webm).");

            return sb.ToString();
        }
    }
}

/// <summary>
/// Exception thrown when thread composition fails.
/// </summary>
public class XThreadException : XException
{
    /// <summary>
    /// Gets the tweet number in the thread where the error occurred.
    /// </summary>
    public int TweetNumber { get; }

    /// <summary>
    /// Gets the total number of tweets in the thread.
    /// </summary>
    public int TotalTweets { get; }

    public XThreadException(int tweetNumber, int totalTweets, string message)
        : base("THREAD_ERROR",
               $"Thread composition failed at tweet {tweetNumber} of {totalTweets}: {message}")
    {
        TweetNumber = tweetNumber;
        TotalTweets = totalTweets;
    }

    /// <summary>
    /// Gets the remediation message.
    /// </summary>
    public string Remediation => $"Thread failed at tweet {TweetNumber} of {TotalTweets}. " +
                                "Previous tweets may have been posted successfully. " +
                                "Check your timeline to see which tweets were posted.";
}
