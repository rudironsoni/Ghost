using FluentAssertions;
using Ghost.Sdk.Spider.Core.Entities.Formatters;
using Xunit;
using System.Web;

namespace Ghost.Sdk.Spider.Tests.Unit.Entities;

public class FormatterTests
{
    public class TrimFormatterTests
    {
        [Fact]
        public void Format_WithWhitespace_ShouldTrim()
        {
            // Arrange
            var formatter = new TrimFormatter();
            var input = "  test value  ";

            // Act
            var result = formatter.Format(input);

            // Assert
            result.Should().Be("test value");
        }

        [Fact]
        public void Format_WithNull_ShouldReturnNull()
        {
            // Arrange
            var formatter = new TrimFormatter();

            // Act
            var result = formatter.Format(null);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void Format_WithNonString_ShouldReturnOriginal()
        {
            // Arrange
            var formatter = new TrimFormatter();
            var input = 123;

            // Act
            var result = formatter.Format(input);

            // Assert
            result.Should().Be(123);
        }
    }

    public class HtmlDecodeFormatterTests
    {
        [Fact]
        public void Format_WithHtmlEntities_ShouldDecode()
        {
            // Arrange
            var formatter = new HtmlDecodeFormatter();
            var input = "Test &amp; Value &lt;tag&gt;";

            // Act
            var result = formatter.Format(input);

            // Assert
            result.Should().Be("Test & Value <tag>");
        }

        [Fact]
        public void Format_WithNull_ShouldReturnNull()
        {
            // Arrange
            var formatter = new HtmlDecodeFormatter();

            // Act
            var result = formatter.Format(null);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void Format_WithNumericEntity_ShouldDecode()
        {
            // Arrange
            var formatter = new HtmlDecodeFormatter();
            var input = "&#169; 2026";

            // Act
            var result = formatter.Format(input);

            // Assert
            result.Should().Be("© 2026");
        }
    }

    public class UrlDecodeFormatterTests
    {
        [Fact]
        public void Format_WithUrlEncoded_ShouldDecode()
        {
            // Arrange
            var formatter = new UrlDecodeFormatter();
            var input = "Hello%20World%21";

            // Act
            var result = formatter.Format(input);

            // Assert
            result.Should().Be("Hello World!");
        }

        [Fact]
        public void Format_WithNull_ShouldReturnNull()
        {
            // Arrange
            var formatter = new UrlDecodeFormatter();

            // Act
            var result = formatter.Format(null);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void Format_WithSpecialCharacters_ShouldDecode()
        {
            // Arrange
            var formatter = new UrlDecodeFormatter();
            var input = "test%2Bvalue%3D123";

            // Act
            var result = formatter.Format(input);

            // Assert
            result.Should().Be("test+value=123");
        }
    }

    public class ReplaceFormatterTests
    {
        [Fact]
        public void Format_WithPattern_ShouldReplace()
        {
            // Arrange
            var formatter = new ReplaceFormatter { OldValue = "old", NewValue = "new" };
            var input = "This is old text";

            // Act
            var result = formatter.Format(input);

            // Assert
            result.Should().Be("This is new text");
        }

        [Fact]
        public void Format_WithRegexPattern_ShouldReplaceAll()
        {
            // Arrange
            var formatter = new ReplaceFormatter { OldValue = "\\d+", NewValue = "X" };
            var input = "Item 123 costs 456";

            // Act
            var result = formatter.Format(input);

            // Assert
            result.Should().Be("Item X costs X");
        }

        [Fact]
        public void Format_WithNull_ShouldReturnNull()
        {
            // Arrange
            var formatter = new ReplaceFormatter { OldValue = "test", NewValue = "replaced" };

            // Act
            var result = formatter.Format(null);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void Validate_WithoutPattern_ShouldThrow()
        {
            // Arrange
            var formatter = new ReplaceFormatter { OldValue = "", NewValue = "test" };

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => formatter.Validate());
        }
    }

    public class RegexFormatterTests
    {
        [Fact]
        public void Format_WithMatchingPattern_ShouldExtractMatch()
        {
            // Arrange
            var formatter = new RegexFormatter { Pattern = "\\d+" };
            var input = "Price: 123.45";

            // Act
            var result = formatter.Format(input);

            // Assert
            result.Should().Be("123");
        }

        [Fact]
        public void Format_WithGroup_ShouldExtractGroup()
        {
            // Arrange
            var formatter = new RegexFormatter { Pattern = "(\\d+)\\.(\\d+)", Group = 2 };
            var input = "123.45";

            // Act
            var result = formatter.Format(input);

            // Assert
            result.Should().Be("45");
        }

