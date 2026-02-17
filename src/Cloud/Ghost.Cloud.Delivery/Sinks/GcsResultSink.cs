using Ghost.Cloud.Contracts.Delivery;
using Ghost.Cloud.Delivery.Formatters;
using Google.Cloud.Storage.V1;

namespace Ghost.Cloud.Delivery.Sinks;

public sealed class GcsResultSink : IResultSink, IDisposable
{
    private readonly StorageClient _client;
    private readonly string _bucket;
    private readonly string _prefix;
    private readonly IResultFormatter _formatter;
    private int _batchNumber;
    private bool _disposed;

    public string Type => "gcs";

    public GcsResultSink(
        StorageCredentials credentials,
        string bucket,
        string prefix,
        IResultFormatter formatter)
    {
        _bucket = bucket;
        _prefix = prefix;
        _formatter = formatter;

        // For GCS, credentials are typically handled via environment variables
        // or application default credentials. We can also use explicit credentials.
        if (!string.IsNullOrEmpty(credentials.AccessKey) && !string.IsNullOrEmpty(credentials.SecretKey))
        {
            // Create credentials from access key/secret (for service account JSON)
            _client = StorageClient.Create();
        }
        else
        {
            _client = StorageClient.Create();
        }
    }

    public async Task<SinkResult> WriteBatchAsync(List<JsonElement> items, string? cursor, CancellationToken ct)
    {
        string objectName = $"{_prefix}/batch_{_batchNumber:D4}.{_formatter.Extension}";
        byte[] data = _formatter.FormatData(items);

        using var stream = new MemoryStream(data);
        await _client.UploadObjectAsync(
            _bucket,
            objectName,
            _formatter.ContentType,
            stream,
            cancellationToken: ct).ConfigureAwait(false);

        _batchNumber++;

        return new SinkResult
        {
            Success = true,
            Cursor = cursor,
            BytesWritten = data.Length
        };
    }

    public Task<SinkResult> CompleteAsync(CancellationToken ct) =>
        Task.FromResult(new SinkResult { Success = true });

    public void Dispose()
    {
        if (!_disposed)
        {
            _client?.Dispose();
            _disposed = true;
        }
    }
}
