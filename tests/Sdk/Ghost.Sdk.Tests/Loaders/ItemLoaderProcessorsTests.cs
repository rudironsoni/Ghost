using FluentAssertions;
using Ghost.Sdk.Loaders;
using Xunit;

namespace Ghost.Sdk.Tests.Loaders;

public sealed class ItemLoaderProcessorsTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Strip_RemovesLeadingAndTrailingWhitespace()
    {
        // Arrange
        var processor = ItemLoaderProcessors.Strip();

        // Act
        var result = processor("  Hello World  ");

        // Assert
        result.Should().Be("Hello World");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Strip_WithNullInput_ReturnsEmptyString()
    {
        // Arrange
        var processor = ItemLoaderProcessors.Strip();

        // Act
        var result = processor(null!);

        // Assert
        result.Should().Be(string.Empty);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Join_ReplacesCommaWithSeparator()
    {
        // Arrange
        var processor = ItemLoaderProcessors.Join(" | ");

        // Act
        var result = processor("apple,banana,cherry");

        // Assert
        result.Should().Be("apple | banana | cherry");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Join_WithNullInput_ReturnsEmptyString()
    {
        // Arrange
        var processor = ItemLoaderProcessors.Join(";");

        // Act
        var result = processor(null!);

        // Assert
        result.Should().Be(string.Empty);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Join_WithNullSeparator_ThrowsArgumentNullException()
    {
        // Act
        var act = () => ItemLoaderProcessors.Join(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Take_TruncatesString()
    {
        // Arrange
        var processor = ItemLoaderProcessors.Take(5);

        // Act
        var result = processor("Hello World");

        // Assert
        result.Should().Be("Hello");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Take_WithStringsShorterThanCount_ReturnsOriginal()
    {
        // Arrange
        var processor = ItemLoaderProcessors.Take(20);

        // Act
        var result = processor("Hello");

        // Assert
        result.Should().Be("Hello");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Take_WithNullInput_ReturnsEmptyString()
    {
        // Arrange
        var processor = ItemLoaderProcessors.Take(5);

        // Act
        var result = processor(null!);

        // Assert
        result.Should().Be(string.Empty);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Take_WithNegativeCount_ThrowsArgumentOutOfRangeException()
    {
        // Act
        var act = () => ItemLoaderProcessors.Take(-1);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Replace_ReplacesSubstring()
    {
        // Arrange
        var processor = ItemLoaderProcessors.Replace("World", "Universe");

        // Act
        var result = processor("Hello World");

        // Assert
        result.Should().Be("Hello Universe");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Replace_WithNullInput_ReturnsEmptyString()
    {
        // Arrange
        var processor = ItemLoaderProcessors.Replace("old", "new");

        // Act
        var result = processor(null!);

        // Assert
        result.Should().Be(string.Empty);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Replace_WithNullOldValue_ThrowsArgumentNullException()
    {
        // Act
        var act = () => ItemLoaderProcessors.Replace(null!, "new");

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Replace_WithNullNewValue_ThrowsArgumentNullException()
    {
        // Act
        var act = () => ItemLoaderProcessors.Replace("old", null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ToLower_ConvertsToLowercase()
    {
        // Arrange
        var processor = ItemLoaderProcessors.ToLower();

        // Act
        var result = processor("HELLO WORLD");

        // Assert
        result.Should().Be("hello world");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ToLower_WithNullInput_ReturnsEmptyString()
    {
        // Arrange
        var processor = ItemLoaderProcessors.ToLower();

        // Act
        var result = processor(null!);

        // Assert
        result.Should().Be(string.Empty);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ToUpper_ConvertsToUppercase()
    {
        // Arrange
        var processor = ItemLoaderProcessors.ToUpper();

        // Act
        var result = processor("hello world");

        // Assert
        result.Should().Be("HELLO WORLD");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ToUpper_WithNullInput_ReturnsEmptyString()
    {
        // Arrange
        var processor = ItemLoaderProcessors.ToUpper();

        // Act
        var result = processor(null!);

        // Assert
        result.Should().Be(string.Empty);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void RegexExtract_ExtractsFirstMatch()
    {
        // Arrange
        var processor = ItemLoaderProcessors.RegexExtract(@"\d+");

        // Act
        var result = processor("Price: $123.45");

        // Assert
        result.Should().Be("123");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void RegexExtract_WithNoMatch_ReturnsEmptyString()
    {
        // Arrange
        var processor = ItemLoaderProcessors.RegexExtract(@"\d+");

        // Act
        var result = processor("No numbers here");

        // Assert
        result.Should().Be(string.Empty);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void RegexExtract_WithNullInput_ReturnsEmptyString()
    {
        // Arrange
        var processor = ItemLoaderProcessors.RegexExtract(@"\d+");

        // Act
        var result = processor(null!);

        // Assert
        result.Should().Be(string.Empty);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void RegexExtract_WithNullPattern_ThrowsArgumentNullException()
    {
        // Act
        var act = () => ItemLoaderProcessors.RegexExtract(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void StripHtml_RemovesHtmlTags()
    {
        // Arrange
        var processor = ItemLoaderProcessors.StripHtml();

        // Act
        var result = processor("<p>Hello <strong>World</strong></p>");

        // Assert
        result.Should().Be("Hello World");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void StripHtml_WithNullInput_ReturnsEmptyString()
    {
        // Arrange
        var processor = ItemLoaderProcessors.StripHtml();

        // Act
        var result = processor(null!);

        // Assert
        result.Should().Be(string.Empty);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void NormalizeWhitespace_NormalizesSpaces()
    {
        // Arrange
        var processor = ItemLoaderProcessors.NormalizeWhitespace();

        // Act
        var result = processor("Hello    World   Test");

        // Assert
        result.Should().Be("Hello World Test");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void NormalizeWhitespace_TrimsLeadingAndTrailing()
    {
        // Arrange
        var processor = ItemLoaderProcessors.NormalizeWhitespace();

        // Act
        var result = processor("  Hello World  ");

        // Assert
        result.Should().Be("Hello World");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void NormalizeWhitespace_WithNullInput_ReturnsEmptyString()
    {
        // Arrange
        var processor = ItemLoaderProcessors.NormalizeWhitespace();

        // Act
        var result = processor(null!);

        // Assert
        result.Should().Be(string.Empty);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void DefaultIfEmpty_WithEmptyInput_ReturnsDefaultValue()
    {
        // Arrange
        var processor = ItemLoaderProcessors.DefaultIfEmpty("N/A");

        // Act
        var result = processor("");

        // Assert
        result.Should().Be("N/A");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void DefaultIfEmpty_WithNonEmptyInput_ReturnsOriginal()
    {
        // Arrange
        var processor = ItemLoaderProcessors.DefaultIfEmpty("N/A");

        // Act
        var result = processor("Hello");

        // Assert
        result.Should().Be("Hello");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void DefaultIfEmpty_WithWhitespaceInput_ReturnsDefaultValue()
    {
        // Arrange
        var processor = ItemLoaderProcessors.DefaultIfEmpty("N/A");

        // Act
        var result = processor("   ");

        // Assert
        result.Should().Be("N/A");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void DefaultIfEmpty_WithNullDefaultValue_ThrowsArgumentNullException()
    {
        // Act
        var act = () => ItemLoaderProcessors.DefaultIfEmpty(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Processors_CanBeChained()
    {
        // Arrange
        var input = "  <p>HELLO WORLD</p>  ";
        var processors = new[]
        {
            ItemLoaderProcessors.StripHtml(),
            ItemLoaderProcessors.Strip(),
            ItemLoaderProcessors.ToLower()
        };

        // Act
        var result = processors.Aggregate(input, (current, processor) => processor(current));

        // Assert
        result.Should().Be("hello world");
    }
}
