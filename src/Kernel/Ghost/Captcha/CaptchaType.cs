namespace Ghost.Captcha;

/// <summary>
/// Types of CAPTCHA challenges that can be encountered
/// </summary>
public enum CaptchaType
{
    /// <summary>
    /// Google reCAPTCHA v2 (checkbox "I'm not a robot")
    /// </summary>
    ReCaptchaV2,

    /// <summary>
    /// Google reCAPTCHA v3 (invisible, score-based)
    /// </summary>
    ReCaptchaV3,

    /// <summary>
    /// hCaptcha challenge
    /// </summary>
    HCaptcha,

    /// <summary>
    /// Arkose Labs FunCaptcha
    /// </summary>
    FunCaptcha,

    /// <summary>
    /// GeeTest CAPTCHA
    /// </summary>
    GeeTest,

    /// <summary>
    /// Cloudflare Turnstile
    /// </summary>
    Turnstile,

    /// <summary>
    /// Simple text-based image CAPTCHA
    /// </summary>
    TextImage,

    /// <summary>
    /// Unknown or unsupported CAPTCHA type
    /// </summary>
    Unknown
}
