namespace Ghost.Sdk.Middleware;

/// <summary>
/// Provides user agent rotation capabilities to avoid detection.
/// </summary>
public interface IUserAgentRotator
{
    /// <summary>
    /// Gets the next user agent in the rotation sequence.
    /// </summary>
    /// <returns>A user agent string.</returns>
    string GetNextUserAgent();

    /// <summary>
    /// Adds a user agent to the rotation pool.
    /// </summary>
    /// <param name="userAgent">The user agent string to add.</param>
    void AddUserAgent(string userAgent);

    /// <summary>
    /// Removes a user agent from the rotation pool.
    /// </summary>
    /// <param name="userAgent">The user agent string to remove.</param>
    void RemoveUserAgent(string userAgent);
}
