using FluentAssertions;
using Ghost.Sdk.Spider.Core.Extraction.Selectors;
using Xunit;
using Ghost.Testing.Reliability;
using Xunit.Abstractions;

namespace Ghost.Sdk.Spider.Tests.Unit.Extraction;

public class CssSelectorTests : ReliabilityTestBase
{
    public CssSelectorTests(ITestOutputHelper output) : base(output) { }
    [Fact]
    public void Select_WithValidCssSelector_ShouldReturnMatches()
    {
        // Arrange
        var selector = new CssSelector(".item");
        var html = @"
            <html>
                <body>
                    <div class='item'>Item 1</div>
                    <div class='item'>Item 2</div>
                    <div class='other'>Other</div>
                </body>
            </html>";

        // Act
        var results = selector.SelectValues(html);

        // Assert
        results.Should().HaveCount(2);
        results[0].Should().Be("Item 1");
        results[1].Should().Be("Item 2");
    }

    [Fact]
    public void Select_WithAttribute_ShouldExtractAttributeValue()
    {
        // Arrange
        var selector = new CssSelector(".item", "data-id");
        var html = @"
            <html>
                <body>
                    <div class='item' data-id='1'>Item 1</div>
                    <div class='item' data-id='2'>Item 2</div>
                </body>
            </html>";

        // Act
        var results = selector.SelectValues(html);

        // Assert
        results.Should().HaveCount(2);
        results[0].Should().Be("1");
        results[1].Should().Be("2");
    }

    [Fact]
    public void SelectFirst_ShouldReturnFirstMatch()
    {
        // Arrange
        var selector = new CssSelector(".item");
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
        result.Should().Be("First");
    }

    [Fact]
    public void SelectFirst_WithNoMatches_ShouldReturnNull()
    {
        // Arrange
        var selector = new CssSelector(".nonexistent");
        var html = "<html><body><div class='item'>Test</div></body></html>";

        // Act
        var result = selector.SelectFirst(html);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Select_WithEmptyContent_ShouldReturnEmptyList()
    {
        // Arrange
        var selector = new CssSelector("div");

        // Act
        var results = selector.SelectValues(string.Empty);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public void Select_WithComplexSelector_ShouldWork()
    {
        // Arrange
        var selector = new CssSelector("article.post h1.post-title");
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
        var results = selector.SelectValues(html);

        // Assert
        results.Should().HaveCount(2);
        results[0].Should().Be("Title 1");
        results[1].Should().Be("Title 2");
    }

    [Fact]
    public void Select_WithIdSelector_ShouldWork()
    {
        // Arrange
        var selector = new CssSelector("#main-title");
        var html = @"
            <html>
                <body>
                    <h1 id='main-title'>Main Title</h1>
                    <h2>Subtitle</h2>
                </body>
            </html>";

        // Act
        var results = selector.SelectValues(html);

        // Assert
        results.Should().HaveCount(1);
        results[0].Should().Be("Main Title");
    }

    [Fact]
    public void Select_WithAttributeSelector_ShouldWork()
    {
        // Arrange
        var selector = new CssSelector("[data-id='1']");
        var html = @"
            <html>
                <body>
                    <div data-id='1'>Item 1</div>
                    <div data-id='2'>Item 2</div>
                </body>
            </html>";

        // Act
        var results = selector.SelectValues(html);

        // Assert
        results.Should().HaveCount(1);
        results[0].Should().Be("Item 1");
    }

    [Fact]
    public void Select_WithPseudoSelector_ShouldWork()
    {
        // Arrange
        var selector = new CssSelector(".item:first-child");
        var html = @"
            <html>
                <body>
                    <div>
                        <span class='item'>First</span>
                        <span class='item'>Second</span>
                    </div>
                </body>
            </html>";

        // Act
        var results = selector.SelectValues(html);

        // Assert
        results.Should().HaveCount(1);
        results[0].Should().Be("First");
    }

    [Fact]
    public void Validate_WithValidSelector_ShouldReturnTrue()
    {
        // Arrange
        var selector = new CssSelector(".test-class");

        // Act
        var isValid = selector.Validate();

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithInvalidSelector_ShouldReturnFalse()
    {
        // Arrange
        var selector = new CssSelector("[invalid[");

        // Act
        var isValid = selector.Validate();

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithNullExpression_ShouldThrow()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => new CssSelector(null!));
    }

    [Fact]
    public void Expression_ShouldReturnConstructorValue()
    {
        // Arrange
        var expression = ".test-class";
        var selector = new CssSelector(expression);

        // Act & Assert
        selector.Expression.Should().Be(expression);
    }

    [Fact]
    public void Select_WithHrefAttribute_ShouldExtractUrl()
    {
        // Arrange
        var selector = new CssSelector("a.link", "href");
        var html = @"
            <html>
                <body>
                    <a class='link' href='https://example.com/page1'>Link 1</a>
                    <a class='link' href='https://example.com/page2'>Link 2</a>
                </body>
            </html>";

        // Act
        var results = selector.SelectValues(html);

        // Assert
        results.Should().HaveCount(2);
        results[0].Should().Be("https://example.com/page1");
        results[1].Should().Be("https://example.com/page2");
    }
}
