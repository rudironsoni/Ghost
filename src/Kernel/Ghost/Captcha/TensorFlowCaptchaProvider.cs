using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Ghost.Captcha;

/// <summary>
/// CAPTCHA provider using self-hosted captcha-tensorflow model
/// Solves text-based image CAPTCHAs using CNN model
/// </summary>
public sealed class TensorFlowCaptchaProvider : ICaptchaProvider
{
    private readonly ILogger<TensorFlowCaptchaProvider> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _apiEndpoint;
    private readonly TimeSpan _timeout;

    public string Name => "TensorFlow";

    // LoggerMessage delegates for performance
    private static readonly Action<ILogger, Exception?> _logAttemptingSolve =
        LoggerMessage.Define(LogLevel.Information, new EventId(1, "AttemptingSolve"), "Attempting to solve text-based CAPTCHA using TensorFlow model");

    private static readonly Action<ILogger, double, Exception?> _logSolved =
        LoggerMessage.Define<double>(LogLevel.Information, new EventId(2, "Solved"), "TensorFlow solved CAPTCHA with confidence {Confidence:P}");

    private static readonly Action<ILogger, string, Exception?> _logCommunicationError =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(3, "CommunicationError"), "Failed to communicate with TensorFlow API at {Endpoint}");

    private static readonly Action<ILogger, Exception?> _logParseError =
        LoggerMessage.Define(LogLevel.Error, new EventId(4, "ParseError"), "Failed to parse TensorFlow API response");

    private static readonly Action<ILogger, string, Exception?> _logHealthCheckFailed =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(5, "HealthCheckFailed"), "TensorFlow API health check failed at {Endpoint}");

    public TensorFlowCaptchaProvider(
        ILogger<TensorFlowCaptchaProvider> logger,
        HttpClient httpClient,
        string apiEndpoint = "http://localhost:5000",
        TimeSpan? timeout = null)
    {
        _logger = logger;
        _httpClient = httpClient;
        _apiEndpoint = apiEndpoint;
        _timeout = timeout ?? TimeSpan.FromSeconds(30);
        _httpClient.Timeout = _timeout;
    }

    public bool CanSolve(CaptchaType type)
    {
        // TensorFlow model works best with simple text-based image CAPTCHAs
        return type == CaptchaType.TextImage;
    }

    public async Task<string> SolveAsync(ICaptchaChallenge challenge, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(challenge);

        if (!CanSolve(challenge.Type))
        {
            throw new NotSupportedException($"TensorFlow provider does not support {challenge.Type} CAPTCHA type");
        }

        if (string.IsNullOrEmpty(challenge.ImageData))
        {
            throw new ArgumentException("ImageData is required for TensorFlow CAPTCHA solving", nameof(challenge));
        }

        _logAttemptingSolve(_logger, null);

        try
        {
            // Send image to captcha-tensorflow API
            var request = new
            {
                image = challenge.ImageData // base64 encoded image
            };

            var content = new StringContent(
                JsonSerializer.Serialize(request),
                Encoding.UTF8,
                "application/json");

            HttpResponseMessage response = await _httpClient.PostAsync(
                $"{_apiEndpoint}/solve",
                content,
                cancellationToken).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            string responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            TensorFlowResponse? result = JsonSerializer.Deserialize<TensorFlowResponse>(responseBody);

            if (result?.Solution == null)
            {
                throw new InvalidOperationException("TensorFlow API returned null solution");
            }

            _logSolved(_logger, result.Confidence, null);

            return result.Solution;
        }
        catch (HttpRequestException ex)
        {
            _logCommunicationError(_logger, _apiEndpoint, ex);
            throw new InvalidOperationException($"TensorFlow API not available at {_apiEndpoint}", ex);
        }
        catch (JsonException ex)
        {
            _logParseError(_logger, ex);
            throw new InvalidOperationException("Invalid response from TensorFlow API", ex);
        }
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"{_apiEndpoint}/health", cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logHealthCheckFailed(_logger, _apiEndpoint, ex);
            return false;
        }
    }

    private sealed class TensorFlowResponse
    {
        public string? Solution { get; set; }
        public double Confidence { get; set; }
    }
}
