using FluentAssertions;
using Ghost.Sdk.Extraction;

namespace Ghost.Sdk.Tests.Extraction;

[Trait("Category", "Unit")]
public sealed class HtmlAgilityLinkExtractorTests
{
    private const string BaseUrl = "https://example.com/page";

    [Fact]
    public void ExtractLinks_WithSimpleHtml_ReturnsAbsoluteUrls()
    {
        // Arrange
        var html = @"
            <html>
                <body>
                    <a href='/about'>About</a>
                    <a href='/contact'>Contact</a>
                </body>
            </html>";
        var extractor = new HtmlAgilityLinkExtractor();

        // Act
        var links = extractor.ExtractLinks(html, BaseUrl).ToList();

        // Assert
        links.Should().HaveCount(2);
        links.Should().Contain("https://example.com/about");
        links.Should().Contain("https://example.com/contact");
    }

    [Fact]
    public void ExtractLinks_WithNestedLinks_ExtractsAll()
    {
        // Arrange
        var html = @"
            <html>
                <body>
                    <div>
                        <a href='/page1'>Page 1</a>
                        <div>
                            <a href='/page2'>Page 2</a>
                        </div>
                    </div>
                    <a href='/page3'>Page 3</a>
                </body>
            </html>";
        var extractor = new HtmlAgilityLinkExtractor();

        // Act
        var links = extractor.ExtractLinks(html, BaseUrl).ToList();

        // Assert
        links.Should().HaveCount(3);
    }

    [Fact]
    public void ExtractLinks_WithXpathRestriction_ExtractsOnlyFromSpecifiedArea()
    {
        // Arrange
        var html = @"
            <html>
                <body>
                    <div id='main'>
                        <a href='/included1'>Included 1</a>
                        <a href='/included2'>Included 2</a>
                    </div>
                    <div id='sidebar'>
                        <a href='/excluded'>Excluded</a>
                    </div>
                </body>
            </html>";
        var options = new LinkExtractorOptions
        {
            RestrictXpaths = new[] { "//div[@id='main']" }
        };
        var extractor = new HtmlAgilityLinkExtractor(options);

        // Act
        var links = extractor.ExtractLinks(html, BaseUrl).ToList();

        // Assert
        links.Should().HaveCount(2);
        links.Should().Contain("https://example.com/included1");
        links.Should().Contain("https://example.com/included2");
        links.Should().NotContain("https://example.com/excluded");
    }

    [Fact]
    public void ExtractLinks_WithFragmentStripping_RemovesFragments()
    {
        // Arrange
        var html = @"
            <html>
                <body>
                    <a href='/page#section1'>Link 1</a>
                    <a href='/page#section2'>Link 2</a>
                    <a href='/other'>Link 3</a>
                </body>
            </html>";
        var options = new LinkExtractorOptions { StripFragments = true };
        var extractor = new HtmlAgilityLinkExtractor(options);

        // Act
        var links = extractor.ExtractLinks(html, BaseUrl).ToList();

        // Assert
        links.Should().HaveCount(2);
        links.Should().Contain("https://example.com/page");
        links.Should().Contain("https://example.com/other");
    }

    [Fact]
    public void ExtractLinks_WithFragmentKeeping_PreservesFragments()
    {
        // Arrange
        var html = @"
            <html>
                <body>
                    <a href='/page#section'>Link</a>
                </body>
            </html>";
        var options = new LinkExtractorOptions { StripFragments = false };
        var extractor = new HtmlAgilityLinkExtractor(options);

        // Act
        var links = extractor.ExtractLinks(html, BaseUrl).ToList();

        // Assert
        links.Should().ContainSingle();
        links[0].Should().Contain("#section");
    }

    [Fact]
    public void ExtractLinks_WithMalformedHtml_HandlesGracefully()
    {
        // Arrange
        var html = @"
            <html>
                <body>
                    <a href='/page1'>Unclosed tag
                    <a href='/page2'>Valid</a>
                    <div>
                        <a href='/page3'>Another</a>
                </body>";
        var extractor = new HtmlAgilityLinkExtractor();

        // Act
        var links = extractor.ExtractLinks(html, BaseUrl).ToList();

        // Assert
        links.Should().HaveCount(3);
    }

    [Fact]
    public void ExtractLinks_WithDenyExtensions_FiltersCorrectly()
    {
        // Arrange
        var html = @"
            <html>
                <body>
                    <a href='/page.html'>HTML</a>
                    <a href='/image.jpg'>Image</a>
                    <a href='/document.pdf'>PDF</a>
                    <a href='/archive.zip'>ZIP</a>
                </body>
            </html>";
        var options = new LinkExtractorOptions
        {
            DenyExtensions = new[] { ".jpg", ".pdf", ".zip" }
        };
        var extractor = new HtmlAgilityLinkExtractor(options);

        // Act
        var links = extractor.ExtractLinks(html, BaseUrl).ToList();

        // Assert
        links.Should().ContainSingle();
        links.Should().Contain("https://example.com/page.html");
    }

    [Fact]
    public void ExtractLinks_WithAllowedExtensions_FiltersCorrectly()
    {
        // Arrange
        var html = @"
            <html>
                <body>
                    <a href='/page.html'>HTML Page</a>
                    <a href='/page.htm'>HTM Page</a>
                    <a href='/page'>No Extension</a>
                    <a href='/image.jpg'>Image</a>
                </body>
            </html>";
        var options = new LinkExtractorOptions
        {
            AllowedExtensions = new[] { ".html", ".htm", "" }
        };
        var extractor = new HtmlAgilityLinkExtractor(options);

        // Act
        var links = extractor.ExtractLinks(html, BaseUrl).ToList();

        // Assert
        links.Should().HaveCount(3);
        links.Should().Contain("https://example.com/page.html");
        links.Should().Contain("https://example.com/page.htm");
        links.Should().Contain("https://example.com/page");
    }

