using System.Data;
using System.Text.Json;
using Ghost.Cloud.Contracts.Events;
using Npgsql;

namespace Ghost.Cloud.Infrastructure.EventStore;

public interface IEventStore
{
    public Task<long> AppendAsync(string grainId, string grainType, ScrapeRunEvent scrapeEvent, long expectedVersion, CancellationToken ct = default);
    public Task<IReadOnlyList<StoredEvent>> ReadAsync(string grainId, long fromVersion, int maxCount, CancellationToken ct = default);
    public Task<long> GetLatestVersionAsync(string grainId, CancellationToken ct = default);
}

public record StoredEvent
{
    public long Version { get; init; }
    public string EventType { get; init; } = string.Empty;
    public JsonElement EventData { get; init; }
    public DateTimeOffset Timestamp { get; init; }
}

public sealed class PostgreSqlEventStore : IEventStore, IDisposable
{
    private readonly string _connectionString;
    private readonly JsonSerializerOptions _jsonOptions;
    private bool _disposed;

    public PostgreSqlEventStore(string connectionString)
    {
        _connectionString = connectionString;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task<long> AppendAsync(string grainId, string grainType, ScrapeRunEvent scrapeEvent, long expectedVersion, CancellationToken ct = default)
    {
        NpgsqlConnection connection = new NpgsqlConnection(_connectionString);
        try
        {
            await connection.OpenAsync(ct).ConfigureAwait(false);

            NpgsqlTransaction transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
            try
            {
                long currentVersion = await GetLatestVersionAsync(grainId, ct).ConfigureAwait(false);
                if (currentVersion != expectedVersion)
                {
                    throw new ConcurrencyException($"Expected version {expectedVersion} but found {currentVersion}");
                }

                long newVersion = expectedVersion + 1;
                string eventType = scrapeEvent.GetType().Name;
                string eventData = JsonSerializer.Serialize(scrapeEvent, scrapeEvent.GetType(), _jsonOptions);

                const string sql = """
                    INSERT INTO OrleansEventStore (GrainId, GrainType, Version, EventType, EventData, Timestamp)
                    VALUES (@GrainId, @GrainType, @Version, @EventType, @EventData::jsonb, @Timestamp)
                    """;

                NpgsqlCommand command = new NpgsqlCommand(sql, connection, transaction);
                try
                {
                    command.Parameters.AddWithValue("@GrainId", grainId);
                    command.Parameters.AddWithValue("@GrainType", grainType);
                    command.Parameters.AddWithValue("@Version", newVersion);
                    command.Parameters.AddWithValue("@EventType", eventType);
                    command.Parameters.AddWithValue("@EventData", eventData);
                    command.Parameters.AddWithValue("@Timestamp", DateTimeOffset.UtcNow);

                    await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                }
                finally
                {
                    await command.DisposeAsync().ConfigureAwait(false);
                }
                await transaction.CommitAsync(ct).ConfigureAwait(false);

                return newVersion;
            }
            catch
            {
                await transaction.RollbackAsync(ct).ConfigureAwait(false);
                throw;
            }
            finally
            {
                await transaction.DisposeAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<StoredEvent>> ReadAsync(string grainId, long fromVersion, int maxCount, CancellationToken ct = default)
    {
        NpgsqlConnection connection = new NpgsqlConnection(_connectionString);
        try
        {
            await connection.OpenAsync(ct).ConfigureAwait(false);

            const string sql = """
                SELECT Version, EventType, EventData, Timestamp
                FROM OrleansEventStore
                WHERE GrainId = @GrainId AND Version >= @FromVersion
                ORDER BY Version ASC
                LIMIT @MaxCount
                """;

            NpgsqlCommand command = new NpgsqlCommand(sql, connection);
            try
            {
                command.Parameters.AddWithValue("@GrainId", grainId);
                command.Parameters.AddWithValue("@FromVersion", fromVersion);
                command.Parameters.AddWithValue("@MaxCount", maxCount);

                List<StoredEvent> events = new List<StoredEvent>();
                NpgsqlDataReader reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
                try
                {
                    while (await reader.ReadAsync(ct).ConfigureAwait(false))
                    {
                        events.Add(new StoredEvent
                        {
                            Version = reader.GetInt64(0),
                            EventType = reader.GetString(1),
                            EventData = JsonSerializer.Deserialize<JsonElement>(reader.GetString(2)),
                            Timestamp = reader.GetDateTime(3)
                        });
                    }
                }
                finally
                {
                    await reader.DisposeAsync().ConfigureAwait(false);
                }

                return events;
            }
            finally
            {
                await command.DisposeAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    public async Task<long> GetLatestVersionAsync(string grainId, CancellationToken ct = default)
    {
        NpgsqlConnection connection = new NpgsqlConnection(_connectionString);
        try
        {
            await connection.OpenAsync(ct).ConfigureAwait(false);

            const string sql = """
                SELECT COALESCE(MAX(Version), 0)
                FROM OrleansEventStore
                WHERE GrainId = @GrainId
                """;

            NpgsqlCommand command = new NpgsqlCommand(sql, connection);
            try
            {
                command.Parameters.AddWithValue("@GrainId", grainId);

                object? result = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
                return result != null ? (long)result : 0;
            }
            finally
            {
                await command.DisposeAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}

public class ConcurrencyException : Exception
{
    public ConcurrencyException(string message) : base(message) { }
}
