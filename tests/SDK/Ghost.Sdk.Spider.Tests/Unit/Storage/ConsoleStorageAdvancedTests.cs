using FluentAssertions;
using Ghost.Sdk.Spider.Storage.Contracts;
using Ghost.Sdk.Spider.Storage.Sinks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using System.Text;

namespace Ghost.Sdk.Spider.Tests.Unit.Storage;

/// <summary>
/// Advanced tests for ConsoleStorage covering edge cases and formatting scenarios.
/// </summary>
public class ConsoleStorageAdvancedTests : IDisposable
{
    private readonly ConsoleStorage _storage;
    private readonly StringWriter _consoleOutput;
    private readonly TextWriter _originalOutput;

    public ConsoleStorageAdvancedTests()
    {
        _storage = new ConsoleStorage(NullLogger<ConsoleStorage>.Instance);
        _consoleOutput = new StringWriter();
        _originalOutput = Console.Out;
        Console.SetOut(_consoleOutput);
    }

    public void Dispose()
    {
        Console.SetOut(_originalOutput);
        _consoleOutput.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task StoreAsync_WithComplexNestedObject_ShouldFormatCorrectly()
    {
        // Arrange
        var item = new
        {
            Id = 1,
            Name = "Complex Item",
            Details = new
            {
                Description = "Nested description",
                Tags = new[] { "tag1", "tag2", "tag3" }
            },
            Metadata = new Dictionary<string, object>
            {
                ["key1"] = "value1",
                ["key2"] = 42,
                ["nested"] = new { Inner = "value" }
            }
        };
        var context = new StorageContext
        {
            SpiderName = "ComplexSpider",
            SourceUrl = "https://example.com",
            Timestamp = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero)
        };

        // Act
        var result = await _storage.StoreAsync(item, context);

        // Assert
        result.Success.Should().BeTrue();
        var output = _consoleOutput.ToString();

        output.Should().Contain("ComplexSpider");
        output.Should().Contain("https://example.com");
        output.Should().Contain("Complex Item");
        output.Should().Contain("Nested description");
        output.Should().Contain("tag1");
        output.Should().Contain("key1");
        output.Should().Contain("Inner");
    }

    [Fact]
    public async Task StoreAsync_WithSpecialCharacters_ShouldHandleCorrectly()
    {
        // Arrange
        var item = new
        {
            Name = "Test \"Quoted\" Name",
            Description = "Line1\nLine2\tTabbed",
            Special = "Special: @#$%^&*()",
            Unicode = "Unicode: 你好世界 🎉"
        };
        var context = StorageContext.Create("SpecialCharsSpider");

        // Act
        var result = await _storage.StoreAsync(item, context);

        // Assert
        result.Success.Should().BeTrue();
        var output = _consoleOutput.ToString();

        output.Should().Contain("Quoted");
        output.Should().Contain("Line1");
        output.Should().Contain("Special:");
        output.Should().Contain("Unicode:");
    }

    [Fact]
    public async Task StoreAsync_WithLargeObject_ShouldHandleCorrectly()
    {
        // Arrange
        var item = new
        {
            Id = 1,
            LargeText = new string('A', 10000),
            Items = Enumerable.Range(1, 100).Select(i => new { Id = i }).ToList()
        };
        var context = StorageContext.Create("LargeObjectSpider");

        // Act
        var result = await _storage.StoreAsync(item, context);

        // Assert
        result.Success.Should().BeTrue();
        result.ItemsStored.Should().Be(1);
    }

    [Fact]
    public async Task StoreAsync_WithDateTime_ShouldFormatCorrectly()
    {
        // Arrange
        var item = new
        {
            Created = new DateTime(2024, 1, 15, 10, 30, 45),
            Modified = new DateTimeOffset(2024, 2, 20, 14, 15, 30, TimeSpan.Zero)
        };
        var context = new StorageContext
        {
            SpiderName = "DateTimeSpider",
            Timestamp = new DateTimeOffset(2024, 3, 1, 9, 0, 0, TimeSpan.Zero)
        };

        // Act
        var result = await _storage.StoreAsync(item, context);

        // Assert
        result.Success.Should().BeTrue();
        var output = _consoleOutput.ToString();

        output.Should().Contain("2024-03-01 09:00:00");
        output.Should().Contain("Created");
        output.Should().Contain("Modified");
    }

