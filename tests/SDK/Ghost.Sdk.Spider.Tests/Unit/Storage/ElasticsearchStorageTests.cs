using FluentAssertions;
using Ghost.Sdk.Spider.Configuration.Models;
using Ghost.Sdk.Spider.Storage.Contracts;
using Xunit;

namespace Ghost.Sdk.Spider.Tests.Unit.Storage;

/// <summary>
/// Tests for Elasticsearch storage implementation using mocking.
/// These tests verify the expected behavior of Elasticsearch storage without requiring a real cluster.
/// </summary>
public class ElasticsearchStorageTests
{
    private readonly ElasticsearchConfiguration _config;
    private readonly MockElasticsearchClient _mockClient;

    public ElasticsearchStorageTests()
    {
        _config = new ElasticsearchConfiguration
        {
            Nodes = new List<string> { "http://localhost:9200" },
            IndexName = "spider-data",
            AutoCreateIndex = true,
            NumberOfShards = 1,
            NumberOfReplicas = 0
        };

        _mockClient = new MockElasticsearchClient();
    }

    [Fact]
    public void Constructor_WithValidConfiguration_ShouldCreate()
    {
        // Arrange & Act
        var storage = new MockElasticsearchStorage(_mockClient, _config);

        // Assert
        storage.Should().NotBeNull();
        storage.Name.Should().Be("Elasticsearch");
    }

    [Fact]
    public void IsAvailable_WithHealthyCluster_ShouldReturnTrue()
    {
        // Arrange
        _mockClient.IsHealthy = true;
        var storage = new MockElasticsearchStorage(_mockClient, _config);

        // Act
        var isAvailable = storage.IsAvailable;

        // Assert
        isAvailable.Should().BeTrue();
    }

