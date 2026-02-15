namespace Ghost.Captcha;

/// <summary>
/// Default implementation of ICaptchaChallenge
/// </summary>
public sealed class CaptchaChallenge : ICaptchaChallenge
{
    public CaptchaType Type { get; init; }
    public string? SiteKey { get; init; }
    public string PageUrl { get; init; }
    public Dictionary<string, string> Parameters { get; init; }
    public string? ImageData { get; init; }

    public CaptchaChallenge(CaptchaType type, string pageUrl)
    {
        Type = type;
        PageUrl = pageUrl;
        Parameters = new Dictionary<string, string>();
    }
}
