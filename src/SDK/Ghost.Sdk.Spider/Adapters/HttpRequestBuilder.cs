using System.Net.Http.Headers;
using System.Text;
using System.Web;
using Ghost.Sdk.Spider.Adapters.Contracts;

namespace Ghost.Sdk.Spider.Adapters;

/// <summary>
/// Builder for constructing HTTP requests with support for various HTTP methods,
/// headers, query parameters, and request bodies.
/// </summary>
/// <remarks>
/// This class provides a fluent API for building complex HTTP requests with
/// support for GET, POST, PUT, DELETE, PATCH methods, custom headers, cookies,
/// query parameters, and different content types.
/// </remarks>
internal class HttpRequestBuilder
{
    private readonly Request _request;
    private readonly StaticHtmlAdapterOptions _options;
    private readonly Dictionary<string, string> _queryParameters = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpRequestBuilder"/> class.
    /// </summary>
    /// <param name="request">The content request to build from.</param>
    /// <param name="options">The adapter options containing default headers and configuration.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> or <paramref name="options"/> is null.</exception>
    public HttpRequestBuilder(Request request, StaticHtmlAdapterOptions options)
    {
        _request = request ?? throw new ArgumentNullException(nameof(request));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Builds an <see cref="HttpRequestMessage"/> from the configured request and options.
    /// </summary>
    /// <returns>A fully configured <see cref="HttpRequestMessage"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the request URL is invalid.</exception>
    public HttpRequestMessage Build()
    {
        var uri = BuildUri();
        var method = GetHttpMethod();

        var httpRequest = new HttpRequestMessage(method, uri);

        ConfigureHeaders(httpRequest);
        ConfigureCookies(httpRequest);
        ConfigureContent(httpRequest);

        return httpRequest;
    }

    /// <summary>
    /// Builds the complete URI including query parameters.
    /// </summary>
    /// <returns>The complete URI for the request.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the URL is invalid.</exception>
    private Uri BuildUri()
    {
        if (!Uri.TryCreate(_request.Url, UriKind.Absolute, out var baseUri))
        {
            throw new InvalidOperationException($"Invalid URL: {_request.Url}");
        }

        // Extract existing query parameters from the URL
        if (!string.IsNullOrEmpty(baseUri.Query))
        {
            var existingParams = HttpUtility.ParseQueryString(baseUri.Query);
            foreach (string? key in existingParams.Keys)
            {
                if (key != null)
                {
                    _queryParameters.TryAdd(key, existingParams[key] ?? string.Empty);
                }
            }
        }

        // Extract query parameters from metadata if provided
        if (_request.Metadata.TryGetValue("QueryParameters", out var queryParamsObj))
        {
            if (queryParamsObj is Dictionary<string, string> queryParams)
            {
                foreach (var (key, value) in queryParams)
                {
                    _queryParameters[key] = value;
                }
            }
        }

        // Build final URI with query string
        if (_queryParameters.Count > 0)
        {
            var uriBuilder = new UriBuilder(baseUri);
            var queryString = HttpUtility.ParseQueryString(uriBuilder.Query);

            foreach (var (key, value) in _queryParameters)
            {
                queryString[key] = value;
            }

            uriBuilder.Query = queryString.ToString();
            return uriBuilder.Uri;
        }

        return baseUri;
    }

    /// <summary>
    /// Gets the HTTP method from the request.
    /// </summary>
    /// <returns>The <see cref="HttpMethod"/> for the request.</returns>
    private HttpMethod GetHttpMethod()
    {
        return _request.Method.ToUpperInvariant() switch
        {
            "GET" => HttpMethod.Get,
            "POST" => HttpMethod.Post,
            "PUT" => HttpMethod.Put,
            "DELETE" => HttpMethod.Delete,
            "PATCH" => HttpMethod.Patch,
            "HEAD" => HttpMethod.Head,
            "OPTIONS" => HttpMethod.Options,
            "TRACE" => HttpMethod.Trace,
            _ => new HttpMethod(_request.Method)
        };
    }

    /// <summary>
    /// Configures HTTP headers for the request.
    /// </summary>
    /// <param name="httpRequest">The HTTP request message to configure.</param>
    private void ConfigureHeaders(HttpRequestMessage httpRequest)
    {
        // Add default headers from options
        foreach (var (name, value) in _options.CustomHeaders)
        {
            TryAddHeader(httpRequest, name, value);
        }

        // Add User-Agent
        if (!string.IsNullOrWhiteSpace(_options.UserAgent))
        {
            httpRequest.Headers.UserAgent.TryParseAdd(_options.UserAgent);
        }

        // Add Accept header
        if (!string.IsNullOrWhiteSpace(_options.AcceptHeader))
        {
            httpRequest.Headers.Accept.TryParseAdd(_options.AcceptHeader);
        }

        // Add Accept-Language header
        if (!string.IsNullOrWhiteSpace(_options.AcceptLanguage))
        {
            TryAddHeader(httpRequest, "Accept-Language", _options.AcceptLanguage);
        }

        // Add Accept-Encoding header
        if (!string.IsNullOrWhiteSpace(_options.AcceptEncoding))
        {
            TryAddHeader(httpRequest, "Accept-Encoding", _options.AcceptEncoding);
        }

        // Add request-specific headers (these override defaults)
        foreach (var (name, value) in _request.Headers)
        {
            TryAddHeader(httpRequest, name, value);
        }

        // Add referer if provided in metadata
        if (_request.Metadata.TryGetValue("Referer", out var referer) && referer is string refererStr)
        {
            httpRequest.Headers.Referrer = new Uri(refererStr, UriKind.RelativeOrAbsolute);
        }
    }

    /// <summary>
    /// Attempts to add a header to the request.
    /// </summary>
    /// <param name="httpRequest">The HTTP request message.</param>
    /// <param name="name">The header name.</param>
    /// <param name="value">The header value.</param>
    private static void TryAddHeader(HttpRequestMessage httpRequest, string name, string value)
    {
        // Remove existing header if present
        httpRequest.Headers.Remove(name);

        // Try to add to request headers first
        if (!httpRequest.Headers.TryAddWithoutValidation(name, value) && httpRequest.Content != null)
        {
            // If that fails and we have content, try adding to content headers
            httpRequest.Content.Headers.TryAddWithoutValidation(name, value);
        }
    }

    /// <summary>
    /// Configures cookies for the request.
    /// </summary>
    /// <param name="httpRequest">The HTTP request message to configure.</param>
    private void ConfigureCookies(HttpRequestMessage httpRequest)
    {
        var cookies = new List<string>();

        // Add cookies from options
        foreach (var (name, value) in _options.Cookies)
        {
            cookies.Add($"{name}={value}");
        }

        // Add cookies from request metadata
        if (_request.Metadata.TryGetValue("Cookies", out var requestCookiesObj))
        {
            if (requestCookiesObj is Dictionary<string, string> requestCookies)
            {
                foreach (var (name, value) in requestCookies)
                {
                    cookies.Add($"{name}={value}");
                }
            }
        }

        // Set Cookie header if we have cookies
        if (cookies.Count > 0)
        {
            httpRequest.Headers.Add("Cookie", string.Join("; ", cookies));
        }
    }

    /// <summary>
    /// Configures the request content for POST/PUT/PATCH requests.
    /// </summary>
    /// <param name="httpRequest">The HTTP request message to configure.</param>
    private void ConfigureContent(HttpRequestMessage httpRequest)
    {
        // Only add content for methods that support it
        if (httpRequest.Method == HttpMethod.Get ||
            httpRequest.Method == HttpMethod.Head ||
            httpRequest.Method == HttpMethod.Options ||
            httpRequest.Method == HttpMethod.Trace)
        {
            return;
        }

        // If no body is provided, return
        if (string.IsNullOrEmpty(_request.Body))
        {
            return;
        }

        // Check if form data is provided in metadata
        if (_request.Metadata.TryGetValue("FormData", out var formDataObj))
        {
            if (formDataObj is Dictionary<string, string> formData)
            {
                ConfigureFormContent(httpRequest, formData);
                return;
            }
        }

        // Check content type from request headers or metadata
        var contentType = GetContentType();

        // Create appropriate content based on content type
        if (contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
        {
            httpRequest.Content = new StringContent(_request.Body, Encoding.UTF8, "application/json");
        }
        else if (contentType.StartsWith("application/xml", StringComparison.OrdinalIgnoreCase) ||
                 contentType.StartsWith("text/xml", StringComparison.OrdinalIgnoreCase))
        {
            httpRequest.Content = new StringContent(_request.Body, Encoding.UTF8, contentType);
        }
        else if (contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
        {
            httpRequest.Content = new StringContent(_request.Body, Encoding.UTF8, contentType);
        }
        else
        {
            // Default to plain text
            httpRequest.Content = new StringContent(_request.Body, Encoding.UTF8, "text/plain");
        }
    }

    /// <summary>
    /// Configures form-urlencoded content for the request.
    /// </summary>
    /// <param name="httpRequest">The HTTP request message.</param>
    /// <param name="formData">The form data dictionary.</param>
    private static void ConfigureFormContent(HttpRequestMessage httpRequest, Dictionary<string, string> formData)
    {
        httpRequest.Content = new FormUrlEncodedContent(formData);
    }

    /// <summary>
    /// Gets the content type from request headers or metadata.
    /// </summary>
    /// <returns>The content type string.</returns>
    private string GetContentType()
    {
        // Check request headers first
        if (_request.Headers.TryGetValue("Content-Type", out var contentType))
        {
            return contentType;
        }

        // Check metadata
        if (_request.Metadata.TryGetValue("ContentType", out var contentTypeObj))
        {
            if (contentTypeObj is string contentTypeStr)
            {
                return contentTypeStr;
            }
        }

        // Default to JSON for POST/PUT/PATCH
        return "application/json";
    }
}