    [Fact]
    public void ExtractLinks_WithAllowedDomains_FiltersCorrectly()
    {
        // Arrange
        var html = @"
            <html>
                <body>
                    <a href='https://example.com/page1'>Same Domain</a>
                    <a href='https://sub.example.com/page2'>Subdomain</a>
                    <a href='https://other.com/page3'>Other Domain</a>
                </body>
            </html>";
        var options = new LinkExtractorOptions
        {
            AllowedDomains = new[] { "example.com" }
        };
        var extractor = new HtmlAgilityLinkExtractor(options);

        // Act
        var links = extractor.ExtractLinks(html, BaseUrl).ToList();

        // Assert
        links.Should().HaveCount(2);
        links.Should().Contain("https://example.com/page1");
        links.Should().Contain("https://sub.example.com/page2");
    }

    [Fact]
    public void ExtractLinks_IgnoresFragmentOnlyLinks()
    {
        // Arrange
        var html = @"
            <html>
                <body>
                    <a href='#section'>Fragment Only</a>
                    <a href='/page'>Valid</a>
                </body>
            </html>";
        var extractor = new HtmlAgilityLinkExtractor();

        // Act
        var links = extractor.ExtractLinks(html, BaseUrl).ToList();

        // Assert
        links.Should().ContainSingle();
        links.Should().Contain("https://example.com/page");
    }

    [Fact]
    public void ExtractLinks_IgnoresJavascriptLinks()
    {
        // Arrange
        var html = @"
            <html>
                <body>
                    <a href='javascript:void(0)'>JS Link</a>
                    <a href='javascript:alert(1)'>Alert</a>
                    <a href='/valid'>Valid Link</a>
                </body>
            </html>";
        var extractor = new HtmlAgilityLinkExtractor();

        // Act
        var links = extractor.ExtractLinks(html, BaseUrl).ToList();

        // Assert
        links.Should().ContainSingle();
        links.Should().Contain("https://example.com/valid");
    }

    [Fact]
    public void ExtractLinks_WithUniqueOnly_RemovesDuplicates()
    {
        // Arrange
        var html = @"
            <html>
                <body>
                    <a href='/page'>Link 1</a>
                    <a href='/page'>Link 2</a>
                    <a href='/page'>Link 3</a>
                    <a href='/other'>Link 4</a>
                </body>
            </html>";
        var options = new LinkExtractorOptions { UniqueOnly = true };
        var extractor = new HtmlAgilityLinkExtractor(options);

        // Act
        var links = extractor.ExtractLinks(html, BaseUrl).ToList();

        // Assert
        links.Should().HaveCount(2);
    }

    [Fact]
    public void ExtractLinks_WithRelativeUrls_ResolvesCorrectly()
    {
        // Arrange
        var html = @"
            <html>
                <body>
                    <a href='../parent'>Parent</a>
                    <a href='./current'>Current</a>
                    <a href='child/nested'>Nested</a>
                    <a href='/absolute'>Absolute</a>
                </body>
            </html>";
        var extractor = new HtmlAgilityLinkExtractor();

        // Act
        var links = extractor.ExtractLinks(html, BaseUrl).ToList();

        // Assert
        links.Should().HaveCount(4);
        links.Should().AllSatisfy(link => link.Should().StartWith("https://"));
    }

    [Fact]
    public void ExtractLinks_WithEmptyHtml_ReturnsEmpty()
    {
        // Arrange
        var extractor = new HtmlAgilityLinkExtractor();

        // Act
        var links = extractor.ExtractLinks(string.Empty, BaseUrl).ToList();

        // Assert
        links.Should().BeEmpty();
    }

    [Fact]
    public void ExtractLinks_WithNullHtml_ThrowsArgumentNullException()
    {
        // Arrange
        var extractor = new HtmlAgilityLinkExtractor();

        // Act
        var act = () => extractor.ExtractLinks(null!, BaseUrl);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ExtractLinks_WithInvalidBaseUrl_ThrowsArgumentException()
    {
        // Arrange
        var extractor = new HtmlAgilityLinkExtractor();

        // Act
        var act = () => extractor.ExtractLinks("<a href='/test'>Test</a>", "not-a-url");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ExtractLinks_WithQueryParameters_PreservesParameters()
    {
        // Arrange
        var html = @"
            <html>
                <body>
                    <a href='/page?param=value&other=123'>Link</a>
                </body>
            </html>";
        var extractor = new HtmlAgilityLinkExtractor();

        // Act
        var links = extractor.ExtractLinks(html, BaseUrl).ToList();

        // Assert
        links.Should().ContainSingle();
        links[0].Should().Contain("?param=value&other=123");
    }

    [Fact]
    public void ExtractLinks_WithMultipleXpathRestrictions_ExtractsFromAllAreas()
    {
        // Arrange
        var html = @"
            <html>
                <body>
                    <div id='area1'>
                        <a href='/link1'>Link 1</a>
                    </div>
                    <div id='area2'>
                        <a href='/link2'>Link 2</a>
                    </div>
                    <div id='excluded'>
                        <a href='/excluded'>Excluded</a>
                    </div>
                </body>
            </html>";
        var options = new LinkExtractorOptions
        {
            RestrictXpaths = new[] { "//div[@id='area1']", "//div[@id='area2']" }
        };
        var extractor = new HtmlAgilityLinkExtractor(options);

        // Act
        var links = extractor.ExtractLinks(html, BaseUrl).ToList();

        // Assert
        links.Should().HaveCount(2);
        links.Should().Contain("https://example.com/link1");
        links.Should().Contain("https://example.com/link2");
        links.Should().NotContain("https://example.com/excluded");
    }
}
