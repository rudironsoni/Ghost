using Ghost.Contracts.Jobs;
using Ghost.Kernel;
using Ghost.Plugin.Glassdoor.Jobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Ghost.Plugin.Glassdoor.Tests;

public sealed class GlassdoorSearchScraperTests
{
    private readonly GhostKernel _kernel;
    private readonly IOptions<GlassdoorOptions> _options;
    private readonly ILogger<GlassdoorSearchScraper> _logger;

    public GlassdoorSearchScraperTests()
    {
        _kernel = new Mock<GhostKernel>().Object;
        _options = Options.Create(new GlassdoorOptions
        {
            Enabled = true,
            ProxyEnabled = false,
            Strategy = JobSearchStrategy.BrowserOnly
        });
        _logger = new Mock<ILogger<GlassdoorSearchScraper>>().Object;
    }

    [Fact]
    public void Constructor_WithNullKernel_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new GlassdoorSearchScraper(null!, _options, _logger));

        Assert.Equal("kernel", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new GlassdoorSearchScraper(_kernel, null!, _logger));

        Assert.Equal("options", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange & Act
        using var scraper = new GlassdoorSearchScraper(_kernel, _options, _logger);

        // Assert
        Assert.NotNull(scraper);
    }

    [Fact]
    public void Constructor_WithNullProxyProvider_CreatesInstance()
    {
        // Arrange & Act
        using var scraper = new GlassdoorSearchScraper(_kernel, _options, _logger, proxyProvider: null);

        // Assert
        Assert.NotNull(scraper);
    }

    [Fact]
    public async Task SearchAsync_WithNullCriteria_ThrowsArgumentNullException()
    {
        // Arrange
        using var scraper = new GlassdoorSearchScraper(_kernel, _options, _logger);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await scraper.SearchAsync(null!, 20));
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        var scraper = new GlassdoorSearchScraper(_kernel, _options, _logger);

        // Act & Assert
        scraper.Dispose();
        scraper.Dispose(); // Should not throw
    }
}
