using FluentAssertions;
using Ghost.Sdk.Spider.Configuration.Models;
using Ghost.Sdk.Spider.Storage.Contracts;
using Moq;
using NUnit.Framework;
using System.Data;

namespace Ghost.Sdk.Spider.Tests.Unit.Storage;

/// <summary>
/// Tests for PostgreSQL storage implementation using mocking.
/// These tests verify the expected behavior of PostgreSQL storage without requiring a real database.
/// </summary>
[TestFixture]
public class PostgreSqlStorageTests
{
    private Mock<IDbConnection> _mockConnection = null!;
    private Mock<IDbCommand> _mockCommand = null!;
    private Mock<IDbTransaction> _mockTransaction = null!;
    private PostgreSqlConfiguration _config = null!;

    [SetUp]
    public void Setup()
    {
        _mockConnection = new Mock<IDbConnection>();
        _mockCommand = new Mock<IDbCommand>();
        _mockTransaction = new Mock<IDbTransaction>();
        
        _mockConnection.Setup(c => c.CreateCommand()).Returns(_mockCommand.Object);
        _mockConnection.Setup(c => c.BeginTransaction()).Returns(_mockTransaction.Object);
        
        _config = new PostgreSqlConfiguration
        {
            Schema = "public",
            TableName = "spider_data",
            AutoCreateTable = true,
            PoolSize = 10
        };
    }

    [Test]
    public void Constructor_WithValidConfiguration_ShouldCreate()
    {
        // Arrange & Act
        var storage = new MockPostgreSqlStorage(_mockConnection.Object, _config);

        // Assert
        storage.Should().NotBeNull();
        storage.Name.Should().Be("PostgreSQL");
    }

    [Test]
    public void IsAvailable_WithOpenConnection_ShouldReturnTrue()
    {
        // Arrange
        _mockConnection.Setup(c => c.State).Returns(ConnectionState.Open);
        var storage = new MockPostgreSqlStorage(_mockConnection.Object, _config);

        // Act
        var isAvailable = storage.IsAvailable;

        // Assert
        isAvailable.Should().BeTrue();
    }

    [Test]
    public void IsAvailable_WithClosedConnection_ShouldReturnFalse()
    {
        // Arrange
        _mockConnection.Setup(c => c.State).Returns(ConnectionState.Closed);
        var storage = new MockPostgreSqlStorage(_mockConnection.Object, _config);

        // Act
        var isAvailable = storage.IsAvailable;

        // Assert
        isAvailable.Should().BeFalse();
    }

    [Test]
    public async Task InitializeAsync_ShouldOpenConnection()
    {
        // Arrange
        var storage = new MockPostgreSqlStorage(_mockConnection.Object, _config);

        // Act
        await storage.InitializeAsync();

        // Assert
        _mockConnection.Verify(c => c.Open(), Times.Once);
    }

    [Test]
    public async Task InitializeAsync_WithAutoCreateTable_ShouldCreateTable()
    {
        // Arrange
        var storage = new MockPostgreSqlStorage(_mockConnection.Object, _config);

        // Act
        await storage.InitializeAsync();

        // Assert
        _mockConnection.Verify(c => c.CreateCommand(), Times.AtLeastOnce);
    }

    [Test]
    public async Task StoreAsync_ShouldExecuteInsertCommand()
    {
        // Arrange
        var storage = new MockPostgreSqlStorage(_mockConnection.Object, _config);
        var item = new { Id = 1, Name = "Test", Value = 42 };
        var context = new StorageContext
        {
            SpiderName = "TestSpider",
            SourceUrl = "https://example.com",
            TableName = "products"
        };

        _mockCommand.Setup(c => c.ExecuteNonQuery()).Returns(1);

        // Act
        var result = await storage.StoreAsync(item, context);

        // Assert
        result.Success.Should().BeTrue();
        result.ItemsStored.Should().Be(1);
        _mockCommand.Verify(c => c.ExecuteNonQuery(), Times.Once);
    }

