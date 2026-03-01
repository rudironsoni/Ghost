namespace Ghost.Sdk.Middleware;

/// <summary>
/// Represents a simplified HTTP request for middleware processing.
/// </summary>
public class Request
{
    /// <summary>
    /// Gets or sets the URL to request.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the HTTP method (GET, POST, etc.).
    /// </summary>
    public string Method { get; set; } = "GET";
}

/// <summary>
/// Represents a simplified HTTP response for middleware processing.
/// </summary>
public class Response
{
    /// <summary>
    /// Gets or sets the HTTP status code.
    /// </summary>
    public int StatusCode { get; set; }

    /// <summary>
    /// Gets or sets the response headers.
    /// </summary>
    public Dictionary<string, string> Headers { get; set; } = [];
}

/// <summary>
/// Interface for middleware that handles HTTP redirects automatically.
/// </summary>
public interface IRedirectMiddleware
{
    /// <summary>
    /// Handles HTTP redirects for a request by following redirect responses.
    /// </summary>
    /// <param name="request">The initial HTTP request to execute.</param>
    /// <param name="execute">A function that executes the actual HTTP request.</param>
    /// <param name="ct">Cancellation token to observe.</param>
    /// <returns>The final non-redirect response.</returns>
    public Task<Response> HandleRedirectsAsync(Request request, Func<Request, Task<Response>> execute, CancellationToken ct);
}

/// <summary>
/// Middleware that automatically follows HTTP redirects with configurable limits.
/// </summary>
/// <remarks>
/// This middleware handles standard HTTP redirect status codes (301, 302, 303, 307, 308)
/// by following the Location header up to a maximum number of redirects. It properly
/// handles relative URLs, method changes, and cross-scheme redirect policies.
/// </remarks>
public class RedirectMiddleware : IRedirectMiddleware
{
    private readonly RedirectOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="RedirectMiddleware"/> class.
    /// </summary>
    /// <param name="options">Configuration options for redirect handling.</param>
    /// <exception cref="ArgumentNullException">Thrown when options is null.</exception>
    public RedirectMiddleware(RedirectOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <summary>
    /// Handles HTTP redirects for a request by following redirect responses until a non-redirect
    /// response is received or the maximum redirect limit is reached.
    /// </summary>
    /// <param name="request">The initial HTTP request to execute.</param>
    /// <param name="execute">
    /// A function that executes the actual HTTP request and returns a response.
    /// This will be called multiple times if redirects are encountered.
    /// </param>
    /// <param name="ct">Cancellation token to observe.</param>
    /// <returns>The final non-redirect response, or the last redirect response if limits are hit.</returns>
    /// <remarks>
    /// The middleware will:
    /// <list type="bullet">
    /// <item>Follow 301, 302, 303, 307, and 308 status codes</item>
    /// <item>Resolve relative URLs in Location headers</item>
    /// <item>Change POST to GET for 301/302/303 redirects</item>
    /// <item>Preserve the method for 307/308 redirects</item>
    /// <item>Stop at max redirects or missing Location header</item>
    /// </list>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the maximum number of redirects is exceeded.
    /// </exception>
    public async Task<Response> HandleRedirectsAsync(Request request, Func<Request, Task<Response>> execute, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(execute);

        int redirectCount = 0;
        Request currentRequest = request;

        while (redirectCount < _options.MaxRedirects)
        {
            Response response = await execute(currentRequest).ConfigureAwait(false);

            if (!IsRedirectStatusCode(response.StatusCode))
            {
                return response;
            }

            string? location = response.Headers.GetValueOrDefault("Location");
            if (string.IsNullOrEmpty(location))
            {
                return response; // No location header, return redirect response
            }

            // Resolve relative URLs
            string redirectUrl = ResolveUrl(currentRequest.Url, location);

            // Check if we should follow this redirect
            if (!ShouldFollowRedirect(currentRequest.Url, redirectUrl))
            {
                return response;
            }

            currentRequest = new Request
            {
                Url = redirectUrl,
                Method = GetRedirectMethod(response.StatusCode, currentRequest.Method)
            };
            redirectCount++;
        }

        throw new InvalidOperationException($"Maximum redirects ({_options.MaxRedirects}) exceeded");
    }

    /// <summary>
    /// Determines if the HTTP status code represents a redirect.
    /// </summary>
    /// <param name="statusCode">The HTTP status code to check.</param>
    /// <returns>True if the status code is a redirect (301, 302, 303, 307, 308); otherwise, false.</returns>
    private static bool IsRedirectStatusCode(int statusCode) => statusCode is 301 or 302 or 303 or 307 or 308;

    /// <summary>
    /// Determines if a redirect should be followed based on cross-scheme policy.
    /// </summary>
    /// <param name="originalUrl">The original request URL.</param>
    /// <param name="redirectUrl">The redirect target URL.</param>
    /// <returns>True if the redirect should be followed; otherwise, false.</returns>
    /// <remarks>
    /// If AllowCrossScheme is false, redirects that change the URL scheme
    /// (e.g., https to http) will not be followed.
    /// </remarks>
    private bool ShouldFollowRedirect(string originalUrl, string redirectUrl)
    {
        // Don't follow redirects to different schemes if not allowed
        if (!_options.AllowCrossScheme && Uri.TryCreate(originalUrl, UriKind.Absolute, out Uri? originalUri)
            && Uri.TryCreate(redirectUrl, UriKind.Absolute, out Uri? redirectUri))
        {
            if (!string.Equals(originalUri.Scheme, redirectUri.Scheme, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Resolves a potentially relative redirect URL against a base URL.
    /// </summary>
    /// <param name="baseUrl">The original request URL to use as the base.</param>
    /// <param name="location">The Location header value (may be relative or absolute).</param>
    /// <returns>An absolute URL for the redirect target.</returns>
    /// <remarks>
    /// If the location is already an absolute URL, it is returned unchanged.
    /// Otherwise, it is resolved relative to the base URL.
    /// </remarks>
    private static string ResolveUrl(string baseUrl, string location)
    {
        // Check if location is a true absolute URL (has scheme like http:// or https://)
        // Uri.TryCreate with UriKind.Absolute returns true for "/path" on .NET,
        // so we need to check for scheme explicitly
        if (Uri.TryCreate(location, UriKind.Absolute, out Uri? locationUri) && !string.IsNullOrEmpty(locationUri.Scheme))
        {
            // Only treat as absolute if it has a scheme (http/https/etc)
            if (locationUri.Scheme.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                return location;
            }
        }

        return new Uri(new Uri(baseUrl), location).ToString();
    }

    /// <summary>
    /// Determines the HTTP method to use for the redirect request based on the status code.
    /// </summary>
    /// <param name="statusCode">The redirect status code.</param>
    /// <param name="originalMethod">The original request's HTTP method.</param>
    /// <returns>The HTTP method to use for the redirect request.</returns>
    /// <remarks>
    /// Per HTTP specification:
    /// <list type="bullet">
    /// <item>301/302: POST becomes GET, others unchanged</item>
    /// <item>303: Always GET</item>
    /// <item>307/308: Preserve original method</item>
    /// </list>
    /// </remarks>
    private static string GetRedirectMethod(int statusCode, string originalMethod) => statusCode switch
    {
        301 or 302 => originalMethod == "POST" ? "GET" : originalMethod,
        303 => "GET",
        _ => originalMethod
    };
}
