using System.Text.Json;
using Ghost.Cloud.Contracts.Delivery;
using Npgsql;

namespace Ghost.Cloud.Infrastructure.Persistence;

public interface IScrapeRunQueries
{
    public Task<ScrapeRunReadModel?> GetRunAsync(string runId, CancellationToken ct = default);
    public Task<IReadOnlyList<ScrapeRunReadModel>> GetRunsByTenantAsync(Guid tenantId, int limit, int offset, CancellationToken ct = default);
    public Task<IReadOnlyList<ScrapeResultReadModel>> GetResultsAsync(string runId, string? cursor, int pageSize, CancellationToken ct = default);
}

public interface IArtifactQueries
{
    public Task<IReadOnlyList<ArtifactMetadataReadModel>> GetArtifactsAsync(string runId, CancellationToken ct = default);
    public Task<IReadOnlyList<ArtifactMetadataReadModel>> GetArtifactsByItemAsync(string runId, string itemId, CancellationToken ct = default);
}

public interface IEndpointQueries
{
    public Task<EndpointReadModel?> GetEndpointAsync(string endpointId, CancellationToken ct = default);
    public Task<IReadOnlyList<EndpointReadModel>> ListEndpointsAsync(bool includeInactive = false, CancellationToken ct = default);
}

public record ScrapeRunReadModel
{
    public string RunId { get; init; } = string.Empty;
    public string EndpointId { get; init; } = string.Empty;
    public Guid TenantId { get; init; }
    public string Status { get; init; } = string.Empty;
    public string Mode { get; init; } = string.Empty;
    public string? WorkerId { get; init; }
    public int ItemsDiscovered { get; init; }
    public int ItemsDelivered { get; init; }
    public int ArtifactsCaptured { get; init; }
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public string? ErrorMessage { get; init; }
    public DeliveryConfig? DeliveryConfig { get; init; }
    public string? ResultLocation { get; init; }

    // CL-M3: Classification, retry, and diagnostics fields for run lifecycle querying
    public string? Classification { get; init; }
    public string? ErrorCode { get; init; }
    public bool IsRetryable { get; init; }
    public int RetryCount { get; init; }
    public string? DiagnosticsUri { get; init; }
    public DateTimeOffset? LastRetryAt { get; init; }
}

public record ScrapeResultReadModel
{
    public Guid Id { get; init; }
    public string RunId { get; init; } = string.Empty;
    public string ItemId { get; init; } = string.Empty;
    public JsonElement Data { get; init; }
    public DateTimeOffset DiscoveredAt { get; init; }
}

public record ArtifactMetadataReadModel
{
    public Guid Id { get; init; }
    public string RunId { get; init; } = string.Empty;
    public string ItemId { get; init; } = string.Empty;
    public string ArtifactType { get; init; } = string.Empty;
    public string StorageUri { get; init; } = string.Empty;
    public string Hash { get; init; } = string.Empty;
    public long? SizeBytes { get; init; }
    public DateTimeOffset CapturedAt { get; init; }
}

