using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Ghost.Cloud.Contracts.Delivery;
using Ghost.Cloud.Delivery.Formatters;

namespace Ghost.Cloud.Delivery.Sinks;

public sealed class S3ResultSink : IResultSink, IDisposable
{
    private readonly AmazonS3Client _client;
    private readonly string _bucket;
    private readonly string _prefix;
    private readonly IResultFormatter _formatter;
    private readonly SinkWriteTracker _writeTracker = new();
    private bool _disposed;

    public string Type => "s3";

    public S3ResultSink(
        StorageCredentials credentials,
        string bucket,
        string prefix,
        IResultFormatter formatter)
    {
        _bucket = bucket;
        _prefix = prefix;
        _formatter = formatter;

        if (!string.IsNullOrEmpty(credentials.RoleArn))
        {
            _client = new AmazonS3Client();
        }
        else if (!string.IsNullOrEmpty(credentials.AccessKey) && !string.IsNullOrEmpty(credentials.SecretKey))
        {
            _client = new AmazonS3Client(
                credentials.AccessKey,
                credentials.SecretKey,
                new AmazonS3Config { RegionEndpoint = RegionEndpoint.USEast1 });
        }
        else
        {
            _client = new AmazonS3Client();
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

        var request = new PutObjectRequest
        {
            BucketName = _bucket,
            Key = writePlan.ObjectName,
            InputStream = new MemoryStream(data),
            ContentType = _formatter.ContentType
        };
        request.Metadata["ghost-integrity-sha256"] = writePlan.IntegritySha256;
        request.Metadata["ghost-idempotency-key"] = writePlan.IdempotencyKey;

        try
        {
            await _client.PutObjectAsync(request, ct).ConfigureAwait(false);
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
