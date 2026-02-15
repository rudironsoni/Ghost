namespace Ghost.Sdk.Middleware;

/// <summary>
/// Implements round-robin user agent rotation with thread-safe operations.
/// </summary>
public class UserAgentRotator : IUserAgentRotator
{
    private readonly List<string> _userAgents = [];
    private int _currentIndex;
    private readonly object _lock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="UserAgentRotator"/> class with default user agents.
    /// </summary>
    public UserAgentRotator()
    {
        // Default user agents - common browsers on different platforms
        _userAgents.AddRange([
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 14_7_1) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.1 Safari/605.1.15",
            "Mozilla/5.0 (iPhone; CPU iPhone OS 18_1 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.1 Mobile/15E148 Safari/604.1",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36 Edg/131.0.0.0"
        ]);
    }

    /// <summary>
    /// Gets the next user agent in the rotation sequence using round-robin.
    /// </summary>
    /// <returns>A user agent string, or default fallback if pool is empty.</returns>
    public string GetNextUserAgent()
    {
        lock (_lock)
        {
            if (_userAgents.Count == 0)
            {
                return "GhostSpider/1.0";
            }

            string agent = _userAgents[_currentIndex];
            _currentIndex = (_currentIndex + 1) % _userAgents.Count;
            return agent;
        }
    }

    /// <summary>
    /// Adds a user agent to the rotation pool if not already present.
    /// </summary>
    /// <param name="userAgent">The user agent string to add.</param>
    public void AddUserAgent(string userAgent)
    {
        lock (_lock)
        {
            if (!_userAgents.Contains(userAgent))
            {
                _userAgents.Add(userAgent);
            }
        }
    }

    /// <summary>
    /// Removes a user agent from the rotation pool and adjusts the index if needed.
    /// </summary>
    /// <param name="userAgent">The user agent string to remove.</param>
    public void RemoveUserAgent(string userAgent)
    {
        lock (_lock)
        {
            _userAgents.Remove(userAgent);
            if (_currentIndex >= _userAgents.Count && _userAgents.Count > 0)
            {
                _currentIndex = 0;
            }
        }
    }
}
