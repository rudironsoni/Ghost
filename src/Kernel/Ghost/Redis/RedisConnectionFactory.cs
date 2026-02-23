using StackExchange.Redis;

namespace Ghost.Redis;

/// <summary>
/// Factory for creating asynchronous Redis connections.
/// Use this instead of synchronous ConnectionMultiplexer.Connect() in DI.
/// </summary>
/// <remarks>
/// This factory provides async-first Redis connection establishment,
/// preventing sync-over-async deadlocks during DI container initialization.
/// </remarks>
public sealed class RedisConnectionFactory : IDisposable
{
  private readonly ConfigurationOptions _options;
  private ConnectionMultiplexer? _connection;
  private readonly SemaphoreSlim _connectLock = new(1, 1);

  /// <summary>
  /// Initializes a new instance of the <see cref="RedisConnectionFactory"/> class.
  /// </summary>
  /// <param name="options">The Redis configuration options.</param>
  public RedisConnectionFactory(ConfigurationOptions options)
  {
    _options = options ?? throw new ArgumentNullException(nameof(options));
  }

  /// <summary>
  /// Connects to Redis asynchronously.
  /// </summary>
  /// <param name="ct">Cancellation token.</param>
  /// <returns>The Redis connection multiplexer.</returns>
  public async Task<IConnectionMultiplexer> ConnectAsync(CancellationToken ct = default)
  {
    if (_connection is not null)
    {
      return _connection;
    }

    await _connectLock.WaitAsync(ct).ConfigureAwait(false);
    try
    {
      // Double-check after acquiring lock
      if (_connection is null)
      {
        _connection = await ConnectionMultiplexer.ConnectAsync(_options).ConfigureAwait(false);
      }
      return _connection;
    }
    finally
    {
      _connectLock.Release();
    }
  }

  /// <summary>
  /// Gets the existing connection if already established, otherwise null.
  /// </summary>
  public IConnectionMultiplexer? Connection => _connection;

  /// <inheritdoc />
  public void Dispose()
  {
    _connectLock.Dispose();
  }
}
