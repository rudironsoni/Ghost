using FluentAssertions;
using Ghost.Sdk.Spider.Core.Extraction.Selectors;
using NUnit.Framework;

namespace Ghost.Sdk.Spider.Tests.Unit.Extraction;

[TestFixture]
public class XPathSelectorTests
{
    [Test]
    public void Select_WithValidXPath_ShouldReturnMatches()
    {
        // Arrange
        var selector = new XPathSelector("//div[@class='item']");
        var html = @"
            <html>
                <body>
                    <div class='item'>Item 1</div>
                    <div class='item'>Item 2</div>
                    <div class='other'>Other</div>
                </body>
            </html>";

        // Act
        var results = selector.Select(html);

        // Assert
        results.Should().HaveCount(2);
        results[0].Should().Contain("Item 1");
        results[1].Should().Contain("Item 2");
    }

    [Test]
    public void Select_WithAttribute_ShouldExtractAttributeValue()
    {
        // Arrange
        var selector = new XPathSelector("//div[@class='item']", "data-id");
        var html = @"
            <html>
                <body>
                    <div class='item' data-id='1'>Item 1</div>
                    <div class='item' data-id='2'>Item 2</div>
                </body>
            </html>";

        // Act
        var results = selector.Select(html);

        // Assert
        results.Should().HaveCount(2);
        results[0].Should().Be("1");
        results[1].Should().Be("2");
    }

    [Test]
    public void SelectFirst_ShouldReturnFirstMatch()
    {
        // Arrange
        var selector = new XPathSelector("//div[@class='item']");
        var html = @"
            <html>
                <body>
                    <div class='item'>First</div>
                    <div class='item'>Second</div>
                </body>
            </html>";

        // Act
        var result = selector.SelectFirst(html);

        // Assert
        result.Should().NotBeNull();
        result.Should().Contain("First");
    }

    [Test]
    public void SelectFirst_WithNoMatches_ShouldReturnNull()
    {
        // Arrange
        var selector = new XPathSelector("//div[@class='nonexistent']");
        var html = "<html><body><div class='item'>Test</div></body></html>";

        // Act
        var result = selector.SelectFirst(html);

        // Assert
        result.Should().BeNull();
    }

    [Test]
    public void Select_WithEmptyContent_ShouldReturnEmptyList()
    {
        // Arrange
        var selector = new XPathSelector("//div");

        // Act
        var results = selector.Select(string.Empty);

        // Assert
        results.Should().BeEmpty();
    }

    [Test]
    public void Select_WithComplexXPath_ShouldWork()
    {
        // Arrange
        var selector = new XPathSelector("//article[@class='post']//h1[@class='post-title']");
        var html = @"
            <html>
                <body>
                    <article class='post'>
                        <h1 class='post-title'>Title 1</h1>
                    </article>
                    <article class='post'>
                        <h1 class='post-title'>Title 2</h1>
                    </article>
                </body>
            </html>";

        // Act
        var results = selector.Select(html);

        // Assert
        results.Should().HaveCount(2);
        results[0].Should().Be("Title 1");
        results[1].Should().Be("Title 2");
    }

    [Test]
    public void Validate_WithValidXPath_ShouldReturnTrue()
    {
        // Arrange
        var selector = new XPathSelector("//div[@class='test']");

        // Act
        var isValid = selector.Validate();

        // Assert
        isValid.Should().BeTrue();
    }

    [Test]
    public void Validate_WithInvalidXPath_ShouldReturnFalse()
    {
        // Arrange
        var selector = new XPathSelector("//[invalid");

        // Act
        var isValid = selector.Validate();

        // Assert
        isValid.Should().BeFalse();
    }

    [Test]
    public void Constructor_WithNullExpression_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new XPathSelector(null!));
    }

    [Test]
    public void Expression_ShouldReturnConstructorValue()
    {
        // Arrange
        var expression = "//div[@class='test']";
        var selector = new XPathSelector(expression);

        // Act & Assert
        selector.Expression.Should().Be(expression);
    }

    [Test]
    public void Select_WithNestedElements_ShouldExtractInnerText()
    {
        // Arrange
        var selector = new XPathSelector("//div[@class='container']/p");
        var html = @"
            <html>
                <body>
                    <div class='container'>
                        <p>Paragraph <strong>with bold</strong> text</p>
                    </div>
                </body>
            </html>";

        // Act
        var results = selector.Select(html);

        // Assert
        results.Should().HaveCount(1);
        results[0].Should().Contain("Paragraph");
        results[0].Should().Contain("with bold");
        results[0].Should().Contain("text");
    }
}
