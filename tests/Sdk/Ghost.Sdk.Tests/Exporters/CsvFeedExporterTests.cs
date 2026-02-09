using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Ghost.Sdk.Exporters;
using Xunit;

namespace Ghost.Sdk.Tests.Exporters;

public sealed class CsvFeedExporterTests
{
    private sealed record TestItem(string Name, int Value, DateTime Created);

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExportAsync_WithValidItems_WritesCsvWithHeaders()
    {
        // Arrange
        var exporter = new CsvFeedExporter();
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

        lines.Should().HaveCount(3); // Header + 2 data rows
        lines[0].Should().Be("Name,Value,Created");
        lines[1].Should().Contain("Item1");
        lines[1].Should().Contain("100");
        lines[2].Should().Contain("Item2");
        lines[2].Should().Contain("200");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExportAsync_WithoutHeaders_WritesOnlyData()
    {
        // Arrange
        var exporter = new CsvFeedExporter(includeHeaders: false);
        var items = new[] { new TestItem("Test", 1, DateTime.Now) };

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

        lines.Should().HaveCount(1); // Only data row
        lines[0].Should().NotContain("Name,Value,Created");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExportAsync_WithCommasInData_QuotesField()
    {
        // Arrange
        var exporter = new CsvFeedExporter();
        var items = new[] { new TestItem("Item, with comma", 100, DateTime.Now) };

        using var stream = new MemoryStream();

        // Act
        await exporter.ExportAsync(items, stream);

        // Assert
        stream.Position = 0;
        using var reader = new StreamReader(stream);
        await reader.ReadLineAsync(); // Skip header
        var dataLine = await reader.ReadLineAsync();

        dataLine.Should().Contain("\"Item, with comma\"");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExportAsync_WithQuotesInData_EscapesQuotes()
    {
        // Arrange
        var exporter = new CsvFeedExporter();
        var items = new[] { new TestItem("Item \"quoted\"", 100, DateTime.Now) };

        using var stream = new MemoryStream();

        // Act
        await exporter.ExportAsync(items, stream);

        // Assert
        stream.Position = 0;
        using var reader = new StreamReader(stream);
        await reader.ReadLineAsync(); // Skip header
        var dataLine = await reader.ReadLineAsync();

        dataLine.Should().Contain("\"Item \"\"quoted\"\"\"");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExportAsync_WithNewlinesInData_QuotesField()
    {
        // Arrange
        var exporter = new CsvFeedExporter();
        var items = new[] { new TestItem("Item\nwith\nnewlines", 100, DateTime.Now) };

        using var stream = new MemoryStream();

        // Act
        await exporter.ExportAsync(items, stream);

        // Assert
        stream.Position = 0;
        using var reader = new StreamReader(stream);
        await reader.ReadLineAsync(); // Skip header
        var remainingContent = await reader.ReadToEndAsync();

        remainingContent.Should().Contain("\"Item\nwith\nnewlines\"");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExportAsync_WithCustomDelimiter_UsesDelimiter()
    {
        // Arrange
        var exporter = new CsvFeedExporter(delimiter: ";");
        var items = new[] { new TestItem("Item1", 100, new DateTime(2024, 1, 1)) };

        using var stream = new MemoryStream();

        // Act
        await exporter.ExportAsync(items, stream);

        // Assert
        stream.Position = 0;
        using var reader = new StreamReader(stream);
        var header = await reader.ReadLineAsync();

        header.Should().Be("Name;Value;Created");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExportAsync_WithEmptyCollection_WritesNothing()
    {
        // Arrange
        var exporter = new CsvFeedExporter();
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
        var exporter = new CsvFeedExporter();
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
        var exporter = new CsvFeedExporter();
        var items = new[] { new TestItem("Test", 1, DateTime.Now) };

        // Act
        var act = () => exporter.ExportAsync(items, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Format_ReturnsCsv()
    {
        // Arrange
        var exporter = new CsvFeedExporter();

        // Act
        var format = exporter.Format;

        // Assert
        format.Should().Be("csv");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExportAsync_LeavesStreamOpen()
    {
        // Arrange
        var exporter = new CsvFeedExporter();
        var items = new[] { new TestItem("Test", 1, DateTime.Now) };
        using var stream = new MemoryStream();

        // Act
        await exporter.ExportAsync(items, stream);

        // Assert
        stream.CanRead.Should().BeTrue();
        stream.CanWrite.Should().BeTrue();
    }
}
