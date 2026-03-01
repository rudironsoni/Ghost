using Npgsql;

namespace Ghost.Cloud.Infrastructure.Idempotency;

public interface IIdempotencyService
{
    public Task<IdempotencyCheckResult> CheckAndStoreAsync(string key, string runId, TimeSpan ttl, CancellationToken ct = default);
    public Task<string?> GetRunIdAsync(string key, CancellationToken ct = default);
    public Task CleanupExpiredAsync(CancellationToken ct = default);
}

public record IdempotencyCheckResult
{
    public bool IsNew { get; init; }
    public string? ExistingRunId { get; init; }
    public bool IsExpired { get; init; }
}

public sealed class PostgreSqlIdempotencyService : IIdempotencyService, IDisposable
{
    private readonly string _connectionString;
    private bool _disposed;

    public PostgreSqlIdempotencyService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<IdempotencyCheckResult> CheckAndStoreAsync(string key, string runId, TimeSpan ttl, CancellationToken ct = default)
    {
        NpgsqlConnection connection = new NpgsqlConnection(_connectionString);
        try
        {
            await connection.OpenAsync(ct).ConfigureAwait(false);

            const string insertSql = """
                INSERT INTO IdempotencyKeys (Key, RunId, ExpiresAt, CreatedAt)
                VALUES (@Key, @RunId, @ExpiresAt, @CreatedAt)
                ON CONFLICT (Key) DO NOTHING
                RETURNING Key
                """;

            await using NpgsqlCommand insertCmd = new NpgsqlCommand(insertSql, connection);
            insertCmd.Parameters.AddWithValue("@Key", key);
            insertCmd.Parameters.AddWithValue("@RunId", runId);
            insertCmd.Parameters.AddWithValue("@ExpiresAt", DateTimeOffset.UtcNow.Add(ttl));
            insertCmd.Parameters.AddWithValue("@CreatedAt", DateTimeOffset.UtcNow);

            object? result = await insertCmd.ExecuteScalarAsync(ct).ConfigureAwait(false);

            if (result != null)
            {
                return new IdempotencyCheckResult { IsNew = true };
            }

            const string selectSql = """
                SELECT RunId, ExpiresAt
                FROM IdempotencyKeys
                WHERE Key = @Key
                """;

            await using NpgsqlCommand selectCmd = new NpgsqlCommand(selectSql, connection);
            selectCmd.Parameters.AddWithValue("@Key", key);

            NpgsqlDataReader reader = await selectCmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            try
            {
                if (await reader.ReadAsync(ct).ConfigureAwait(false))
                {
                    string existingRunId = reader.GetString(0);
                    DateTimeOffset expiresAt = reader.GetDateTime(1);
                    bool isExpired = expiresAt < DateTimeOffset.UtcNow;

                    return new IdempotencyCheckResult
                    {
                        IsNew = false,
                        ExistingRunId = existingRunId,
                        IsExpired = isExpired
                    };
                }
            }
            finally
            {
                await reader.DisposeAsync().ConfigureAwait(false);
            }

            return new IdempotencyCheckResult { IsNew = false };
        }
        finally
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    public async Task<string?> GetRunIdAsync(string key, CancellationToken ct = default)
    {
        NpgsqlConnection connection = new NpgsqlConnection(_connectionString);
        try
        {
            await connection.OpenAsync(ct).ConfigureAwait(false);

            const string sql = """
                SELECT RunId
                FROM IdempotencyKeys
                WHERE Key = @Key AND ExpiresAt > @Now
                """;

            await using NpgsqlCommand command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Key", key);
            command.Parameters.AddWithValue("@Now", DateTimeOffset.UtcNow);

            object? result = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
            return result?.ToString();
        }
        finally
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    public async Task CleanupExpiredAsync(CancellationToken ct = default)
    {
        NpgsqlConnection connection = new NpgsqlConnection(_connectionString);
        try
        {
            await connection.OpenAsync(ct).ConfigureAwait(false);

            const string sql = """
                DELETE FROM IdempotencyKeys
                WHERE ExpiresAt < @Now
                """;

            await using NpgsqlCommand command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Now", DateTimeOffset.UtcNow);

            await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
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
