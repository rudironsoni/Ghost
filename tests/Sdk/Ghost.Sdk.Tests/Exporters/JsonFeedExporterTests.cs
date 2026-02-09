using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Ghost.Sdk.Exporters;
using Xunit;

namespace Ghost.Sdk.Tests.Exporters;

public sealed class JsonFeedExporterTests
{
    private sealed record TestItem(string Name, int Value, DateTime Created);

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExportAsync_WithValidItems_WritesJsonLines()
    {
        // Arrange
        var exporter = new JsonFeedExporter();
        var items = new[]
        {
            new TestItem("Item1", 100, new DateTime(2024, 1, 1)),
            new TestItem("Item2", 200, new DateTime(2024, 1, 2))
        };

        using var stream = new MemoryStream();

        // Act
        await exporter.ExportAsync(items, stream);

        // Assert
        stream.Position = 0;
        using var reader = new StreamReader(stream);
        var lines = new List<string>();
        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            lines.Add(line);
        }

        lines.Should().HaveCount(2);

        var item1 = JsonSerializer.Deserialize<TestItem>(lines[0]);
        item1.Should().NotBeNull();
        item1!.Name.Should().Be("Item1");
        item1.Value.Should().Be(100);

        var item2 = JsonSerializer.Deserialize<TestItem>(lines[1]);
        item2.Should().NotBeNull();
        item2!.Name.Should().Be("Item2");
        item2.Value.Should().Be(200);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExportAsync_WithEmptyCollection_WritesNothing()
    {
        // Arrange
        var exporter = new JsonFeedExporter();
        var items = Array.Empty<TestItem>();

        using var stream = new MemoryStream();

        // Act
        await exporter.ExportAsync(items, stream);

        // Assert
        stream.Length.Should().Be(0);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExportAsync_WithNullItems_ThrowsArgumentNullException()
    {
        // Arrange
        var exporter = new JsonFeedExporter();
        using var stream = new MemoryStream();

        // Act
        var act = () => exporter.ExportAsync<TestItem>(null!, stream);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExportAsync_WithNullStream_ThrowsArgumentNullException()
    {
        // Arrange
        var exporter = new JsonFeedExporter();
        var items = new[] { new TestItem("Test", 1, DateTime.Now) };

        // Act
        var act = () => exporter.ExportAsync(items, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Format_ReturnsJsonl()
    {
        // Arrange
        var exporter = new JsonFeedExporter();

        // Act
        var format = exporter.Format;

        // Assert
        format.Should().Be("jsonl");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExportAsync_LeavesStreamOpen()
    {
        // Arrange
        var exporter = new JsonFeedExporter();
        var items = new[] { new TestItem("Test", 1, DateTime.Now) };
        using var stream = new MemoryStream();

        // Act
        await exporter.ExportAsync(items, stream);

        // Assert
        stream.CanRead.Should().BeTrue();
        stream.CanWrite.Should().BeTrue();
    }
}
