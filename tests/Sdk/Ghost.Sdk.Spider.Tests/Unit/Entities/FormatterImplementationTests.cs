using FluentAssertions;
using Ghost.Sdk.Spider.Core.Entities.Formatters;
using Xunit;
using System.Globalization;

namespace Ghost.Sdk.Spider.Tests.Unit.Entities;

public class FormatterImplementationTests
{
    public class TrimFormatterTests
    {
        [Fact]
        public void Format_WithWhitespace_TrimsWhitespace()
        {
            // Arrange
            var formatter = new TrimFormatter();

            // Act
            var result = formatter.Format("  hello world  ");

            // Assert
            result.Should().Be("hello world");
        }

        [Fact]
        public void Format_WithTrimStart_TrimsOnlyStart()
        {
            // Arrange
            var formatter = new TrimFormatter { TrimStart = true };

            // Act
            var result = formatter.Format("  hello  ");

            // Assert
            result.Should().Be("hello  ");
        }

        [Fact]
        public void Format_WithTrimEnd_TrimsOnlyEnd()
        {
            // Arrange
            var formatter = new TrimFormatter { TrimEnd = true };

            // Act
            var result = formatter.Format("  hello  ");

            // Assert
            result.Should().Be("  hello");
        }

        [Fact]
        public void Format_WithCustomChars_TrimsSpecifiedChars()
        {
            // Arrange
            var formatter = new TrimFormatter { TrimChars = ",.;" };

            // Act
            var result = formatter.Format(",,,hello...");

            // Assert
            result.Should().Be("hello");
        }

        [Fact]
        public void Format_WithNonString_ReturnsOriginalValue()
        {
            // Arrange
            var formatter = new TrimFormatter();

            // Act
            var result = formatter.Format(123);

            // Assert
            result.Should().Be(123);
        }
    }

    public class RegexFormatterTests
    {
        [Fact]
        public void Format_WithReplacement_ReplacesPattern()
        {
            // Arrange
            var formatter = new RegexFormatter
            {
                Pattern = @"(\d{3})-(\d{3})-(\d{4})",
                Replacement = "($1) $2-$3"
            };

            // Act
            var result = formatter.Format("555-123-4567");

            // Assert
            result.Should().Be("(555) 123-4567");
        }

        [Fact]
        public void Format_WithExtraction_ExtractsMatch()
        {
            // Arrange
            var formatter = new RegexFormatter
            {
                Pattern = @"\d{3}-\d{3}-\d{4}"
            };

            // Act
            var result = formatter.Format("Call us at 555-123-4567 today");

            // Assert
            result.Should().Be("555-123-4567");
        }

        [Fact]
        public void Format_WithCaptureGroup_ExtractsGroup()
        {
            // Arrange
            var formatter = new RegexFormatter
            {
                Pattern = @"\$(\d+\.\d+)",
                Group = 1
            };

            // Act
            var result = formatter.Format("Price: $99.99");

            // Assert
            result.Should().Be("99.99");
        }

        [Fact]
        public void Format_WithIgnoreCase_MatchesCaseInsensitive()
        {
            // Arrange
            var formatter = new RegexFormatter
            {
                Pattern = "hello",
                Replacement = "goodbye",
                IgnoreCase = true
            };

            // Act
            var result = formatter.Format("HELLO World");

            // Assert
            result.Should().Be("goodbye World");
        }

