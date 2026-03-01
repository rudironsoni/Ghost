using Microsoft.Playwright;

namespace Ghost.Sdk.Middleware;

/// <summary>
/// Interface for managing HTTP cookies across requests.
/// </summary>
public interface ICookieMiddleware
{
    /// <summary>
    /// Retrieves all cookies for the specified domain.
    /// </summary>
    /// <param name="domain">The domain to retrieve cookies for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A collection of cookies for the domain.</returns>
    public Task<IReadOnlyList<Cookie>> GetCookiesAsync(string domain, CancellationToken ct = default);

    /// <summary>
    /// Sets a cookie for the specified domain.
    /// </summary>
    /// <param name="domain">The domain to set the cookie for.</param>
    /// <param name="cookie">The cookie to set.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task SetCookieAsync(string domain, Cookie cookie, CancellationToken ct = default);

    /// <summary>
    /// Loads cookies from a JSON file.
    /// </summary>
    /// <param name="filePath">The path to the JSON file containing cookies.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task LoadCookiesAsync(string filePath, CancellationToken ct = default);

    /// <summary>
    /// Saves all cookies to a JSON file.
    /// </summary>
    /// <param name="filePath">The path to save the JSON file to.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task SaveCookiesAsync(string filePath, CancellationToken ct = default);

    /// <summary>
    /// Clears all cookies for the specified domain.
    /// </summary>
    /// <param name="domain">The domain to clear cookies for.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task ClearCookiesAsync(string domain, CancellationToken ct = default);

    /// <summary>
    /// Clears all cookies.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    public Task ClearAllCookiesAsync(CancellationToken ct = default);
}
