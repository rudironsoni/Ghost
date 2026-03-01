using System.Collections.Concurrent;

namespace Ghost.Sdk.Middleware;

/// <summary>
/// Middleware that respects robots.txt rules from websites.
/// </summary>
public class RobotsMiddleware : IRobotsMiddleware
{
    private readonly HttpClient _httpClient;
    private readonly ConcurrentDictionary<string, RobotsTxt> _robotsCache = new();
    private readonly RobotsOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="RobotsMiddleware"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client for fetching robots.txt files.</param>
    /// <param name="options">Configuration options.</param>
    public RobotsMiddleware(HttpClient httpClient, RobotsOptions options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);

        _httpClient = httpClient;
        _options = options;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RobotsMiddleware"/> class with default options.
    /// </summary>
    /// <param name="httpClient">The HTTP client for fetching robots.txt files.</param>
    public RobotsMiddleware(HttpClient httpClient)
        : this(httpClient, new RobotsOptions())
    {
    }

    /// <inheritdoc/>
    public async Task<bool> CanFetchAsync(string url, string userAgent, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(userAgent);

        // Relative URLs cannot be checked - require absolute URL with http/https scheme
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
            return false;

        // Must be http or https scheme (not file:// etc.)
        if (!string.Equals(uri.Scheme, "http", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string baseUrl = $"{uri.Scheme}://{uri.Host}";

        // Try to get cached robots.txt
        if (!_robotsCache.TryGetValue(baseUrl, out RobotsTxt? robotsTxt))
        {
            await LoadRobotsTxtAsync(baseUrl, userAgent, ct).ConfigureAwait(false);
            _robotsCache.TryGetValue(baseUrl, out robotsTxt);
        }

        // If no robots.txt found or parsing failed, allow by default (or deny if configured)
        if (robotsTxt == null)
            return _options.AllowOnError;

        string path = uri.PathAndQuery;
        return robotsTxt.CanFetch(path, userAgent);
    }

    /// <inheritdoc/>
    public async Task LoadRobotsTxtAsync(string baseUrl, string userAgent, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(baseUrl);
        ArgumentNullException.ThrowIfNull(userAgent);

        string robotsUrl = $"{baseUrl.TrimEnd('/')}/robots.txt";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, robotsUrl);
            request.Headers.Add("User-Agent", userAgent);

            if (_options.Timeout.HasValue)
            {
                using var timeoutCts = new CancellationTokenSource(_options.Timeout.Value);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
                ct = linkedCts.Token;
            }

            HttpResponseMessage response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                string content = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                RobotsTxt robotsTxt = RobotsTxtParser.Parse(content);
                _robotsCache[baseUrl] = robotsTxt;
            }
            else
            {
                // No robots.txt or error - respect AllowOnError option
                var robotsTxt = new RobotsTxt();
                if (!_options.AllowOnError)
                {
                    // When AllowOnError is false, add a disallow all rule
                    var rules = new UserAgentRules();
                    rules.AddDisallow("/");
                    robotsTxt.AddRules("*", rules);
                }
                _robotsCache[baseUrl] = robotsTxt;
            }
        }
        catch (TaskCanceledException)
        {
            // Timeout or cancellation - allow all on error
            _robotsCache[baseUrl] = new RobotsTxt();
        }
        catch (HttpRequestException)
        {
            // Network error - allow all on error
            _robotsCache[baseUrl] = new RobotsTxt();
        }
        catch
        {
            // Any other error - allow all on error
            _robotsCache[baseUrl] = new RobotsTxt();
        }
    }
}