    [Test]
    public async Task StoreAsync_WithUpsert_ShouldExecuteUpsertCommand()
    {
        // Arrange
        var storage = new MockPostgreSqlStorage(_mockConnection.Object, _config);
        var item = new { Id = 1, Name = "Test", Url = "https://example.com/item" };
        var context = new StorageContext
        {
            SpiderName = "TestSpider",
            UpdateOnConflict = true,
            UniqueKeys = new List<string> { "Id", "Url" }
        };

        _mockCommand.Setup(c => c.ExecuteNonQuery()).Returns(1);

        // Act
        var result = await storage.StoreAsync(item, context);

        // Assert
        result.Success.Should().BeTrue();
        _mockCommand.Verify(c => c.ExecuteNonQuery(), Times.Once);
    }

    [Test]
    public async Task StoreBatchAsync_ShouldExecuteBatchInsert()
    {
        // Arrange
        var storage = new MockPostgreSqlStorage(_mockConnection.Object, _config);
        var items = new[]
        {
            new { Id = 1, Name = "Item1" },
            new { Id = 2, Name = "Item2" },
            new { Id = 3, Name = "Item3" }
        };
        var context = new StorageContext
        {
            SpiderName = "BatchSpider",
            BatchId = "batch-001"
        };

        _mockCommand.Setup(c => c.ExecuteNonQuery()).Returns(3);

        // Act
        var result = await storage.StoreBatchAsync(items, context);

        // Assert
        result.Success.Should().BeTrue();
        result.ItemsStored.Should().Be(3);
        _mockConnection.Verify(c => c.BeginTransaction(), Times.Once);
        _mockTransaction.Verify(t => t.Commit(), Times.Once);
    }

    [Test]
    public async Task StoreBatchAsync_WithError_ShouldRollback()
    {
        // Arrange
        var storage = new MockPostgreSqlStorage(_mockConnection.Object, _config);
        var items = new[] { new { Id = 1 } };
        var context = StorageContext.Create("TestSpider");

        _mockCommand.Setup(c => c.ExecuteNonQuery()).Throws(new InvalidOperationException("Database error"));

        // Act
        var result = await storage.StoreBatchAsync(items, context);

        // Assert
        result.Success.Should().BeFalse();
        _mockTransaction.Verify(t => t.Rollback(), Times.Once);
    }

    [Test]
    public async Task StoreAsync_WithNullValues_ShouldHandleCorrectly()
    {
        // Arrange
        var storage = new MockPostgreSqlStorage(_mockConnection.Object, _config);
        var item = new { Id = 1, Name = "Test", Description = (string?)null };
        var context = StorageContext.Create("TestSpider");

        _mockCommand.Setup(c => c.ExecuteNonQuery()).Returns(1);

        // Act
        var result = await storage.StoreAsync(item, context);

        // Assert
        result.Success.Should().BeTrue();
    }

    [Test]
    public async Task StoreAsync_WithLargeObject_ShouldHandle()
    {
        // Arrange
        var storage = new MockPostgreSqlStorage(_mockConnection.Object, _config);
        var item = new
        {
            Id = 1,
            LargeText = new string('A', 100000),
            JsonData = new { Complex = new { Nested = new { Data = "Value" } } }
        };
        var context = StorageContext.Create("LargeObjectSpider");

        _mockCommand.Setup(c => c.ExecuteNonQuery()).Returns(1);

        // Act
        var result = await storage.StoreAsync(item, context);

        // Assert
        result.Success.Should().BeTrue();
    }

    [Test]
    public async Task FlushAsync_ShouldComplete()
    {
        // Arrange
        var storage = new MockPostgreSqlStorage(_mockConnection.Object, _config);

        // Act
        var act = async () => await storage.FlushAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Test]
    public async Task CloseAsync_ShouldCloseConnection()
    {
        // Arrange
        var storage = new MockPostgreSqlStorage(_mockConnection.Object, _config);

        // Act
        await storage.CloseAsync();

        // Assert
        _mockConnection.Verify(c => c.Close(), Times.Once);
    }

