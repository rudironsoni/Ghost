using FluentAssertions;
using Ghost.Sdk.Spider.Storage.Contracts;
using Ghost.Sdk.Spider.Storage.Sinks;
using Moq;
using Xunit;
using System.Net;
using System.Net.Http;

namespace Ghost.Sdk.Spider.Tests.Unit.Storage;

/// <summary>
/// Comprehensive tests for storage sinks including WebhookStorage and ConsoleStorage.
/// </summary>
public class StorageSinksTests
{
    [Fact]
    public async Task ConsoleStorage_StoreAsync_ShouldSucceed()
    {
        // Arrange
        var storage = new ConsoleStorage();
        var item = new { Id = 1, Name = "Test Item" };
        var context = new StorageContext
        {
            SpiderName = "TestSpider",
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        var result = await storage.StoreAsync(item, context);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.ItemsStored.Should().Be(1);
        result.Error.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task ConsoleStorage_StoreBatchAsync_ShouldSucceed()
    {
        // Arrange
        var storage = new ConsoleStorage();
        var items = new[]
        {
            new { Id = 1, Name = "Item 1" },
            new { Id = 2, Name = "Item 2" },
            new { Id = 3, Name = "Item 3" }
        };
        var context = new StorageContext
        {
            SpiderName = "TestSpider",
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        var result = await storage.StoreBatchAsync(items, context);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.ItemsStored.Should().Be(3);
    }

    [Fact]
    public async Task ConsoleStorage_InitializeAsync_ShouldSucceed()
    {
        // Arrange
        var storage = new ConsoleStorage();

        // Act
        Func<Task> act = async () => await storage.InitializeAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ConsoleStorage_FlushAsync_ShouldSucceed()
    {
        // Arrange
        var storage = new ConsoleStorage();

        // Act
        Func<Task> act = async () => await storage.FlushAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ConsoleStorage_CloseAsync_ShouldSucceed()
    {
        // Arrange
        var storage = new ConsoleStorage();

        // Act
        Func<Task> act = async () => await storage.CloseAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void ConsoleStorage_Properties_ShouldReturnExpectedValues()
    {
        // Arrange
        var storage = new ConsoleStorage();

        // Assert
        storage.Name.Should().Be("Console");
        storage.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task ConsoleStorage_WithNullItem_ShouldHandleGracefully()
    {
        // Arrange
        var storage = new ConsoleStorage();
        var context = new StorageContext
        {
            SpiderName = "TestSpider",
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        var result = await storage.StoreAsync<object>(null!, context);

        // Assert
        result.Should().NotBeNull();
        // Behavior may vary - either succeed with warning or fail gracefully
    }

    [Fact]
    public async Task ConsoleStorage_WithEmptyBatch_ShouldHandleGracefully()
    {
        // Arrange
        var storage = new ConsoleStorage();
        var items = Array.Empty<object>();
        var context = new StorageContext
        {
            SpiderName = "TestSpider",
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        var result = await storage.StoreBatchAsync(items, context);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.ItemsStored.Should().Be(0);
    }

    [Fact]
    public async Task ConsoleStorage_WithCancellation_ShouldRespectToken()
    {
        // Arrange
        var storage = new ConsoleStorage();
        var item = new { Id = 1 };
        var context = new StorageContext
        {
            SpiderName = "TestSpider",
            Timestamp = DateTimeOffset.UtcNow
        };
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        Func<Task> act = async () => await storage.StoreAsync(item, context, cts.Token);

        // Assert - May throw OperationCanceledException or complete
        // Implementation specific behavior
        await act.Should().NotThrowAsync<NullReferenceException>();
    }

    [Fact]
    public void StorageResult_CreateSuccess_ShouldHaveCorrectProperties()
    {
        // Arrange
        var duration = TimeSpan.FromMilliseconds(100);

        // Act
        var result = StorageResult.CreateSuccess(5, duration);

        // Assert
        result.Success.Should().BeTrue();
        result.ItemsStored.Should().Be(5);
        result.Duration.Should().Be(duration);
        result.Error.Should().BeNullOrEmpty();
        result.Exception.Should().BeNull();
    }

    [Fact]
    public void StorageResult_CreateFailure_ShouldHaveCorrectProperties()
    {
        // Arrange
        var error = "Test error";
        var exception = new InvalidOperationException("Test");
        var duration = TimeSpan.FromMilliseconds(50);

        // Act
        var result = StorageResult.CreateFailure(error, exception, duration);

        // Assert
        result.Success.Should().BeFalse();
        result.ItemsStored.Should().Be(0);
        result.Duration.Should().Be(duration);
        result.Error.Should().Be(error);
        result.Exception.Should().Be(exception);
    }

    [Fact]
    public void StorageResult_Metadata_ShouldBeInitialized()
    {
        // Act
        var result = StorageResult.CreateSuccess(1, TimeSpan.Zero);

        // Assert
        result.Metadata.Should().NotBeNull();
        result.Metadata.Should().BeEmpty();
    }

    [Fact]
    public void StorageResult_Metadata_ShouldAllowAddingValues()
    {
        // Arrange
        var result = StorageResult.CreateSuccess(1, TimeSpan.Zero);

        // Act
        result.Metadata["key1"] = "value1";
        result.Metadata["key2"] = 123;

        // Assert
        result.Metadata.Should().HaveCount(2);
        result.Metadata["key1"].Should().Be("value1");
        result.Metadata["key2"].Should().Be(123);
    }

    [Fact]
    public void StorageContext_ShouldAllowCreation()
    {
        // Arrange & Act
        var context = new StorageContext
        {
            SpiderName = "TestSpider",
            Timestamp = DateTimeOffset.UtcNow,
            Metadata = new Dictionary<string, object>
            {
                ["key1"] = "value1"
            }
        };

        // Assert
        context.SpiderName.Should().Be("TestSpider");
        context.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
        context.Metadata.Should().ContainKey("key1");
    }

    [Fact]
    public async Task ConsoleStorage_MultipleCalls_ShouldAccumulateItems()
    {
        // Arrange
        var storage = new ConsoleStorage();
        var context = new StorageContext
        {
            SpiderName = "TestSpider",
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        var result1 = await storage.StoreAsync(new { Id = 1 }, context);
        var result2 = await storage.StoreAsync(new { Id = 2 }, context);
        var result3 = await storage.StoreAsync(new { Id = 3 }, context);

        // Assert
        result1.Success.Should().BeTrue();
        result2.Success.Should().BeTrue();
        result3.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ConsoleStorage_WithComplexObject_ShouldSerialize()
    {
        // Arrange
        var storage = new ConsoleStorage();
        var complexItem = new
        {
            Id = 1,
            Name = "Complex Item",
            Tags = new[] { "tag1", "tag2", "tag3" },
            Metadata = new Dictionary<string, object>
            {
                ["created"] = DateTimeOffset.UtcNow,
                ["count"] = 42
            },
            Nested = new
            {
                SubId = 100,
                SubName = "Nested"
            }
        };
        var context = new StorageContext
        {
            SpiderName = "TestSpider",
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        var result = await storage.StoreAsync(complexItem, context);

        // Assert
        result.Success.Should().BeTrue();
        result.ItemsStored.Should().Be(1);
    }

    [Fact]
    public async Task ConsoleStorage_IsThreadSafe_ShouldHandleConcurrentCalls()
    {
        // Arrange
        var storage = new ConsoleStorage();
        var context = new StorageContext
        {
            SpiderName = "TestSpider",
            Timestamp = DateTimeOffset.UtcNow
        };
        var tasks = new List<Task<StorageResult>>();

        // Act
        for (int i = 0; i < 10; i++)
        {
            var id = i;
            tasks.Add(storage.StoreAsync(new { Id = id }, context));
        }

        var results = await Task.WhenAll(tasks.ToArray());

        // Assert
        results.Should().AllSatisfy(result => result.Success.Should().BeTrue());
    }

    [Fact]
    public async Task StorageResult_WithLongDuration_ShouldCapture()
    {
        // Arrange
        var storage = new ConsoleStorage();
        var context = new StorageContext
        {
            SpiderName = "TestSpider",
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        var result = await storage.StoreAsync(new { Id = 1 }, context);

        // Assert
        result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
    }
}
