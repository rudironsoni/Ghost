namespace Ghost.Captcha;

/// <summary>
/// Represents a CAPTCHA challenge that needs to be solved
/// </summary>
public interface ICaptchaChallenge
{
    /// <summary>
    /// Type of CAPTCHA challenge
    /// </summary>
    public CaptchaType Type { get; }

    /// <summary>
    /// Site key for the CAPTCHA (e.g., reCAPTCHA site key)
    /// </summary>
    public string? SiteKey { get; }

    /// <summary>
    /// URL of the page containing the CAPTCHA
    /// </summary>
    public string PageUrl { get; }

    /// <summary>
    /// Additional parameters specific to the CAPTCHA type
    /// </summary>
    public Dictionary<string, string> Parameters { get; }

    /// <summary>
    /// Image data for image-based CAPTCHAs (base64 encoded)
    /// </summary>
    public string? ImageData { get; }
}
