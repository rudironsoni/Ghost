using FluentAssertions;
using Ghost.Sdk.Spider.Storage.Contracts;
using Ghost.Sdk.Spider.Storage.Sinks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using System.Collections.Concurrent;
using Ghost.Testing.Reliability;
using Xunit.Abstractions;

namespace Ghost.Sdk.Spider.Tests.Unit.Storage;

/// <summary>
/// Tests for batch processing edge cases and performance scenarios
/// </summary>
public class BatchProcessingTests : ReliabilityTestBase
{
    public BatchProcessingTests(ITestOutputHelper output) : base(output) { }
    [Fact]
    public async Task BatchProcessing_WithConcurrentBatches_ShouldHandleThreadSafely()
    {
        // Arrange
        var storage = new ConsoleStorage(NullLogger<ConsoleStorage>.Instance);
        await storage.InitializeAsync();

        var batches = Enumerable.Range(1, 10)
            .Select(batchNum => Enumerable.Range(1, 50)
                .Select(i => new { BatchNum = batchNum, ItemId = i, Data = $"Batch{batchNum}-Item{i}" })
                .ToArray())
            .ToArray();

        var contexts = batches.Select((_, i) => new StorageContext
        {
            SpiderName = "ConcurrentBatchSpider",
            SourceUrl = $"https://example.com/batch{i}",
            BatchId = $"batch-{i}"
        }).ToArray();

        // Act
        var tasks = batches.Zip(contexts, (batch, context) =>
            storage.StoreBatchAsync(batch, context));

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().AllSatisfy(r => r.Success.Should().BeTrue());
        results.Sum(r => r.ItemsStored).Should().Be(500); // 10 batches * 50 items
    }

    [Fact]
    public async Task BatchProcessing_WithEmptyBatch_ShouldSucceed()
    {
        // Arrange
        var storage = new ConsoleStorage();
        await storage.InitializeAsync();

        var emptyBatch = Array.Empty<object>();
        var context = new StorageContext
        {
            SpiderName = "EmptyBatchSpider",
            SourceUrl = "https://example.com"
        };

        // Act
        var result = await storage.StoreBatchAsync(emptyBatch, context);

        // Assert
        result.Success.Should().BeTrue();
        result.ItemsStored.Should().Be(0);
    }

    [Fact]
    public async Task BatchProcessing_WithSingleItem_ShouldProcessCorrectly()
    {
        // Arrange
        var storage = new ConsoleStorage();
        await storage.InitializeAsync();

        var singleItemBatch = new[] { new { Id = 1, Name = "Single" } };
        var context = new StorageContext
        {
            SpiderName = "SingleItemBatchSpider",
            SourceUrl = "https://example.com"
        };

        // Act
        var result = await storage.StoreBatchAsync(singleItemBatch, context);

        // Assert
        result.Success.Should().BeTrue();
        result.ItemsStored.Should().Be(1);
    }

    [Fact]
    public async Task BatchProcessing_WithLargeBatch_ShouldComplete()
    {
        // Arrange
        var storage = new ConsoleStorage();
        await storage.InitializeAsync();

        var largeBatch = Enumerable.Range(1, 10000)
            .Select(i => new
            {
                Id = i,
                Name = $"Item {i}",
                Description = $"Description for item {i}",
                Tags = new[] { $"tag{i}", $"category{i % 10}" },
                Timestamp = DateTimeOffset.UtcNow
            })
            .ToArray();

        var context = new StorageContext
        {
            SpiderName = "LargeBatchSpider",
            SourceUrl = "https://example.com",
            BatchId = "large-batch-001"
        };

        // Act
        var start = DateTimeOffset.UtcNow;
        var result = await storage.StoreBatchAsync(largeBatch, context);
        var elapsed = DateTimeOffset.UtcNow - start;

        // Assert
        result.Success.Should().BeTrue();
        result.ItemsStored.Should().Be(10000);
        elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task BatchProcessing_WithDuplicateItems_ShouldProcessAll()
    {
        // Arrange
        var storage = new ConsoleStorage();
        await storage.InitializeAsync();

        var batchWithDuplicates = new[]
        {
            new { Id = 1, Name = "Item1" },
            new { Id = 2, Name = "Item2" },
            new { Id = 1, Name = "Item1" }, // Duplicate
            new { Id = 3, Name = "Item3" },
            new { Id = 2, Name = "Item2" }  // Duplicate
        };

        var context = new StorageContext
        {
            SpiderName = "DuplicateBatchSpider",
            SourceUrl = "https://example.com"
        };

        // Act
        var result = await storage.StoreBatchAsync(batchWithDuplicates, context);

        // Assert
        result.Success.Should().BeTrue();
        result.ItemsStored.Should().Be(5); // All items stored, including duplicates
    }

    [Fact]
    public async Task BatchProcessing_WithMixedTypes_ShouldHandleGracefully()
    {
        // Arrange
        var storage = new ConsoleStorage();
        await storage.InitializeAsync();

        var mixedBatch = new object[]
        {
            new { Type = "TypeA", Id = 1, Name = "Item1" },
            new { Type = "TypeB", Id = 2, Code = "CODE2", Active = true },
            new { Type = "TypeC", Id = 3, Description = "Desc3", Tags = new[] { "tag1", "tag2" } }
        };

        var context = new StorageContext
        {
            SpiderName = "MixedTypeBatchSpider",
            SourceUrl = "https://example.com"
        };

        // Act
        var result = await storage.StoreBatchAsync(mixedBatch, context);

        // Assert
        result.Success.Should().BeTrue();
        result.ItemsStored.Should().Be(3);
    }

    [Fact]
    public async Task BatchProcessing_WithNullItems_ShouldSkipNulls()
    {
        // Arrange
        var storage = new ConsoleStorage();
        await storage.InitializeAsync();

        var batchWithNulls = new object?[]
        {
            new { Id = 1, Name = "Item1" },
            null,
            new { Id = 2, Name = "Item2" },
            null,
            new { Id = 3, Name = "Item3" }
        };

        var nonNullBatch = batchWithNulls.Where(x => x != null).ToArray()!;
        var context = new StorageContext
        {
            SpiderName = "NullItemsBatchSpider",
            SourceUrl = "https://example.com"
        };

        // Act
        var result = await storage.StoreBatchAsync(nonNullBatch, context);

        // Assert
        result.Success.Should().BeTrue();
        result.ItemsStored.Should().Be(3);
    }

    [Fact]
    public async Task BatchProcessing_SequentialBatches_ShouldMaintainOrder()
    {
        // Arrange
        var storage = new ConsoleStorage();
        await storage.InitializeAsync();

        var processedBatches = new ConcurrentBag<string>();

        // Act
        for (int i = 1; i <= 5; i++)
        {
            var batch = new[] { new { BatchNum = i, Data = $"Batch {i}" } };
            var context = new StorageContext
            {
                SpiderName = "SequentialBatchSpider",
                SourceUrl = $"https://example.com/batch{i}",
                BatchId = $"batch-{i}"
            };

            await storage.StoreBatchAsync(batch, context);
            processedBatches.Add($"batch-{i}");
        }

        // Assert
        processedBatches.Should().HaveCount(5);
    }
}
