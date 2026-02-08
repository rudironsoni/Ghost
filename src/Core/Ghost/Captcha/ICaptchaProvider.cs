namespace Ghost.Captcha;

/// <summary>
/// Interface for CAPTCHA solving providers
/// </summary>
public interface ICaptchaProvider
{
    /// <summary>
    /// Name of the provider
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Solves a CAPTCHA challenge asynchronously
    /// </summary>
    /// <param name="challenge">The CAPTCHA challenge to solve</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The solution token or text</returns>
    Task<string> SolveAsync(ICaptchaChallenge challenge, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines if this provider can solve the given CAPTCHA type
    /// </summary>
    /// <param name="type">The CAPTCHA type</param>
    /// <returns>True if this provider can handle the CAPTCHA type</returns>
    bool CanSolve(CaptchaType type);

    /// <summary>
    /// Checks if the provider is available and healthy
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if provider is available</returns>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}
