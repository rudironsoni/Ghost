using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using FluentAssertions;
using Ghost.Sdk.Exporters;
using Xunit;

namespace Ghost.Sdk.Tests.Exporters;

public sealed class XmlFeedExporterTests
{
    private sealed record TestItem(string Name, int Value, DateTime Created);

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExportAsync_WithValidItems_WritesValidXml()
    {
        // Arrange
        var exporter = new XmlFeedExporter();
        var items = new[]
        {
            new TestItem("Item1", 100, new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
            new TestItem("Item2", 200, new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc))
        };

        using var stream = new MemoryStream();

        // Act
        await exporter.ExportAsync(items, stream);

        // Assert
        stream.Position = 0;
        var document = await XDocument.LoadAsync(stream, LoadOptions.None, default);

        document.Should().NotBeNull();
        document.Root.Should().NotBeNull();
        document.Root!.Name.LocalName.Should().Be("items");

        var itemElements = document.Root.Elements("test").ToList();
        itemElements.Should().HaveCount(2);

        var firstItem = itemElements[0];
        firstItem.Element("name")?.Value.Should().Be("Item1");
        firstItem.Element("value")?.Value.Should().Be("100");

        var secondItem = itemElements[1];
        secondItem.Element("name")?.Value.Should().Be("Item2");
        secondItem.Element("value")?.Value.Should().Be("200");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExportAsync_WithCustomRootElement_UsesCustomRoot()
    {
        // Arrange
        var exporter = new XmlFeedExporter(rootElementName: "results");
        var items = new[] { new TestItem("Test", 1, DateTime.Now) };

        using var stream = new MemoryStream();

        // Act
        await exporter.ExportAsync(items, stream);

        // Assert
        stream.Position = 0;
        var document = await XDocument.LoadAsync(stream, LoadOptions.None, default);

        document.Root.Should().NotBeNull();
        document.Root!.Name.LocalName.Should().Be("results");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExportAsync_WithSpecialCharacters_EscapesProperly()
    {
        // Arrange
        var exporter = new XmlFeedExporter();
        var items = new[] { new TestItem("Item <with> & \"special\" 'chars'", 100, DateTime.Now) };

        using var stream = new MemoryStream();

        // Act
        await exporter.ExportAsync(items, stream);

        // Assert
        stream.Position = 0;
        var document = await XDocument.LoadAsync(stream, LoadOptions.None, default);

        var nameElement = document.Root!.Elements("test").First().Element("name");
        nameElement.Should().NotBeNull();
        nameElement!.Value.Should().Be("Item <with> & \"special\" 'chars'");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExportAsync_WithEmptyCollection_WritesEmptyRoot()
    {
        // Arrange
        var exporter = new XmlFeedExporter();
        var items = Array.Empty<TestItem>();

        using var stream = new MemoryStream();

        // Act
        await exporter.ExportAsync(items, stream);

        // Assert
        stream.Position = 0;
        var document = await XDocument.LoadAsync(stream, LoadOptions.None, default);

        document.Root.Should().NotBeNull();
        document.Root!.Name.LocalName.Should().Be("items");
        document.Root.Elements().Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExportAsync_WithNullItems_ThrowsArgumentNullException()
    {
        // Arrange
        var exporter = new XmlFeedExporter();
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
        var exporter = new XmlFeedExporter();
        var items = new[] { new TestItem("Test", 1, DateTime.Now) };

        // Act
        var act = () => exporter.ExportAsync(items, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Format_ReturnsXml()
    {
        // Arrange
        var exporter = new XmlFeedExporter();

        // Act
        var format = exporter.Format;

        // Assert
        format.Should().Be("xml");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExportAsync_LeavesStreamOpen()
    {
        // Arrange
        var exporter = new XmlFeedExporter();
        var items = new[] { new TestItem("Test", 1, DateTime.Now) };
        using var stream = new MemoryStream();

        // Act
        await exporter.ExportAsync(items, stream);

        // Assert
        stream.CanRead.Should().BeTrue();
        stream.CanWrite.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExportAsync_WithIndentDisabled_DoesNotIndent()
    {
        // Arrange
        var exporter = new XmlFeedExporter(indent: false);
        var items = new[] { new TestItem("Test", 1, DateTime.Now) };

        using var stream = new MemoryStream();

        // Act
        await exporter.ExportAsync(items, stream);

        // Assert
        stream.Position = 0;
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();

        // Unindented XML should not have multiple lines with spacing
        content.Should().NotContain("  <"); // No indentation with spaces
    }

    private sealed record ComplexItem(string Name, List<string> Tags);

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExportAsync_WithCollectionProperty_SerializesAsList()
    {
        // Arrange
        var exporter = new XmlFeedExporter();
        var items = new[] { new ComplexItem("Test", new List<string> { "tag1", "tag2", "tag3" }) };

        using var stream = new MemoryStream();

        // Act
        await exporter.ExportAsync(items, stream);

        // Assert
        stream.Position = 0;
        var document = await XDocument.LoadAsync(stream, LoadOptions.None, default);

        var tagsElement = document.Root!.Elements("complex").First().Element("tags");
        tagsElement.Should().NotBeNull();
        tagsElement!.Elements("item").Should().HaveCount(3);
    }
}
