namespace Ghost.Sdk.Spider.Configuration.Models;

/// <summary>
/// Configuration for data storage.
/// </summary>
public sealed class StorageConfiguration
{
    /// <summary>
    /// Gets or sets the storage provider type (InMemory, PostgreSQL, Elasticsearch, Custom).
    /// </summary>
    public string Provider { get; set; } = "InMemory";

    /// <summary>
    /// Gets or sets the connection string.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the database/index name.
    /// </summary>
    public string? DatabaseName { get; set; }

    /// <summary>
    /// Gets or sets the collection/table name.
    /// </summary>
    public string? CollectionName { get; set; }

    /// <summary>
    /// Gets or sets PostgreSQL-specific configuration.
    /// </summary>
    public PostgreSqlConfiguration? PostgreSql { get; set; }

    /// <summary>
    /// Gets or sets Elasticsearch-specific configuration.
    /// </summary>
    public ElasticsearchConfiguration? Elasticsearch { get; set; }

    /// <summary>
    /// Gets or sets whether to batch write operations.
    /// </summary>
    public bool UseBatching { get; set; } = true;

    /// <summary>
    /// Gets or sets the batch size for write operations.
    /// </summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Gets or sets the batch timeout (milliseconds).
    /// </summary>
    public int BatchTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Gets or sets custom storage provider configuration.
    /// </summary>
    public Dictionary<string, object> CustomConfiguration { get; set; } = [];
}

/// <summary>
/// PostgreSQL-specific configuration.
/// </summary>
public sealed class PostgreSqlConfiguration
{
    /// <summary>
    /// Gets or sets the schema name.
    /// </summary>
    public string Schema { get; set; } = "public";

    /// <summary>
    /// Gets or sets the table name.
    /// </summary>
    public string TableName { get; set; } = "spider_data";

    /// <summary>
    /// Gets or sets whether to auto-create the table.
    /// </summary>
    public bool AutoCreateTable { get; set; } = true;

    /// <summary>
    /// Gets or sets the connection pool size.
    /// </summary>
    public int PoolSize { get; set; } = 10;

    /// <summary>
    /// Gets or sets additional connection parameters.
    /// </summary>
    public Dictionary<string, string> ConnectionParameters { get; set; } = [];
}

/// <summary>
/// Elasticsearch-specific configuration.
/// </summary>
public sealed class ElasticsearchConfiguration
{
    /// <summary>
    /// Gets or sets the Elasticsearch node URIs.
    /// </summary>
    public List<string> Nodes { get; set; } = [];

    /// <summary>
    /// Gets or sets the index name.
    /// </summary>
    public string IndexName { get; set; } = "spider-data";

    /// <summary>
    /// Gets or sets whether to auto-create the index.
    /// </summary>
    public bool AutoCreateIndex { get; set; } = true;

    /// <summary>
    /// Gets or sets the number of shards.
    /// </summary>
    public int NumberOfShards { get; set; } = 1;

    /// <summary>
    /// Gets or sets the number of replicas.
    /// </summary>
    public int NumberOfReplicas { get; set; }

    /// <summary>
    /// Gets or sets authentication username.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Gets or sets authentication password.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Gets or sets whether to use SSL.
    /// </summary>
    public bool UseSsl { get; set; }

    /// <summary>
    /// Gets or sets index mappings configuration.
    /// </summary>
    public Dictionary<string, object> Mappings { get; set; } = [];
}