        [Fact]
        public void Format_WithNoMatch_ShouldReturnOriginal()
        {
            // Arrange
            var formatter = new RegexFormatter { Pattern = "\\d+" };
            var input = "No numbers here";

            // Act
            var result = formatter.Format(input);

            // Assert
            result.Should().Be("No numbers here");
        }

        [Fact]
        public void Format_WithNull_ShouldReturnNull()
        {
            // Arrange
            var formatter = new RegexFormatter { Pattern = "test" };

            // Act
            var result = formatter.Format(null);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void Validate_WithInvalidPattern_ShouldThrow()
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
        public void Format_WithValidDateTime_ShouldParse()
        {
            // Arrange
            var formatter = new DateTimeFormatter();
            var input = "2026-01-15T10:30:00Z";

            // Act
            var result = formatter.Format(input);

            // Assert
            result.Should().BeOfType<DateTime>();
            ((DateTime)result!).Year.Should().Be(2026);
            ((DateTime)result!).Month.Should().Be(1);
            ((DateTime)result!).Day.Should().Be(15);
        }

        [Fact]
        public void Format_WithCustomFormatString_ShouldParseCorrectly()
        {
            // Arrange
            var formatter = new DateTimeFormatter { InputFormat = "yyyy-MM-dd" };
            var input = "2026-02-04";

            // Act
            var result = formatter.Format(input);

            // Assert
            result.Should().BeOfType<DateTime>();
            ((DateTime)result!).Year.Should().Be(2026);
            ((DateTime)result!).Month.Should().Be(2);
            ((DateTime)result!).Day.Should().Be(4);
        }

        [Fact]
        public void Format_WithInvalidDate_ShouldReturnOriginal()
        {
            // Arrange
            var formatter = new DateTimeFormatter();
            var input = "not a date";

            // Act
            var result = formatter.Format(input);

            // Assert
            result.Should().Be("not a date");
        }

        [Fact]
        public void Format_WithNull_ShouldReturnNull()
        {
            // Arrange
            var formatter = new DateTimeFormatter();

            // Act
            var result = formatter.Format(null);

            // Assert
            result.Should().BeNull();
        }
    }

    public class StringFormatterTests
    {
        [Fact]
        public void Format_WithLowerCase_ShouldConvertToLowerCase()
        {
            // Arrange
            var formatter = new StringFormatter { FormatString = "{0}" };
            var input = "TEST VALUE";

        // Act
        var result = formatter.Format(input.ToLowerInvariant());

            // Assert
            result.Should().Be("test value");
        }

        [Fact]
        public void Format_WithUpperCase_ShouldConvertToUpperCase()
        {
            // Arrange
            var formatter = new StringFormatter { FormatString = "{0}" };
            var input = "test value";

        // Act
        var result = formatter.Format(input.ToUpperInvariant());

            // Assert
            result.Should().Be("TEST VALUE");
        }

        [Fact]
        public void Format_WithCapitalize_ShouldCapitalizeFirstLetter()
        {
            // Arrange
            var formatter = new StringFormatter { FormatString = "{0}" };
            var input = "test value";
            var capitalized = char.ToUpperInvariant(input[0]) + input.Substring(1);

            // Act
            var result = formatter.Format(capitalized);

            // Assert
            result.Should().Be("Test value");
        }

        [Fact]
        public void Format_WithNull_ShouldReturnNull()
        {
            // Arrange
            var formatter = new StringFormatter { FormatString = "{0}" };

            // Act
            var result = formatter.Format(null);

            // Assert
            result.Should().BeNull();
        }
    }

    public class FormatterChainTests
    {
        [Fact]
        public void MultipleFormatters_ShouldApplyInOrder()
        {
            // Arrange
            var formatter1 = new TrimFormatter { Order = 1 };
            var formatter2 = new HtmlDecodeFormatter { Order = 2 };
            var input = "  &amp;test&amp;  ";

            // Act
            var result1 = formatter1.Format(input);
            var result2 = formatter2.Format(result1);

            // Assert
            result2.Should().Be("&test&");
        }

        [Fact]
        public void FormatterOrder_ShouldBeRespected()
        {
            // Arrange
            var formatter1 = new TrimFormatter { Order = 1, Name = "Trim" };
            var formatter2 = new ReplaceFormatter
            {
                Order = 2,
                Name = "Replace",
                OldValue = "test",
                NewValue = "result"
            };

            // Act & Assert
            formatter1.Order.Should().BeLessThan(formatter2.Order);
        }

        [Fact]
        public void Formatter_ToString_ShouldReturnDescription()
        {
            // Arrange
            var formatter = new TrimFormatter { Name = "TestTrim", Order = 5 };

            // Act
            var description = formatter.ToString();

            // Assert
            description.Should().Contain("TestTrim");
            description.Should().Contain("5");
        }
    }
}
