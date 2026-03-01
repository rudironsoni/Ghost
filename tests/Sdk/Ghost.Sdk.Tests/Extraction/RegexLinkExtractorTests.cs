using FluentAssertions;
using Ghost.Sdk.Extraction;
using Xunit;

namespace Ghost.Sdk.Tests.Extraction;

[Trait("Category", "Unit")]
public sealed class RegexLinkExtractorTests
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
        var extractor = new RegexLinkExtractor();

        // Act
        var links = extractor.ExtractLinks(html, BaseUrl).ToList();

        // Assert
        links.Should().HaveCount(2);
        links.Should().Contain("https://example.com/about");
        links.Should().Contain("https://example.com/contact");
    }

    [Fact]
    public void ExtractLinks_WithAbsoluteUrls_ReturnsUrls()
    {
        // Arrange
        var html = @"
            <html>
                <body>
                    <a href='https://example.com/page1'>Page 1</a>
                    <a href='https://other.com/page2'>Page 2</a>
                </body>
            </html>";
        var extractor = new RegexLinkExtractor();

        // Act
        var links = extractor.ExtractLinks(html, BaseUrl).ToList();

        // Assert
        links.Should().HaveCount(2);
        links.Should().Contain("https://example.com/page1");
        links.Should().Contain("https://other.com/page2");
    }

    [Fact]
    public void ExtractLinks_WithDenyExtensions_FiltersCorrectly()
    {
        // Arrange
        var html = @"
            <html>
                <body>
                    <a href='/page.html'>HTML Page</a>
                    <a href='/image.jpg'>Image</a>
                    <a href='/doc.pdf'>PDF</a>
                </body>
            </html>";
        var options = new LinkExtractorOptions
        {
            DenyExtensions = new[] { ".jpg", ".pdf" }
        };
        var extractor = new RegexLinkExtractor(options);

        // Act
        var links = extractor.ExtractLinks(html, BaseUrl).ToList();

        // Assert
        links.Should().HaveCount(1);
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
        var extractor = new RegexLinkExtractor(options);

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
        var extractor = new RegexLinkExtractor(options);

        // Act
        var links = extractor.ExtractLinks(html, BaseUrl).ToList();

        // Assert
        links.Should().HaveCount(2);
        links.Should().Contain("https://example.com/page1");
        links.Should().Contain("https://sub.example.com/page2");
    }

    [Fact]
    public void ExtractLinks_IgnoresFragments()
    {
        // Arrange
        var html = @"
            <html>
                <body>
                    <a href='#section'>Fragment Only</a>
                </body>
            </html>";
        var extractor = new RegexLinkExtractor();

        // Act
        var links = extractor.ExtractLinks(html, BaseUrl).ToList();

        // Assert
        links.Should().BeEmpty();
    }

    [Fact]
    public void ExtractLinks_IgnoresJavascriptLinks()
    {
        // Arrange
        var html = @"
            <html>
                <body>
                    <a href='javascript:void(0)'>JS Link</a>
                    <a href='/valid'>Valid Link</a>
                </body>
            </html>";
        var extractor = new RegexLinkExtractor();

        // Act
        var links = extractor.ExtractLinks(html, BaseUrl).ToList();

        // Assert
        links.Should().HaveCount(1);
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
                    <a href='/other'>Link 3</a>
                </body>
            </html>";
        var options = new LinkExtractorOptions { UniqueOnly = true };
        var extractor = new RegexLinkExtractor(options);

        // Act
        var links = extractor.ExtractLinks(html, BaseUrl).ToList();

        // Assert
        links.Should().HaveCount(2);
        links.Should().Contain("https://example.com/page");
        links.Should().Contain("https://example.com/other");
    }

    [Fact]
    public void ExtractLinks_WithMalformedHtml_HandlesGracefully()
    {
        // Arrange
        var html = @"
            <html>
                <body>
                    <a href='/valid'>Valid</a>
                    <a href=''>Empty</a>
                    <a href='   '>Whitespace</a>
                    <a>No href</a>
                </body>
            </html>";
        var extractor = new RegexLinkExtractor();

        // Act
        var links = extractor.ExtractLinks(html, BaseUrl).ToList();

        // Assert
        links.Should().HaveCount(1);
        links.Should().Contain("https://example.com/valid");
    }

    [Fact]
    public void ExtractLinks_WithEmptyHtml_ReturnsEmpty()
    {
        // Arrange
        var extractor = new RegexLinkExtractor();

        // Act
        var links = extractor.ExtractLinks(string.Empty, BaseUrl).ToList();

        // Assert
        links.Should().BeEmpty();
    }

    [Fact]
    public void ExtractLinks_WithNullHtml_ThrowsArgumentNullException()
    {
        // Arrange
        var extractor = new RegexLinkExtractor();

        // Act
        var act = () => extractor.ExtractLinks(null!, BaseUrl);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ExtractLinks_WithInvalidBaseUrl_ThrowsArgumentException()
    {
        // Arrange
        var extractor = new RegexLinkExtractor();

        // Act
        var act = () => extractor.ExtractLinks("<a href='/test'>Test</a>", "not-a-url");

        // Assert
        act.Should().Throw<ArgumentException>();
    }
}
