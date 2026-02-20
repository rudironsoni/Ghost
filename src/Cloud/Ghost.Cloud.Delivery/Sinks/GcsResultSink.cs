using Ghost.Cloud.Contracts.Delivery;
using Ghost.Cloud.Delivery.Formatters;
using Google.Cloud.Storage.V1;
using StorageObject = Google.Apis.Storage.v1.Data.Object;

namespace Ghost.Cloud.Delivery.Sinks;

public sealed class GcsResultSink : IResultSink, IDisposable
{
    private readonly StorageClient _client;
    private readonly string _bucket;
    private readonly string _prefix;
    private readonly IResultFormatter _formatter;
    private readonly SinkWriteTracker _writeTracker = new();
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
        byte[] data = _formatter.FormatData(items);
        SinkWritePlan writePlan = SinkWritePlanner.Create(_prefix, _formatter.Extension, cursor, data);

        if (!_writeTracker.TryStart(writePlan))
        {
            return new SinkResult
            {
                Success = true,
                Cursor = cursor,
                BytesWritten = 0
            };
        }

        using var stream = new MemoryStream(data);
        var storageObject = new StorageObject
        {
            Bucket = _bucket,
            Name = writePlan.ObjectName,
            ContentType = _formatter.ContentType,
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ghost-integrity-sha256"] = writePlan.IntegritySha256,
                ["ghost-idempotency-key"] = writePlan.IdempotencyKey
            }
        };
        try
        {
            await _client.UploadObjectAsync(
                storageObject,
                stream,
                options: null,
                cancellationToken: ct).ConfigureAwait(false);
        }
        catch
        {
            _writeTracker.MarkFailed(writePlan);
            throw;
        }

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