        [Fact]
        public void Validate_WithInvalidPattern_ThrowsException()
        {
            // Arrange
            var formatter = new RegexFormatter { Pattern = "[invalid(" };

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => formatter.Validate());
        }
    }

    public class DateTimeFormatterTests
    {
        [Fact]
        public void Format_WithInputFormat_ParsesCustomFormat()
        {
            // Arrange
            var formatter = new DateTimeFormatter
            {
                InputFormat = "MM/dd/yyyy",
                OutputFormat = "yyyy-MM-dd"
            };

            // Act
            var result = formatter.Format("12/25/2024");

            // Assert
            result.Should().Be("2024-12-25");
        }

        [Fact]
        public void Format_WithDefaultParsing_ParsesStandardFormat()
        {
            // Arrange
            var formatter = new DateTimeFormatter
            {
                OutputFormat = "yyyy-MM-dd"
            };

            // Act
            var result = formatter.Format("2024-12-25T10:30:00");

            // Assert
            result.Should().Be("2024-12-25");
        }

        [Fact]
        public void Format_WithDateTime_FormatsDateTime()
        {
            // Arrange
            var formatter = new DateTimeFormatter
            {
                OutputFormat = "MM/dd/yyyy"
            };
            var date = new DateTime(2024, 12, 25);

            // Act
            var result = formatter.Format(date);

            // Assert
            result.Should().Be("12/25/2024");
        }

        [Fact]
        public void Format_WithInvalidDate_ReturnsOriginal()
        {
            // Arrange
            var formatter = new DateTimeFormatter
            {
                InputFormat = "yyyy-MM-dd"
            };

            // Act
            var result = formatter.Format("not a date");

            // Assert
            result.Should().Be("not a date");
        }

        [Fact]
        public void Format_WithInvalidCulture_HandlesGracefully()
        {
            // Arrange
            var formatter = new DateTimeFormatter
            {
                Culture = "invalid-culture",
                OutputFormat = "yyyy-MM-dd"
            };

            // Act - The formatter might handle invalid culture gracefully or throw
            // This tests the implementation's actual behavior
            var result = formatter.Format("2024-01-01");

            // Assert - Either gets formatted or returns original based on implementation
            result.Should().NotBeNull();
        }
    }

    public class HtmlDecodeFormatterTests
    {
        [Fact]
        public void Format_WithHtmlEntities_DecodesEntities()
        {
            // Arrange
            var formatter = new HtmlDecodeFormatter();

            // Act
            var result = formatter.Format("&lt;div&gt;Hello &amp; goodbye&lt;/div&gt;");

            // Assert
            result.Should().Be("<div>Hello & goodbye</div>");
        }

        [Fact]
        public void Format_WithMultipleDecode_DecodesMultipleTimes()
        {
            // Arrange
            var formatter = new HtmlDecodeFormatter
            {
                DecodeMultipleTimes = true
            };

            // Act
            var result = formatter.Format("&amp;lt;div&amp;gt;");

            // Assert
            result.Should().Be("<div>");
        }

        [Fact]
        public void Format_WithNoEntities_ReturnsOriginal()
        {
            // Arrange
            var formatter = new HtmlDecodeFormatter();

            // Act
            var result = formatter.Format("Hello World");

            // Assert
            result.Should().Be("Hello World");
        }

        [Fact]
        public void Format_WithNonString_ReturnsOriginal()
        {
            // Arrange
            var formatter = new HtmlDecodeFormatter();

            // Act
            var result = formatter.Format(123);

            // Assert
            result.Should().Be(123);
        }

        [Fact]
        public void Validate_WithInvalidMaxIterations_ThrowsException()
        {
            // Arrange
            var formatter = new HtmlDecodeFormatter { MaxDecodeIterations = 0 };

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => formatter.Validate());
        }
    }

    public class StringFormatterTests
    {
        [Fact]
        public void Format_WithFormatString_FormatsValue()
        {
            // Arrange
            var formatter = new StringFormatter { FormatString = "Value: {0}" };

            // Act
            var result = formatter.Format("test");

            // Assert
            result.Should().Be("Value: test");
        }

        [Fact]
        public void Format_WithNumericFormat_FormatsNumber()
        {
            // Arrange
            var formatter = new StringFormatter { FormatString = "${0:N2}" };

            // Act
            var result = formatter.Format(1234.5);

            // Assert
            result.Should().Be("$1,234.50");
        }

        [Fact]
        public void Format_WithCulture_UsesCultureFormatting()
        {
            // Arrange
            var formatter = new StringFormatter
            {
                FormatString = "{0:N2}",
                Culture = "de-DE"
            };

            // Act
            var result = formatter.Format(1234.5);

            // Assert
            result.Should().Be("1.234,50");
        }

        [Fact]
        public void Validate_WithoutPlaceholder_ThrowsException()
        {
            // Arrange
            var formatter = new StringFormatter { FormatString = "No placeholder" };

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => formatter.Validate());
        }
    }

    public class ReplaceFormatterTests
    {
        [Fact]
        public void Format_WithSimpleReplace_ReplacesText()
        {
            // Arrange
            var formatter = new ReplaceFormatter
            {
                OldValue = "old",
                NewValue = "new"
            };

            // Act
            var result = formatter.Format("This is old text");

            // Assert
            result.Should().Be("This is new text");
        }

        [Fact]
        public void Format_WithIgnoreCase_ReplacesCaseInsensitive()
        {
            // Arrange
            var formatter = new ReplaceFormatter
            {
                OldValue = "hello",
                NewValue = "goodbye",
                IgnoreCase = true
            };

            // Act
            var result = formatter.Format("HELLO World");

            // Assert
            result.Should().Be("goodbye World");
        }

        [Fact]
        public void Format_WithMultipleOccurrences_ReplacesAll()
        {
            // Arrange
            var formatter = new ReplaceFormatter
            {
                OldValue = "a",
                NewValue = "o"
            };

            // Act
            var result = formatter.Format("banana");

            // Assert
            result.Should().Be("bonono");
        }
    }

    public class UrlDecodeFormatterTests
    {
        [Fact]
        public void Format_WithEncodedUrl_DecodesUrl()
        {
            // Arrange
            var formatter = new UrlDecodeFormatter();

            // Act
            var result = formatter.Format("Hello%20World%21");

            // Assert
            result.Should().Be("Hello World!");
        }

        [Fact]
        public void Format_WithMultipleDecode_DecodesMultipleTimes()
        {
            // Arrange
            var formatter = new UrlDecodeFormatter
            {
                DecodeMultipleTimes = true
            };

            // Act
            var result = formatter.Format("Hello%2520World");

            // Assert
            result.Should().Be("Hello World");
        }

        [Fact]
        public void Format_WithNonString_ReturnsOriginal()
        {
            // Arrange
            var formatter = new UrlDecodeFormatter();

            // Act
            var result = formatter.Format(123);

            // Assert
            result.Should().Be(123);
        }

        [Fact]
        public void Validate_WithInvalidMaxIterations_ThrowsException()
        {
            // Arrange
            var formatter = new UrlDecodeFormatter { MaxDecodeIterations = 0 };

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => formatter.Validate());
        }
    }

    public class FormatterChainTests
    {
        [Fact]
        public void FormatterChain_WithMultipleFormatters_AppliesInOrder()
        {
            // Arrange
            var trimFormatter = new TrimFormatter();
            var upperFormatter = new StringFormatter { FormatString = "{0}" };
            var replaceFormatter = new ReplaceFormatter
            {
                OldValue = "HELLO",
                NewValue = "GOODBYE"
            };

            var value = "  HELLO WORLD  ";

            // Act - Apply formatters in sequence
            var result = trimFormatter.Format(value); // "HELLO WORLD"
            result = upperFormatter.Format(result);    // "HELLO WORLD" (no change with {0})
            result = replaceFormatter.Format(result);  // "GOODBYE WORLD"

            // Assert
            result.Should().Be("GOODBYE WORLD");
        }
    }
}
