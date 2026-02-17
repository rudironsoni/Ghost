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
    private int _batchNumber;
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
        string key = $"{_prefix}/batch_{_batchNumber:D4}.{_formatter.Extension}";
        byte[] data = _formatter.FormatData(items);

        var request = new PutObjectRequest
        {
            BucketName = _bucket,
            Key = key,
            InputStream = new MemoryStream(data),
            ContentType = _formatter.ContentType
        };

        await _client.PutObjectAsync(request, ct).ConfigureAwait(false);
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