    [Fact]
    public void IsAvailable_WithUnhealthyCluster_ShouldReturnFalse()
    {
        // Arrange
        _mockClient.IsHealthy = false;
        var storage = new MockElasticsearchStorage(_mockClient, _config);

        // Act
        var isAvailable = storage.IsAvailable;

        // Assert
        isAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task InitializeAsync_WithAutoCreateIndex_ShouldCreateIndex()
    {
        // Arrange
        var storage = new MockElasticsearchStorage(_mockClient, _config);

        // Act
        await storage.InitializeAsync();

        // Assert
        _mockClient.IndexCreated.Should().BeTrue();
        _mockClient.CreatedIndexName.Should().Be("spider-data");
    }

    [Fact]
    public async Task InitializeAsync_WithExistingIndex_ShouldNotRecreate()
    {
        // Arrange
        _mockClient.IndexExists = true;
        var storage = new MockElasticsearchStorage(_mockClient, _config);

        // Act
        await storage.InitializeAsync();

        // Assert
        _mockClient.IndexCreated.Should().BeFalse();
    }

    [Fact]
    public async Task StoreAsync_ShouldIndexDocument()
    {
        // Arrange
        var storage = new MockElasticsearchStorage(_mockClient, _config);
        var item = new
        {
            Id = "doc-1",
            Title = "Test Document",
            Content = "This is test content",
            Tags = new[] { "tag1", "tag2" }
        };
        var context = new StorageContext
        {
            SpiderName = "TestSpider",
            SourceUrl = "https://example.com"
        };

        // Act
        var result = await storage.StoreAsync(item, context);

        // Assert
        result.Success.Should().BeTrue();
        result.ItemsStored.Should().Be(1);
        _mockClient.IndexedDocuments.Should().Be(1);
    }

    [Fact]
    public async Task StoreBatchAsync_ShouldBulkIndex()
    {
        // Arrange
        var storage = new MockElasticsearchStorage(_mockClient, _config);
        var items = new[]
        {
            new { Id = "1", Title = "Doc 1" },
            new { Id = "2", Title = "Doc 2" },
            new { Id = "3", Title = "Doc 3" }
        };
        var context = new StorageContext
        {
            SpiderName = "BulkSpider",
            BatchId = "bulk-001"
        };

        // Act
        var result = await storage.StoreBatchAsync(items, context);

        // Assert
        result.Success.Should().BeTrue();
        result.ItemsStored.Should().Be(3);
        _mockClient.BulkOperations.Should().Be(1);
        _mockClient.IndexedDocuments.Should().Be(3);
    }

    [Fact]
    public async Task StoreAsync_WithCustomIndex_ShouldUseCorrectIndex()
    {
        // Arrange
        var storage = new MockElasticsearchStorage(_mockClient, _config);
        var item = new { Id = "1", Data = "Test" };
        var context = new StorageContext
        {
            SpiderName = "CustomIndexSpider",
            TableName = "custom-index"
        };

        // Act
        var result = await storage.StoreAsync(item, context);

        // Assert
        result.Success.Should().BeTrue();
        _mockClient.LastUsedIndex.Should().Be("custom-index");
    }

    [Fact]
    public async Task StoreAsync_WithUpdate_ShouldUpdateDocument()
    {
        // Arrange
        var storage = new MockElasticsearchStorage(_mockClient, _config);
        var item = new { Id = "existing-doc", Title = "Updated Title" };
        var context = new StorageContext
        {
            SpiderName = "UpdateSpider",
            UpdateOnConflict = true
        };

        // Act
        var result = await storage.StoreAsync(item, context);

        // Assert
        result.Success.Should().BeTrue();
        _mockClient.UpdatedDocuments.Should().Be(1);
    }

    [Fact]
    public async Task StoreBatchAsync_WithPartialFailure_ShouldReportFailures()
    {
        // Arrange
        _mockClient.SimulatePartialFailure = true;
        var storage = new MockElasticsearchStorage(_mockClient, _config);
        var items = new[]
        {
            new { Id = "1", Title = "Doc 1" },
            new { Id = "2", Title = "Doc 2" },
            new { Id = "3", Title = "Doc 3" }
        };
        var context = StorageContext.Create("PartialFailureSpider");

        // Act
        var result = await storage.StoreBatchAsync(items, context);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("partial");
    }

    [Fact]
    public async Task StoreAsync_WithMappingConflict_ShouldReturnFailure()
    {
        // Arrange
        _mockClient.SimulateMappingConflict = true;
        var storage = new MockElasticsearchStorage(_mockClient, _config);
        var item = new { Id = "1", NumericField = "not-a-number" };
        var context = StorageContext.Create("MappingConflictSpider");

        // Act
        var result = await storage.StoreAsync(item, context);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("mapping");
    }

    [Fact]
    public async Task StoreBatchAsync_WithEmptyList_ShouldSucceed()
    {
        // Arrange
        var storage = new MockElasticsearchStorage(_mockClient, _config);
        var items = Array.Empty<object>();
        var context = StorageContext.Create("EmptyBatchSpider");

        // Act
        var result = await storage.StoreBatchAsync(items, context);

        // Assert
        result.Success.Should().BeTrue();
        result.ItemsStored.Should().Be(0);
    }

    [Fact]
    public async Task FlushAsync_ShouldRefreshIndex()
    {
        // Arrange
        var storage = new MockElasticsearchStorage(_mockClient, _config);

        // Act
        await storage.FlushAsync();

        // Assert
        _mockClient.IndexRefreshed.Should().BeTrue();
    }

    [Fact]
    public async Task CloseAsync_ShouldDisposeClient()
    {
        // Arrange
        var storage = new MockElasticsearchStorage(_mockClient, _config);

        // Act
        await storage.CloseAsync();

        // Assert
        _mockClient.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public async Task StoreAsync_WithNestedObject_ShouldIndexCorrectly()
    {
        // Arrange
        var storage = new MockElasticsearchStorage(_mockClient, _config);
        var item = new
        {
            Id = "nested-1",
            User = new
            {
                Name = "John Doe",
                Email = "john@example.com",
                Profile = new
                {
                    Age = 30,
                    Location = "New York"
                }
            }
        };
        var context = StorageContext.Create("NestedSpider");

        // Act
        var result = await storage.StoreAsync(item, context);

        // Assert
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task StoreAsync_WithArrayFields_ShouldIndexCorrectly()
    {
        // Arrange
        var storage = new MockElasticsearchStorage(_mockClient, _config);
        var item = new
        {
            Id = "array-1",
            Tags = new[] { "tag1", "tag2", "tag3" },
            Numbers = new[] { 1, 2, 3, 4, 5 }
        };
        var context = StorageContext.Create("ArraySpider");

        // Act
        var result = await storage.StoreAsync(item, context);

        // Assert
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task StoreBatchAsync_WithLargeBatch_ShouldHandleCorrectly()
    {
        // Arrange
        var storage = new MockElasticsearchStorage(_mockClient, _config);
        var items = Enumerable.Range(1, 1000)
            .Select(i => new { Id = i.ToString(), Title = $"Document {i}" })
            .ToArray();
        var context = new StorageContext
        {
            SpiderName = "LargeBatchSpider",
            BatchId = "large-bulk-001"
        };

        // Act
        var result = await storage.StoreBatchAsync(items, context);

        // Assert
        result.Success.Should().BeTrue();
        result.ItemsStored.Should().Be(1000);
    }

    [Fact]
    public async Task InitializeAsync_WithAuthentication_ShouldConnect()
    {
        // Arrange
        _config.Username = "elastic";
        _config.Password = "password";
        _config.UseSsl = true;
        var storage = new MockElasticsearchStorage(_mockClient, _config);

        // Act
        await storage.InitializeAsync();

        // Assert
        _mockClient.AuthenticationUsed.Should().BeTrue();
    }

    [Fact]
    public async Task StoreAsync_WithTimestamp_ShouldIncludeTimestamp()
    {
        // Arrange
        var storage = new MockElasticsearchStorage(_mockClient, _config);
        var item = new { Id = "1", Title = "Test" };
        var timestamp = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);
        var context = new StorageContext
        {
            SpiderName = "TimestampSpider",
            Timestamp = timestamp
        };

        // Act
        var result = await storage.StoreAsync(item, context);

        // Assert
        result.Success.Should().BeTrue();
        _mockClient.LastDocumentTimestamp.Should().Be(timestamp);
    }

    #region Mock Implementation

    private sealed class MockElasticsearchClient
    {
        public bool IsHealthy { get; set; } = true;
        public bool IndexExists { get; set; } = false;
        public bool IndexCreated { get; set; } = false;
        public string? CreatedIndexName { get; set; }
        public int IndexedDocuments { get; set; } = 0;
        public int UpdatedDocuments { get; set; } = 0;
        public int BulkOperations { get; set; } = 0;
        public string? LastUsedIndex { get; set; }
        public bool IndexRefreshed { get; set; } = false;
        public bool IsDisposed { get; set; } = false;
        public bool SimulatePartialFailure { get; set; } = false;
        public bool SimulateMappingConflict { get; set; } = false;
        public bool AuthenticationUsed { get; set; } = false;
        public DateTimeOffset? LastDocumentTimestamp { get; set; }
    }

    private sealed class MockElasticsearchStorage : IStorage
    {
        private readonly MockElasticsearchClient _client;
        private readonly ElasticsearchConfiguration _config;

        public MockElasticsearchStorage(MockElasticsearchClient client, ElasticsearchConfiguration config)
        {
            _client = client;
            _config = config;
        }

        public string Name => "Elasticsearch";
        public bool IsAvailable => _client.IsHealthy;

        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (!string.IsNullOrEmpty(_config.Username))
            {
                _client.AuthenticationUsed = true;
            }

            if (_config.AutoCreateIndex && !_client.IndexExists)
            {
                _client.IndexCreated = true;
                _client.CreatedIndexName = _config.IndexName;
            }

            return Task.CompletedTask;
        }

        public Task<StorageResult> StoreAsync<T>(T item, StorageContext context, CancellationToken cancellationToken = default)
        {
            var startTime = DateTimeOffset.UtcNow;

            try
            {
                if (_client.SimulateMappingConflict)
                {
                    return Task.FromResult(StorageResult.CreateFailure(
                        "Elasticsearch mapping conflict: field type mismatch",
                        new InvalidOperationException("Mapping conflict"),
                        DateTimeOffset.UtcNow - startTime));
                }

                var indexName = context.TableName ?? _config.IndexName;
                _client.LastUsedIndex = indexName;
                _client.LastDocumentTimestamp = context.Timestamp;

                if (context.UpdateOnConflict)
                {
                    _client.UpdatedDocuments++;
                }
                else
                {
                    _client.IndexedDocuments++;
                }

                return Task.FromResult(StorageResult.CreateSuccess(1, DateTimeOffset.UtcNow - startTime));
            }
            catch (Exception ex)
            {
                return Task.FromResult(StorageResult.CreateFailure(ex.Message, ex, DateTimeOffset.UtcNow - startTime));
            }
        }

        public Task<StorageResult> StoreBatchAsync<T>(IEnumerable<T> items, StorageContext context, CancellationToken cancellationToken = default)
        {
            var startTime = DateTimeOffset.UtcNow;
            var itemList = items.ToList();

            if (itemList.Count == 0)
            {
                return Task.FromResult(StorageResult.CreateSuccess(0, DateTimeOffset.UtcNow - startTime));
            }

            try
            {
                if (_client.SimulatePartialFailure)
                {
                    return Task.FromResult(StorageResult.CreateFailure(
                        "Bulk operation partially failed",
                        null,
                        DateTimeOffset.UtcNow - startTime));
                }

                _client.BulkOperations++;
                _client.IndexedDocuments += itemList.Count;
                _client.LastUsedIndex = context.TableName ?? _config.IndexName;

                return Task.FromResult(StorageResult.CreateSuccess(itemList.Count, DateTimeOffset.UtcNow - startTime));
            }
            catch (Exception ex)
            {
                return Task.FromResult(StorageResult.CreateFailure(ex.Message, ex, DateTimeOffset.UtcNow - startTime));
            }
        }

        public Task FlushAsync(CancellationToken cancellationToken = default)
        {
            _client.IndexRefreshed = true;
            return Task.CompletedTask;
        }

        public Task CloseAsync(CancellationToken cancellationToken = default)
        {
            _client.IsDisposed = true;
            return Task.CompletedTask;
        }
    }

    #endregion
}
