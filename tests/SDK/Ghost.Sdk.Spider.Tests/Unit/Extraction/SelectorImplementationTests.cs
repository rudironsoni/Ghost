using FluentAssertions;
using Ghost.Sdk.Spider.Core.Extraction.Selectors;
using Xunit;
using System.Text.RegularExpressions;

namespace Ghost.Sdk.Spider.Tests.Unit.Extraction;

public class SelectorImplementationTests
{
    public class XPathSelectorTests
    {
        [Fact]
        public void Select_WithBasicXPath_ReturnsAllMatches()
        {
            // Arrange
            var html = "<html><div><p>Text 1</p><p>Text 2</p></div></html>";
            var selector = new XPathSelector("//p");

            // Act
            var results = selector.Select(html);

            // Assert
            results.Should().HaveCount(2);
            results[0].Should().Be("Text 1");
            results[1].Should().Be("Text 2");
        }

        [Fact]
        public void Select_WithAttributeSelector_ReturnsAttributeValues()
        {
            // Arrange
            var html = "<html><a href='http://example.com'>Link</a></html>";
            var selector = new XPathSelector("//a", "href");

            // Act
            var results = selector.SelectValues(html);

            // Assert
            results.Should().ContainSingle();
            results[0].Should().Be("http://example.com");
        }

        [Fact]
        public void SelectFirst_ReturnsFirstMatch()
        {
            // Arrange
            var html = "<html><p>First</p><p>Second</p></html>";
            var selector = new XPathSelector("//p");

            // Act
            var result = selector.SelectFirst(html);

            // Assert
            result.Should().Be("First");
        }

        [Fact]
        public void Select_WithEmptyContent_ReturnsEmptyList()
        {
            // Arrange
            var selector = new XPathSelector("//p");

            // Act
            var results = selector.SelectValues("");

            // Assert
            results.Should().BeEmpty();
        }

        [Fact]
        public void Validate_WithValidXPath_ReturnsTrue()
        {
            // Arrange
            var selector = new XPathSelector("//div[@class='test']");

            // Act
            var isValid = selector.Validate();

            // Assert
            isValid.Should().BeTrue();
        }
    }

    public class CssSelectorTests
    {
        [Fact]
        public void Select_WithBasicCss_ReturnsAllMatches()
        {
            // Arrange
            var html = "<html><div class='item'>Item 1</div><div class='item'>Item 2</div></html>";
            var selector = new CssSelector(".item");

            // Act
            var results = selector.Select(html);

            // Assert
            results.Should().HaveCount(2);
            results[0].Should().Be("Item 1");
            results[1].Should().Be("Item 2");
        }

        [Fact]
        public void Select_WithAttributeSelector_ReturnsAttributeValues()
        {
            // Arrange
            var html = "<html><img src='image.jpg' alt='Test'/></html>";
            var selector = new CssSelector("img", "src");

            // Act
            var results = selector.Select(html);

            // Assert
            results.Should().ContainSingle();
            results[0].Should().Be("image.jpg");
        }

        [Fact]
        public void SelectFirst_ReturnsFirstMatch()
        {
            // Arrange
            var html = "<html><span>First</span><span>Second</span></html>";
            var selector = new CssSelector("span");

            // Act
            var result = selector.SelectFirst(html);

            // Assert
            result.Should().Be("First");
        }

        [Fact]
        public void Select_WithComplexSelector_ReturnsMatches()
        {
            // Arrange
            var html = "<html><div class='container'><p class='text'>Nested</p></div></html>";
            var selector = new CssSelector(".container .text");

            // Act
            var results = selector.Select(html);

            // Assert
            results.Should().ContainSingle();
            results[0].Should().Be("Nested");
        }

        [Fact]
        public void Validate_WithValidCss_ReturnsTrue()
        {
            // Arrange
            var selector = new CssSelector("div.class#id");

            // Act
            var isValid = selector.Validate();

            // Assert
            isValid.Should().BeTrue();
        }
    }

