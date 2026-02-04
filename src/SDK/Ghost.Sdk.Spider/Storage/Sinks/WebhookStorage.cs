using Ghost.Sdk.Spider.Adapters.Contracts;
using Ghost.Sdk.Spider.Storage.Contracts;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text;

namespace Ghost.Sdk.Spider.Storage.Sinks;

/// <summary>
/// Storage implementation that sends data to a webhook endpoint.
/// </summary>
/// <remarks>
/// This storage posts extracted data as JSON to a configured webhook URL,
/// useful for integrating with external systems or triggering downstream processes.
/// </remarks>
public class WebhookStorage : IStorage
{
    private readonly HttpClient _httpClient;
    private readonly string _webhookUrl;
    private readonly ILogger<WebhookStorage>? _logger;
    private readonly JsonSerializerSettings _jsonSettings;

    /// <inheritdoc/>
    public string Name => "Webhook";

    /// <inheritdoc/>
    public bool IsAvailable => !string.IsNullOrWhiteSpace(_webhookUrl);

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookStorage"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client for sending requests.</param>
    /// <param name="webhookUrl">The webhook URL to POST data to.</param>
    /// <param name="logger">Optional logger for diagnostic information.</param>
    public WebhookStorage(HttpClient httpClient, string webhookUrl, ILogger<WebhookStorage>? logger = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _webhookUrl = webhookUrl ?? throw new ArgumentNullException(nameof(webhookUrl));
        _logger = logger;

        _jsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Ignore,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        };
    }

    /// <inheritdoc/>
    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Webhook storage initialized for URL: {WebhookUrl}", _webhookUrl);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task<StorageResult> StoreAsync<T>(
        T item,
        StorageContext context,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTimeOffset.UtcNow;

        try
        {
            var payload = new
            {
                spider = context.SpiderName,
                source = context.SourceUrl,
                timestamp = context.Timestamp,
                data = item,
                metadata = context.Metadata,
                tags = context.Tags
            };

            var json = JsonConvert.SerializeObject(payload, _jsonSettings);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(_webhookUrl, content, cancellationToken).ConfigureAwait(false);
            var duration = DateTimeOffset.UtcNow - startTime;

            if (response.IsSuccessStatusCode)
            {
                _logger?.LogDebug("Successfully posted item to webhook: {StatusCode}", response.StatusCode);
                return StorageResult.CreateSuccess(1, duration);
            }
            else
            {
                var error = $"Webhook returned {response.StatusCode}: {response.ReasonPhrase}";
                _logger?.LogWarning("Webhook post failed: {Error}", error);
                return StorageResult.CreateFailure(error, null, duration);
            }
        }
        catch (Exception ex)
        {
            var duration = DateTimeOffset.UtcNow - startTime;
            _logger?.LogError(ex, "Failed to post item to webhook");
            return StorageResult.CreateFailure($"Webhook error: {ex.Message}", ex, duration);
        }
    }

    /// <inheritdoc/>
    public async Task<StorageResult> StoreBatchAsync<T>(
        IEnumerable<T> items,
        StorageContext context,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTimeOffset.UtcNow;
        var itemList = items.ToList();

        try
        {
            var payload = new
            {
                spider = context.SpiderName,
                source = context.SourceUrl,
                timestamp = context.Timestamp,
                batchId = context.BatchId,
                count = itemList.Count,
                data = itemList,
                metadata = context.Metadata,
                tags = context.Tags
            };

            var json = JsonConvert.SerializeObject(payload, _jsonSettings);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(_webhookUrl, content, cancellationToken).ConfigureAwait(false);
            var duration = DateTimeOffset.UtcNow - startTime;

            if (response.IsSuccessStatusCode)
            {
                _logger?.LogDebug("Successfully posted batch to webhook: {Count} items", itemList.Count);
                return StorageResult.CreateSuccess(itemList.Count, duration);
            }
            else
            {
                var error = $"Webhook returned {response.StatusCode}: {response.ReasonPhrase}";
                _logger?.LogWarning("Webhook batch post failed: {Error}", error);
                return StorageResult.CreateFailure(error, null, duration);
            }
        }
        catch (Exception ex)
        {
            var duration = DateTimeOffset.UtcNow - startTime;
            _logger?.LogError(ex, "Failed to post batch to webhook");
            return StorageResult.CreateFailure($"Webhook error: {ex.Message}", ex, duration);
        }
    }

    /// <inheritdoc/>
    public Task FlushAsync(CancellationToken cancellationToken = default)
    {
        // Webhook posts are immediate
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task CloseAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Webhook storage closed");
        return Task.CompletedTask;
    }
}
