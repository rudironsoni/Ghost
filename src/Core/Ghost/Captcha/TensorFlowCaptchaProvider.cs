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

        _logger.LogInformation("Attempting to solve text-based CAPTCHA using TensorFlow model");

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

            var response = await _httpClient.PostAsync(
                $"{_apiEndpoint}/solve",
                content,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<TensorFlowResponse>(responseBody);

            if (result?.Solution == null)
            {
                throw new InvalidOperationException("TensorFlow API returned null solution");
            }

            _logger.LogInformation(
                "TensorFlow solved CAPTCHA with confidence {Confidence:P}",
                result.Confidence);

            return result.Solution;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to communicate with TensorFlow API at {Endpoint}", _apiEndpoint);
            throw new InvalidOperationException($"TensorFlow API not available at {_apiEndpoint}", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse TensorFlow API response");
            throw new InvalidOperationException("Invalid response from TensorFlow API", ex);
        }
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_apiEndpoint}/health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "TensorFlow API health check failed at {Endpoint}", _apiEndpoint);
            return false;
        }
    }

    private sealed class TensorFlowResponse
    {
        public string? Solution { get; set; }
        public double Confidence { get; set; }
    }
}