    public class RegexSelectorTests
    {
        [Fact]
        public void Select_WithSimplePattern_ReturnsAllMatches()
        {
            // Arrange
            var content = "Email: test@example.com and another@test.com";
            var selector = new RegexSelector(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b");

            // Act
            var results = selector.Select(content);

            // Assert
            results.Should().HaveCount(2);
            results[0].Should().Be("test@example.com");
            results[1].Should().Be("another@test.com");
        }

        [Fact]
        public void Select_WithCaptureGroup_ReturnsGroupValue()
        {
            // Arrange
            var content = "Price: $99.99 and $49.99";
            var selector = new RegexSelector(@"\$(\d+\.\d+)", group: 1);

            // Act
            var results = selector.Select(content);

            // Assert
            results.Should().HaveCount(2);
            results[0].Should().Be("99.99");
            results[1].Should().Be("49.99");
        }

        [Fact]
        public void SelectFirst_ReturnsFirstMatch()
        {
            // Arrange
            var content = "ABC123 DEF456";
            var selector = new RegexSelector(@"[A-Z]{3}(\d{3})", group: 1);

            // Act
            var result = selector.SelectFirst(content);

            // Assert
            result.Should().Be("123");
        }

        [Fact]
        public void Select_WithCaseInsensitive_MatchesIgnoringCase()
        {
            // Arrange
            var content = "Hello HELLO hello";
            var selector = new RegexSelector("hello", options: RegexOptions.IgnoreCase);

            // Act
            var results = selector.Select(content);

            // Assert
            results.Should().HaveCount(3);
        }

        [Fact]
        public void Validate_WithValidPattern_ReturnsTrue()
        {
            // Arrange
            var selector = new RegexSelector(@"\d+");

            // Act
            var isValid = selector.Validate();

            // Assert
            isValid.Should().BeTrue();
        }
    }

    public class JsonPathSelectorTests
    {
        [Fact]
        public void Select_WithBasicPath_ReturnsAllMatches()
        {
            // Arrange
            var json = @"{""items"": [""item1"", ""item2"", ""item3""]}";
            var selector = new JsonPathSelector("$.items[*]");

            // Act
            var results = selector.Select(json);

            // Assert
            results.Should().HaveCount(3);
            results[0].Should().Contain("item1");
            results[1].Should().Contain("item2");
            results[2].Should().Contain("item3");
        }

        [Fact]
        public void Select_WithNestedPath_ReturnsValue()
        {
            // Arrange
            var json = @"{""user"": {""name"": ""John"", ""email"": ""john@test.com""}}";
            var selector = new JsonPathSelector("$.user.name");

            // Act
            var results = selector.Select(json);

            // Assert
            results.Should().ContainSingle();
            results[0].Should().Contain("John");
        }

        [Fact]
        public void SelectFirst_ReturnsFirstMatch()
        {
            // Arrange
            var json = @"{""numbers"": [1, 2, 3, 4, 5]}";
            var selector = new JsonPathSelector("$.numbers[*]");

            // Act
            var result = selector.SelectFirst(json);

            // Assert
            result.Should().NotBeNull();
            result.Should().Contain("1");
        }

        [Fact]
        public void Select_WithArrayFilter_ReturnsFilteredItems()
        {
            // Arrange
            var json = @"{""products"": [{""name"": ""A"", ""price"": 100}, {""name"": ""B"", ""price"": 200}]}";
            var selector = new JsonPathSelector("$.products[*].name");

            // Act
            var results = selector.Select(json);

            // Assert
            results.Should().HaveCount(2);
        }

        [Fact]
        public void Select_WithInvalidJson_ReturnsEmptyList()
        {
            // Arrange
            var selector = new JsonPathSelector("$.test");

            // Act
            var results = selector.Select("not valid json");

            // Assert
            results.Should().BeEmpty();
        }

        [Fact]
        public void Validate_WithValidPath_ReturnsTrue()
        {
            // Arrange
            var selector = new JsonPathSelector("$.test.path");

            // Act
            var isValid = selector.Validate();

            // Assert
            isValid.Should().BeTrue();
        }
    }

    public class JmesPathSelectorTests
    {
        [Fact]
        public void Select_WithBasicExpression_ReturnsValue()
        {
            // Arrange
            var json = @"{""name"": ""John"", ""age"": 30}";
            var selector = new JmesPathSelector("name");

            // Act
            var results = selector.Select(json);

            // Assert
            results.Should().ContainSingle();
            results[0].Should().Contain("John");
        }

        [Fact]
        public void Select_WithArrayProjection_ReturnsAllValues()
        {
            // Arrange
            var json = @"{""users"": [{""name"": ""Alice""}, {""name"": ""Bob""}]}";
            var selector = new JmesPathSelector("users[*].name");

            // Act
            var results = selector.Select(json);

            // Assert
            results.Should().HaveCount(2);
        }

        [Fact]
        public void SelectFirst_ReturnsFirstMatch()
        {
            // Arrange
            var json = @"{""items"": [""first"", ""second""]}";
            var selector = new JmesPathSelector("items");

            // Act
            var result = selector.SelectFirst(json);

            // Assert
            result.Should().NotBeNull();
        }

        [Fact]
        public void Select_WithInvalidJson_ReturnsEmptyList()
        {
            // Arrange
            var selector = new JmesPathSelector("test");

            // Act
            var results = selector.Select("invalid");

            // Assert
            results.Should().BeEmpty();
        }

        [Fact]
        public void Validate_WithValidExpression_ReturnsTrue()
        {
            // Arrange
            var selector = new JmesPathSelector("test.path");

            // Act
            var isValid = selector.Validate();

            // Assert
            isValid.Should().BeTrue();
        }
    }
}
