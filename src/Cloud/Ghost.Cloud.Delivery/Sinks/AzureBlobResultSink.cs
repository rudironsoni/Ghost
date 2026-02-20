using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Ghost.Cloud.Contracts.Delivery;
using Ghost.Cloud.Delivery.Formatters;

namespace Ghost.Cloud.Delivery.Sinks;

public sealed class AzureBlobResultSink : IResultSink, IDisposable
{
    private readonly BlobContainerClient _containerClient;
    private readonly string _prefix;
    private readonly IResultFormatter _formatter;
    private readonly SinkWriteTracker _writeTracker = new();
    private bool _disposed;

    public string Type => "azure";

    public AzureBlobResultSink(
        StorageCredentials credentials,
        string containerName,
        string prefix,
        IResultFormatter formatter)
    {
        _prefix = prefix;
        _formatter = formatter;

        // Azure credentials can be connection string, account key, or token-based
        if (!string.IsNullOrEmpty(credentials.AccessKey) && !string.IsNullOrEmpty(credentials.StorageAccount))
        {
            // Use account name and key
            string accountName = credentials.StorageAccount;
            string connectionString = $"DefaultEndpointsProtocol=https;AccountName={accountName};AccountKey={credentials.AccessKey};EndpointSuffix=core.windows.net";
            _containerClient = new BlobContainerClient(connectionString, containerName);
        }
        else if (!string.IsNullOrEmpty(credentials.StorageAccount))
        {
            // Use connection string with just account name (will use default credentials)
            string accountName = credentials.StorageAccount;
            string connectionString = $"DefaultEndpointsProtocol=https;AccountName={accountName};EndpointSuffix=core.windows.net";
            _containerClient = new BlobContainerClient(connectionString, containerName);
        }
        else
        {
            // Fallback to connection string from environment
            string? connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("Azure Storage credentials not provided and AZURE_STORAGE_CONNECTION_STRING environment variable is not set");
            }
            _containerClient = new BlobContainerClient(connectionString, containerName);
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

        BlobClient blobClient = _containerClient.GetBlobClient(writePlan.ObjectName);
        using var stream = new MemoryStream(data);

        var uploadOptions = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = _formatter.ContentType
            },
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ghost-integrity-sha256"] = writePlan.IntegritySha256,
                ["ghost-idempotency-key"] = writePlan.IdempotencyKey
            }
        };

        try
        {
            await blobClient.UploadAsync(stream, uploadOptions, ct).ConfigureAwait(false);
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
            _disposed = true;
        }
    }
}