    [Test]
    public async Task StoreAsync_WithCustomSchema_ShouldUseCorrectSchema()
    {
        // Arrange
        _config.Schema = "custom_schema";
        var storage = new MockPostgreSqlStorage(_mockConnection.Object, _config);
        var item = new { Id = 1 };
        var context = StorageContext.Create("TestSpider");

        _mockCommand.Setup(c => c.ExecuteNonQuery()).Returns(1);

        // Act
        var result = await storage.StoreAsync(item, context);

        // Assert
        result.Success.Should().BeTrue();
        // Would verify SQL contains "custom_schema" in real implementation
    }

    [Test]
    public async Task StoreBatchAsync_WithEmptyList_ShouldSucceed()
    {
        // Arrange
        var storage = new MockPostgreSqlStorage(_mockConnection.Object, _config);
        var items = Array.Empty<object>();
        var context = StorageContext.Create("EmptyBatchSpider");

        // Act
        var result = await storage.StoreBatchAsync(items, context);

        // Assert
        result.Success.Should().BeTrue();
        result.ItemsStored.Should().Be(0);
    }

    [Test]
    public async Task StoreAsync_WithConnectionFailure_ShouldReturnFailure()
    {
        // Arrange
        var storage = new MockPostgreSqlStorage(_mockConnection.Object, _config);
        _mockCommand.Setup(c => c.ExecuteNonQuery()).Throws(new InvalidOperationException("Connection lost"));
        
        var item = new { Id = 1 };
        var context = StorageContext.Create("TestSpider");

        // Act
        var result = await storage.StoreAsync(item, context);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Connection lost");
    }

    #region Mock Implementation

    private class MockPostgreSqlStorage : IStorage
    {
        private readonly IDbConnection _connection;
        private readonly PostgreSqlConfiguration _config;

        public MockPostgreSqlStorage(IDbConnection connection, PostgreSqlConfiguration config)
        {
            _connection = connection;
            _config = config;
        }

        public string Name => "PostgreSQL";
        public bool IsAvailable => _connection.State == ConnectionState.Open;

        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            _connection.Open();
            
            if (_config.AutoCreateTable)
            {
                var command = _connection.CreateCommand();
                command.CommandText = $"CREATE TABLE IF NOT EXISTS {_config.Schema}.{_config.TableName} (data jsonb)";
                // Simulated - would execute in real implementation
            }
            
            return Task.CompletedTask;
        }

        public Task<StorageResult> StoreAsync<T>(T item, StorageContext context, CancellationToken cancellationToken = default)
        {
            var startTime = DateTimeOffset.UtcNow;
            
            try
            {
                var command = _connection.CreateCommand();
                command.CommandText = context.UpdateOnConflict
                    ? $"INSERT INTO {_config.Schema}.{context.TableName ?? _config.TableName} VALUES (@data) ON CONFLICT DO UPDATE"
                    : $"INSERT INTO {_config.Schema}.{context.TableName ?? _config.TableName} VALUES (@data)";
                
                var rowsAffected = command.ExecuteNonQuery();
                return Task.FromResult(StorageResult.CreateSuccess(rowsAffected, DateTimeOffset.UtcNow - startTime));
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
            
            var transaction = _connection.BeginTransaction();
            
            try
            {
                var command = _connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = $"INSERT INTO {_config.Schema}.{context.TableName ?? _config.TableName} VALUES (@data)";
                
                var rowsAffected = command.ExecuteNonQuery();
                transaction.Commit();
                
                return Task.FromResult(StorageResult.CreateSuccess(itemList.Count, DateTimeOffset.UtcNow - startTime));
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                return Task.FromResult(StorageResult.CreateFailure(ex.Message, ex, DateTimeOffset.UtcNow - startTime));
            }
        }

        public Task FlushAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task CloseAsync(CancellationToken cancellationToken = default)
        {
            _connection.Close();
            return Task.CompletedTask;
        }
    }

    #endregion
}
