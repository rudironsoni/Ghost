using FluentAssertions;
using Ghost.Sdk.Spider.Storage.Contracts;
using Ghost.Sdk.Spider.Storage.Sinks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using System.Text;

namespace Ghost.Sdk.Spider.Tests.Unit.Storage;

public class ConsoleStorageTests : IDisposable
{
    private readonly ConsoleStorage _storage;
    private readonly StringWriter _consoleOutput;
    private readonly TextWriter _originalOutput;

    public ConsoleStorageTests()
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
    public void Name_ShouldReturnConsole()
    {
        // Act
        var name = _storage.Name;

        // Assert
        name.Should().Be("Console");
    }

    [Fact]
    public void IsAvailable_ShouldReturnTrue()
    {
        // Act
        var isAvailable = _storage.IsAvailable;

        // Assert
        isAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task InitializeAsync_ShouldComplete()
    {
        // Act
        var act = async () => await _storage.InitializeAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task StoreAsync_ShouldWriteToConsole()
    {
        // Arrange
        var item = new { Name = "Test", Value = 42 };
        var context = new StorageContext
        {
            SpiderName = "TestSpider",
            SourceUrl = "https://example.com",
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        var result = await _storage.StoreAsync(item, context);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.ItemsStored.Should().Be(1);

        var output = _consoleOutput.ToString();
        output.Should().Contain("TestSpider");
        output.Should().Contain("https://example.com");
        output.Should().Contain("Test");
        output.Should().Contain("42");
    }

    [Fact]
    public async Task StoreBatchAsync_ShouldWriteAllItems()
    {
        // Arrange
        var items = new[]
        {
            new { Name = "Item1", Value = 1 },
            new { Name = "Item2", Value = 2 },
            new { Name = "Item3", Value = 3 }
        };
        var context = new StorageContext
        {
            SpiderName = "BatchSpider",
            SourceUrl = "https://example.com/batch",
            Timestamp = DateTimeOffset.UtcNow,
            BatchId = "batch-123"
        };

        // Act
        var result = await _storage.StoreBatchAsync(items, context);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.ItemsStored.Should().Be(3);

        var output = _consoleOutput.ToString();
        output.Should().Contain("BatchSpider");
        output.Should().Contain("batch-123");
        output.Should().Contain("Item1");
        output.Should().Contain("Item2");
        output.Should().Contain("Item3");
    }

    [Fact]
    public async Task FlushAsync_ShouldComplete()
    {
        // Act
        var act = async () => await _storage.FlushAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task CloseAsync_ShouldComplete()
    {
        // Act
        var act = async () => await _storage.CloseAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task StoreAsync_WithNullValues_ShouldHandle()
    {
        // Arrange
        var item = new { Name = (string?)null, Value = 0 };
        var context = new StorageContext
        {
            SpiderName = "TestSpider",
            SourceUrl = "https://example.com",
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        var result = await _storage.StoreAsync(item, context);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task StoreBatchAsync_WithEmptyList_ShouldSucceed()
    {
        // Arrange
        var items = Array.Empty<object>();
        var context = new StorageContext
        {
            SpiderName = "EmptyBatchSpider",
            SourceUrl = "https://example.com",
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        var result = await _storage.StoreBatchAsync(items, context);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.ItemsStored.Should().Be(0);
    }
}