    [Fact]
    public async Task StoreBatchAsync_ShouldShowBatchHeader()
    {
        // Arrange
        var items = new[]
        {
            new { Id = 1, Name = "Item1" },
            new { Id = 2, Name = "Item2" }
        };
        var context = new StorageContext
        {
            SpiderName = "BatchHeaderSpider",
            BatchId = "test-batch-123",
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        var result = await _storage.StoreBatchAsync(items, context);

        // Assert
        result.Success.Should().BeTrue();
        result.ItemsStored.Should().Be(2);

        var output = _consoleOutput.ToString();
        output.Should().Contain("test-batch-123");
        output.Should().Contain("BatchHeaderSpider");
        output.Should().Contain("Items: 2");
    }

    [Fact]
    public async Task StoreAsync_ShouldIncludeDecorators()
    {
        // Arrange
        var item = new { Name = "Test" };
        var context = StorageContext.Create("TestSpider");

        // Act
        await _storage.StoreAsync(item, context);

        // Assert
        var output = _consoleOutput.ToString();

        // Should contain separator lines
        var lines = output.Split(Environment.NewLine);
        lines.Should().Contain(line => line.StartsWith("=") && line.Length >= 80);
        lines.Should().Contain(line => line.StartsWith("-") && line.Length >= 80);
    }

    [Fact]
    public async Task StoreAsync_WithEmptyObject_ShouldSucceed()
    {
        // Arrange
        var item = new { };
        var context = StorageContext.Create("EmptyObjectSpider");

        // Act
        var result = await _storage.StoreAsync(item, context);

        // Assert
        result.Success.Should().BeTrue();
        result.ItemsStored.Should().Be(1);
    }

    [Fact]
    public async Task StoreBatchAsync_WithSingleItem_ShouldProcessCorrectly()
    {
        // Arrange
        var items = new[] { new { Id = 1, Name = "Single" } };
        var context = StorageContext.Create("SingleItemBatch");

        // Act
        var result = await _storage.StoreBatchAsync(items, context);

        // Assert
        result.Success.Should().BeTrue();
        result.ItemsStored.Should().Be(1);
    }

    [Fact]
    public async Task StoreAsync_WithNumericTypes_ShouldFormatCorrectly()
    {
        // Arrange
        var item = new
        {
            IntValue = 42,
            LongValue = 9876543210L,
            DoubleValue = 3.14159,
            DecimalValue = 99.99m,
            FloatValue = 1.23f
        };
        var context = StorageContext.Create("NumericSpider");

        // Act
        var result = await _storage.StoreAsync(item, context);

        // Assert
        result.Success.Should().BeTrue();
        var output = _consoleOutput.ToString();

        output.Should().Contain("42");
        output.Should().Contain("9876543210");
        output.Should().Contain("3.14159");
        output.Should().Contain("99.99");
    }

    [Fact]
    public async Task StoreAsync_WithBooleanValues_ShouldFormatCorrectly()
    {
        // Arrange
        var item = new
        {
            IsActive = true,
            IsDeleted = false,
            HasValue = true
        };
        var context = StorageContext.Create("BooleanSpider");

        // Act
        var result = await _storage.StoreAsync(item, context);

        // Assert
        result.Success.Should().BeTrue();
        var output = _consoleOutput.ToString();

        output.Should().Contain("IsActive");
        output.Should().Contain("IsDeleted");
    }

    [Fact]
    public async Task StoreAsync_WithArrays_ShouldFormatCorrectly()
    {
        // Arrange
        var item = new
        {
            StringArray = new[] { "one", "two", "three" },
            IntArray = new[] { 1, 2, 3, 4, 5 },
            MixedArray = new object[] { "text", 42, true }
        };
        var context = StorageContext.Create("ArraySpider");

        // Act
        var result = await _storage.StoreAsync(item, context);

        // Assert
        result.Success.Should().BeTrue();
        var output = _consoleOutput.ToString();

        output.Should().Contain("one");
        output.Should().Contain("three");
    }

    [Fact]
    public async Task StoreBatchAsync_WithManyItems_ShouldProcessAll()
    {
        // Arrange
        var items = Enumerable.Range(1, 50)
            .Select(i => new { Id = i, Name = $"Item{i}" })
            .ToArray();
        var context = new StorageContext
        {
            SpiderName = "ManyItemsSpider",
            BatchId = "large-batch"
        };

        // Act
        var result = await _storage.StoreBatchAsync(items, context);

        // Assert
        result.Success.Should().BeTrue();
        result.ItemsStored.Should().Be(50);

        var output = _consoleOutput.ToString();
        output.Should().Contain("Items: 50");
        output.Should().Contain("Item1");
        output.Should().Contain("Item50");
    }

    [Fact]
    public async Task StoreAsync_WithContextMetadata_ShouldNotFailIfNotDisplayed()
    {
        // Arrange
        var item = new { Name = "Test" };
        var context = new StorageContext
        {
            SpiderName = "MetadataSpider",
            SourceUrl = "https://example.com",
            Metadata = new Dictionary<string, object>
            {
                ["key1"] = "value1",
                ["key2"] = 42
            },
            Tags = new List<string> { "tag1", "tag2" }
        };

        // Act
        var result = await _storage.StoreAsync(item, context);

        // Assert
        result.Success.Should().BeTrue();
        var output = _consoleOutput.ToString();
        output.Should().Contain("MetadataSpider");
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldSucceed()
    {
        // Act
        var storage = new ConsoleStorage(null);

        // Assert
        storage.Should().NotBeNull();
        storage.Name.Should().Be("Console");
        storage.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task StoreAsync_ShouldRecordDuration()
    {
        // Arrange
        var item = new { Name = "Test" };
        var context = StorageContext.Create("TestSpider");

        // Act
        var result = await _storage.StoreAsync(item, context);

        // Assert
        result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task StoreBatchAsync_ShouldRecordDuration()
    {
        // Arrange
        var items = new[] { new { Id = 1 }, new { Id = 2 } };
        var context = StorageContext.Create("TestSpider");

        // Act
        var result = await _storage.StoreBatchAsync(items, context);

        // Assert
        result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
    }
}