public record EndpointReadModel
{
    public string EndpointId { get; init; } = string.Empty;
    public string PluginId { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Capability { get; init; } = string.Empty;
    public JsonElement InputSchema { get; init; }
    public JsonElement OutputSchema { get; init; }
    public List<string> DeliveryModes { get; init; } = [];
    public bool SupportsArtifacts { get; init; }
    public bool IsActive { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class PostgreSqlReadStore : IScrapeRunQueries, IArtifactQueries, IEndpointQueries, IDisposable
{
    private readonly string _connectionString;
    private bool _disposed;

    public PostgreSqlReadStore(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<ScrapeRunReadModel?> GetRunAsync(string runId, CancellationToken ct = default)
    {
        NpgsqlConnection connection = new NpgsqlConnection(_connectionString);
        try
        {
            await connection.OpenAsync(ct).ConfigureAwait(false);

            const string sql = """
                SELECT RunId, EndpointId, TenantId, Status, Mode, WorkerId,
                       ItemsDiscovered, ItemsDelivered, ArtifactsCaptured,
                       StartedAt, CompletedAt, ErrorMessage, ErrorCode,
                       Classification, IsRetryable, RetryCount, DiagnosticsUri,
                       LastRetryAt, DeliveryConfig, ResultLocation
                FROM ScrapeRunReadModels
                WHERE RunId = @RunId
                """;

            await using NpgsqlCommand command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("@RunId", runId);

            NpgsqlDataReader reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            try
            {
                if (await reader.ReadAsync(ct).ConfigureAwait(false))
                {
                    return MapToScrapeRunReadModel(reader);
                }
            }
            finally
            {
                await reader.DisposeAsync().ConfigureAwait(false);
            }

            return null;
        }
        finally
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<ScrapeRunReadModel>> GetRunsByTenantAsync(Guid tenantId, int limit, int offset, CancellationToken ct = default)
    {
        NpgsqlConnection connection = new NpgsqlConnection(_connectionString);
        try
        {
            await connection.OpenAsync(ct).ConfigureAwait(false);

            const string sql = """
                SELECT RunId, EndpointId, TenantId, Status, Mode, WorkerId,
                       ItemsDiscovered, ItemsDelivered, ArtifactsCaptured,
                       StartedAt, CompletedAt, ErrorMessage, ErrorCode,
                       Classification, IsRetryable, RetryCount, DiagnosticsUri,
                       LastRetryAt, DeliveryConfig, ResultLocation
                FROM ScrapeRunReadModels
                WHERE TenantId = @TenantId
                ORDER BY StartedAt DESC
                LIMIT @Limit OFFSET @Offset
                """;

            await using NpgsqlCommand command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("@TenantId", tenantId);
            command.Parameters.AddWithValue("@Limit", limit);
            command.Parameters.AddWithValue("@Offset", offset);

            List<ScrapeRunReadModel> results = new List<ScrapeRunReadModel>();
            NpgsqlDataReader reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            try
            {
                while (await reader.ReadAsync(ct).ConfigureAwait(false))
                {
                    results.Add(MapToScrapeRunReadModel(reader));
                }
            }
            finally
            {
                await reader.DisposeAsync().ConfigureAwait(false);
            }

            return results;
        }
        finally
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<ScrapeResultReadModel>> GetResultsAsync(string runId, string? cursor, int pageSize, CancellationToken ct = default)
    {
        NpgsqlConnection connection = new NpgsqlConnection(_connectionString);
        try
        {
            await connection.OpenAsync(ct).ConfigureAwait(false);

            string sql;

            if (string.IsNullOrEmpty(cursor))
            {
                sql = """
                    SELECT Id, RunId, ItemId, Data, DiscoveredAt
                    FROM ScrapeResults
                    WHERE RunId = @RunId
                    ORDER BY DiscoveredAt ASC
                    LIMIT @PageSize
                    """;
            }
            else
            {
                sql = """
                    SELECT Id, RunId, ItemId, Data, DiscoveredAt
                    FROM ScrapeResults
                    WHERE RunId = @RunId AND DiscoveredAt > (SELECT DiscoveredAt FROM ScrapeResults WHERE Id = @Cursor)
                    ORDER BY DiscoveredAt ASC
                    LIMIT @PageSize
                    """;
            }

            await using NpgsqlCommand command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("@RunId", runId);
            command.Parameters.AddWithValue("@PageSize", pageSize);
            if (!string.IsNullOrEmpty(cursor))
            {
                command.Parameters.AddWithValue("@Cursor", Guid.Parse(cursor));
            }

            List<ScrapeResultReadModel> results = new List<ScrapeResultReadModel>();
            NpgsqlDataReader reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            try
            {
                while (await reader.ReadAsync(ct).ConfigureAwait(false))
                {
                    results.Add(new ScrapeResultReadModel
                    {
                        Id = reader.GetGuid(0),
                        RunId = reader.GetString(1),
                        ItemId = reader.GetString(2),
                        Data = JsonSerializer.Deserialize<JsonElement>(reader.GetString(3)),
                        DiscoveredAt = reader.GetDateTime(4)
                    });
                }
            }
            finally
            {
                await reader.DisposeAsync().ConfigureAwait(false);
            }

            return results;
        }
        finally
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<ArtifactMetadataReadModel>> GetArtifactsAsync(string runId, CancellationToken ct = default)
    {
        NpgsqlConnection connection = new NpgsqlConnection(_connectionString);
        try
        {
            await connection.OpenAsync(ct).ConfigureAwait(false);

            const string sql = """
                SELECT Id, RunId, ItemId, ArtifactType, StorageUri, Hash, SizeBytes, CapturedAt
                FROM ArtifactMetadata
                WHERE RunId = @RunId
                ORDER BY CapturedAt DESC
                """;

            await using NpgsqlCommand command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("@RunId", runId);

            List<ArtifactMetadataReadModel> results = new List<ArtifactMetadataReadModel>();
            NpgsqlDataReader reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            try
            {
                while (await reader.ReadAsync(ct).ConfigureAwait(false))
                {
                    results.Add(new ArtifactMetadataReadModel
                    {
                        Id = reader.GetGuid(0),
                        RunId = reader.GetString(1),
                        ItemId = reader.GetString(2),
                        ArtifactType = reader.GetString(3),
                        StorageUri = reader.GetString(4),
                        Hash = reader.GetString(5),
                        SizeBytes = reader.IsDBNull(6) ? null : reader.GetInt64(6),
                        CapturedAt = reader.GetDateTime(7)
                    });
                }
            }
            finally
            {
                await reader.DisposeAsync().ConfigureAwait(false);
            }

            return results;
        }
        finally
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<ArtifactMetadataReadModel>> GetArtifactsByItemAsync(string runId, string itemId, CancellationToken ct = default)
    {
        NpgsqlConnection connection = new NpgsqlConnection(_connectionString);
        try
        {
            await connection.OpenAsync(ct).ConfigureAwait(false);

            const string sql = """
                SELECT Id, RunId, ItemId, ArtifactType, StorageUri, Hash, SizeBytes, CapturedAt
                FROM ArtifactMetadata
                WHERE RunId = @RunId AND ItemId = @ItemId
                ORDER BY CapturedAt DESC
                """;

            await using NpgsqlCommand command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("@RunId", runId);
            command.Parameters.AddWithValue("@ItemId", itemId);

            List<ArtifactMetadataReadModel> results = new List<ArtifactMetadataReadModel>();
            NpgsqlDataReader reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            try
            {
                while (await reader.ReadAsync(ct).ConfigureAwait(false))
                {
                    results.Add(new ArtifactMetadataReadModel
                    {
                        Id = reader.GetGuid(0),
                        RunId = reader.GetString(1),
                        ItemId = reader.GetString(2),
                        ArtifactType = reader.GetString(3),
                        StorageUri = reader.GetString(4),
                        Hash = reader.GetString(5),
                        SizeBytes = reader.IsDBNull(6) ? null : reader.GetInt64(6),
                        CapturedAt = reader.GetDateTime(7)
                    });
                }
            }
            finally
            {
                await reader.DisposeAsync().ConfigureAwait(false);
            }

            return results;
        }
        finally
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    public async Task<EndpointReadModel?> GetEndpointAsync(string endpointId, CancellationToken ct = default)
    {
        NpgsqlConnection connection = new NpgsqlConnection(_connectionString);
        try
        {
            await connection.OpenAsync(ct).ConfigureAwait(false);

            const string sql = """
                SELECT EndpointId, PluginId, Version, DisplayName, Capability,
                       InputSchema, OutputSchema, DeliveryModes, SupportsArtifacts, IsActive, CreatedAt
                FROM EndpointRegistry
                WHERE EndpointId = @EndpointId
                """;

            await using NpgsqlCommand command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("@EndpointId", endpointId);

            NpgsqlDataReader reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            try
            {
                if (await reader.ReadAsync(ct).ConfigureAwait(false))
                {
                    return MapToEndpointReadModel(reader);
                }
            }
            finally
            {
                await reader.DisposeAsync().ConfigureAwait(false);
            }

            return null;
        }
        finally
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<EndpointReadModel>> ListEndpointsAsync(bool includeInactive = false, CancellationToken ct = default)
    {
        NpgsqlConnection connection = new NpgsqlConnection(_connectionString);
        try
        {
            await connection.OpenAsync(ct).ConfigureAwait(false);

            string sql = """
                SELECT EndpointId, PluginId, Version, DisplayName, Capability,
                       InputSchema, OutputSchema, DeliveryModes, SupportsArtifacts, IsActive, CreatedAt
                FROM EndpointRegistry
                """;

            if (!includeInactive)
            {
                sql += " WHERE IsActive = TRUE";
            }

            sql += " ORDER BY DisplayName";

            await using NpgsqlCommand command = new NpgsqlCommand(sql, connection);

            List<EndpointReadModel> results = new List<EndpointReadModel>();
            NpgsqlDataReader reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            try
            {
                while (await reader.ReadAsync(ct).ConfigureAwait(false))
                {
                    results.Add(MapToEndpointReadModel(reader));
                }
            }
            finally
            {
                await reader.DisposeAsync().ConfigureAwait(false);
            }

            return results;
        }
        finally
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static ScrapeRunReadModel MapToScrapeRunReadModel(NpgsqlDataReader reader)
    {
        return new ScrapeRunReadModel
        {
            RunId = reader.GetString(0),
            EndpointId = reader.GetString(1),
            TenantId = reader.GetGuid(2),
            Status = reader.GetString(3),
            Mode = reader.GetString(4),
            WorkerId = reader.IsDBNull(5) ? null : reader.GetString(5),
            ItemsDiscovered = reader.GetInt32(6),
            ItemsDelivered = reader.GetInt32(7),
            ArtifactsCaptured = reader.GetInt32(8),
            StartedAt = reader.GetDateTime(9),
            CompletedAt = reader.IsDBNull(10) ? null : reader.GetDateTime(10),
            ErrorMessage = reader.IsDBNull(11) ? null : reader.GetString(11),
            ErrorCode = reader.IsDBNull(12) ? null : reader.GetString(12),
            Classification = reader.IsDBNull(13) ? null : reader.GetString(13),
            IsRetryable = !reader.IsDBNull(14) && reader.GetBoolean(14),
            RetryCount = reader.IsDBNull(15) ? 0 : reader.GetInt32(15),
            DiagnosticsUri = reader.IsDBNull(16) ? null : reader.GetString(16),
            LastRetryAt = reader.IsDBNull(17) ? null : reader.GetDateTime(17),
            DeliveryConfig = reader.IsDBNull(18) ? null : JsonSerializer.Deserialize<DeliveryConfig>(reader.GetString(18)),
            ResultLocation = reader.IsDBNull(19) ? null : reader.GetString(19)
        };
    }

    private static EndpointReadModel MapToEndpointReadModel(NpgsqlDataReader reader)
    {
        return new EndpointReadModel
        {
            EndpointId = reader.GetString(0),
            PluginId = reader.GetString(1),
            Version = reader.GetString(2),
            DisplayName = reader.GetString(3),
            Capability = reader.GetString(4),
            InputSchema = JsonSerializer.Deserialize<JsonElement>(reader.GetString(5)),
            OutputSchema = JsonSerializer.Deserialize<JsonElement>(reader.GetString(6)),
            DeliveryModes = JsonSerializer.Deserialize<List<string>>(reader.GetString(7)) ?? [],
            SupportsArtifacts = reader.GetBoolean(8),
            IsActive = reader.GetBoolean(9),
            CreatedAt = reader.GetDateTime(10)
        };
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}
