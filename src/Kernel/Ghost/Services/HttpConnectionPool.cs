using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.Logging;

namespace Ghost.Services;

public interface IHttpConnectionPool
{
    public Task<HttpClient> AcquireAsync(CancellationToken cancellationToken = default);
    public Task ReleaseAsync(HttpClient client, bool healthy = true);
    public Task PruneUnhealthyAsync(CancellationToken cancellationToken = default);
    public int AvailableCount { get; }
    public int InUseCount { get; }
}

public sealed class HttpConnectionPool : IHttpConnectionPool
{
    private static readonly Action<ILogger, int, int, Exception?> _poolStatus = LoggerMessage.Define<int, int>(
        LogLevel.Debug,
        new EventId(1, "PoolStatus"),
        "Connection pool status - Available: {Available}, InUse: {InUse}");

    private readonly ConcurrentBag<PooledConnection> _available = new();
    private readonly ConcurrentDictionary<HttpClient, PooledConnection> _inUse = new();
    private readonly ILogger<HttpConnectionPool> _logger;
    private readonly HttpConnectionPoolOptions _options;

    private int _availableCount;
    private int _totalCreated;
    private int _totalRecycled;
    private int _totalPruned;

    public int AvailableCount => _availableCount;
    public int InUseCount => _inUse.Count;

    public HttpConnectionPool(HttpConnectionPoolOptions options, ILogger<HttpConnectionPool> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<HttpClient> AcquireAsync(CancellationToken cancellationToken = default)
    {
        while (_available.TryTake(out PooledConnection? pooled))
        {
            Interlocked.Decrement(ref _availableCount);

            if (await IsHealthyAsync(pooled, cancellationToken).ConfigureAwait(false))
            {
                _inTryAdd(pooled.Client, pooled);
                _poolStatus(_logger, _availableCount, _inUse.Count, null);
                return pooled.Client;
            }

            DisposeConnection(pooled);
            Interlocked.Increment(ref _totalPruned);
        }

        PooledConnection newConnection = CreateConnection();
        Interlocked.Increment(ref _totalCreated);
        _inUse.TryAdd(newConnection.Client, newConnection);
        _poolStatus(_logger, _availableCount, _inUse.Count, null);
        return newConnection.Client;
    }

    public Task ReleaseAsync(HttpClient client, bool healthy = true)
    {
        if (!_inUse.TryRemove(client, out PooledConnection? pooled))
        {
            DisposeClient(client);
            return Task.CompletedTask;
        }

        if (!healthy)
        {
            DisposeConnection(pooled);
            Interlocked.Increment(ref _totalPruned);
            return Task.CompletedTask;
        }

        if (pooled.UsageCount >= _options.MaxUsagePerConnection)
        {
            DisposeConnection(pooled);
            Interlocked.Increment(ref _totalRecycled);
            return Task.CompletedTask;
        }

        pooled.LastUsed = DateTime.UtcNow;
        pooled.UsageCount++;
        _available.Add(pooled);
        Interlocked.Increment(ref _availableCount);
        _poolStatus(_logger, _availableCount, _inUse.Count, null);

        return Task.CompletedTask;
    }

    public async Task PruneUnhealthyAsync(CancellationToken cancellationToken = default)
    {
        List<PooledConnection> healthy = [];
        List<PooledConnection> toPrune = [];

        while (_available.TryTake(out PooledConnection? pooled))
        {
            Interlocked.Decrement(ref _availableCount);

            if (await IsHealthyAsync(pooled, cancellationToken).ConfigureAwait(false))
            {
                healthy.Add(pooled);
            }
            else
            {
                toPrune.Add(pooled);
                Interlocked.Increment(ref _totalPruned);
            }
        }

        foreach (PooledConnection healthyConnection in healthy)
        {
            _available.Add(healthyConnection);
            Interlocked.Increment(ref _availableCount);
        }

        foreach (PooledConnection pruned in toPrune)
        {
            DisposeConnection(pruned);
        }

        _poolStatus(_logger, _availableCount, _inUse.Count, null);
    }

    private PooledConnection CreateConnection()
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = _options.ConnectionLifetime,
            PooledConnectionIdleTimeout = _options.IdleTimeout,
            MaxConnectionsPerServer = _options.MaxConnectionsPerServer,
            EnableMultipleHttp2Connections = true
        };

        var client = new HttpClient(handler, disposeHandler: true)
        {
            Timeout = _options.RequestTimeout
        };

        return new PooledConnection
        {
            Client = client,
            CreatedAt = DateTime.UtcNow,
            LastUsed = DateTime.UtcNow,
            UsageCount = 0
        };
    }

    private static async Task<bool> IsHealthyAsync(PooledConnection pooled, CancellationToken cancellationToken)
    {
        try
        {
            if (DateTime.UtcNow - pooled.LastUsed > pooled.Options.IdleTimeout)
            {
                return false;
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Head, "https://www.google.com/");
                using HttpResponseMessage response = await pooled.Client.SendAsync(request, cts.Token).ConfigureAwait(false);
                return response.IsSuccessStatusCode;
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                return false;
            }
        }
        catch
        {
            return false;
        }
    }

    private static void DisposeConnection(PooledConnection pooled)
    {
        try
        {
            pooled.Client?.Dispose();
        }
        catch { }
    }

    private static void DisposeClient(HttpClient client)
    {
        try
        {
            client?.Dispose();
        }
        catch
        {
        }
    }

    private bool _inTryAdd(HttpClient key, PooledConnection value) => _inUse.TryAdd(key, value);
}

public sealed record PooledConnection
{
    public HttpClient Client { get; init; } = null!;
    public DateTime CreatedAt { get; init; }
    public DateTime LastUsed { get; set; }
    public int UsageCount { get; set; }
    public HttpConnectionPoolOptions Options { get; init; } = new();
}

public sealed record HttpConnectionPoolOptions
{
    public TimeSpan ConnectionLifetime { get; init; } = TimeSpan.FromMinutes(10);
    public TimeSpan IdleTimeout { get; init; } = TimeSpan.FromMinutes(2);
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public int MaxUsagePerConnection { get; init; } = 1000;
    public int MaxConnectionsPerServer { get; init; } = 100;
}
