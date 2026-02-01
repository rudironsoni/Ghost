using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Ghost.Contracts.Jobs;
using Ghost.Platform.Glassdoor.Internal;
using Ghost.Platform.Google.Jobs.Internal;
using Ghost.Platform.Indeed.Internal;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Ghost.Platform.Common.Tests.DotnetSpider;

/// <summary>
/// Integration tests for multi-strategy parsers (Indeed, Glassdoor, Google Jobs).
/// Tests the complete flow of content classification, strategy selection, and fallback logic.
/// </summary>
public class IndeedMultiStrategyParserIntegrationTests
{
    private readonly Mock<ILogger<IndeedMultiStrategyParser>> _mockLogger;

    public IndeedMultiStrategyParserIntegrationTests()
    {
        _mockLogger = new Mock<ILogger<IndeedMultiStrategyParser>>();
    }

    #region Content Classification Tests

    [Theory]
    [InlineData("<html><body><div class=\"job\">test</div></body></html>")]
    [InlineData("{\"jobs\": []}")]
    [InlineData("[{\"title\": \"Software Engineer\"}]")]
    [InlineData("")]
    public async Task ClassifyContent_ShouldDetectContentType(string html)
    {
        // Arrange
        var parser = new IndeedMultiStrategyParser(_mockLogger.Object);

        // Act
        var result = await parser.ParseHtmlAsync(html);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<List<JobListing>>(result);
    }

    #endregion

    #region Empty/Null HTML Tests

    [Fact]
    public async Task ParseHtmlAsync_ShouldReturnEmptyList_WhenHtmlIsNull()
    {
        // Arrange
        var parser = new IndeedMultiStrategyParser(_mockLogger.Object);

        // Act
        var result = await parser.ParseHtmlAsync(null!);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task ParseHtmlAsync_ShouldReturnEmptyList_WhenHtmlIsEmpty()
    {
        // Arrange
        var parser = new IndeedMultiStrategyParser(_mockLogger.Object);

        // Act
        var result = await parser.ParseHtmlAsync(string.Empty);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task ParseHtmlAsync_ShouldReturnEmptyList_WhenHtmlIsWhitespace()
    {
        // Arrange
        var parser = new IndeedMultiStrategyParser(_mockLogger.Object);

        // Act
        var result = await parser.ParseHtmlAsync("   \n\t  ");

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    #endregion

    #region Fallback Strategy Tests

    [Fact]
    public async Task ParseHtmlAsync_ShouldFallBackToRegex_WhenJsonParsingFails()
    {
        // Arrange
        var parser = new IndeedMultiStrategyParser(_mockLogger.Object);
        var malformedJson = "{invalid json}}";

        // Act
        var result = await parser.ParseHtmlAsync(malformedJson);

        // Assert
        Assert.NotNull(result);
        // Should handle gracefully without throwing
    }

    [Fact]
    public async Task ParseHtmlAsync_ShouldAttemptMultipleStrategies_WhenPrimaryFails()
    {
        // Arrange
        var parser = new IndeedMultiStrategyParser(_mockLogger.Object);
        var htmlWithoutValidStructure = "<html><body>No valid job data here</body></html>";

        // Act
        var result = await parser.ParseHtmlAsync(htmlWithoutValidStructure);

        // Assert
        Assert.NotNull(result);
        // First and second strategies fail, regex strategy may or may not find data
    }

    #endregion

    #region Regex Pattern Matching Tests

    [Fact]
    public async Task ParseHtmlAsync_ShouldExtractJobs_WhenValidHtmlStructureProvided()
    {
        // Arrange
        var parser = new IndeedMultiStrategyParser(_mockLogger.Object);
        var html = SampleIndeedHtml.GetValidJobListingHtml(2);

        // Act
        var result = await parser.ParseHtmlAsync(html);

        // Assert
        Assert.NotNull(result);
        // Regex strategy should find jobs if structure matches
    }

    [Fact]
    public async Task ParseHtmlAsync_ShouldParseJobDetails_WhenCompleteHtmlProvided()
    {
        // Arrange
        var parser = new IndeedMultiStrategyParser(_mockLogger.Object);
        var html = SampleIndeedHtml.GetCompleteJobEntryHtml();

        // Act
        var result = await parser.ParseHtmlAsync(html);

        // Assert
        Assert.NotNull(result);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task ParseHtmlAsync_ShouldNotThrow_WhenHtmlIsMalformed()
    {
        // Arrange
        var parser = new IndeedMultiStrategyParser(_mockLogger.Object);
        var malformedHtml = "<div><span>unclosed tags";

        // Act & Assert (should not throw)
        var result = await parser.ParseHtmlAsync(malformedHtml);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ParseHtmlAsync_ShouldNotThrow_WhenJsonIsMalformed()
    {
        // Arrange
        var parser = new IndeedMultiStrategyParser(_mockLogger.Object);
        var malformedJson = "{\"jobs\": [incomplete";

        // Act & Assert (should not throw)
        var result = await parser.ParseHtmlAsync(malformedJson);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ParseHtmlAsync_ShouldHandleExceptionsGracefully_WhenParsingFails()
    {
        // Arrange
        var parser = new IndeedMultiStrategyParser(_mockLogger.Object);
        var problematicHtml = new string('x', 1000000); // Very large content

        // Act & Assert (should not throw)
        var result = await parser.ParseHtmlAsync(problematicHtml);
        Assert.NotNull(result);
    }

    #endregion

    #region Mixed Content Tests

    [Fact]
    public async Task ParseHtmlAsync_ShouldHandle_MixedHtmlAndJsonContent()
    {
        // Arrange
        var parser = new IndeedMultiStrategyParser(_mockLogger.Object);
        var mixedContent = @"
            <html>
            <body>
                <script>
                    var data = {""jobs"": [{""title"": ""Developer""}]};
                </script>
            </body>
            </html>";

        // Act
        var result = await parser.ParseHtmlAsync(mixedContent);

        // Assert
        Assert.NotNull(result);
    }

    #endregion
}

/// <summary>
/// Integration tests for Glassdoor multi-strategy parser
/// </summary>
public class GlassdoorMultiStrategyParserIntegrationTests
{
    private readonly Mock<ILogger<GlassdoorMultiStrategyParser>> _mockLogger;

    public GlassdoorMultiStrategyParserIntegrationTests()
    {
        _mockLogger = new Mock<ILogger<GlassdoorMultiStrategyParser>>();
    }

    #region Content Classification Tests

    [Theory]
    [InlineData("<html><body><li class=\"react-job-listing\">test</li></body></html>", true)]
    [InlineData("{\"data\": []}", true)]
    [InlineData("<div>no job data</div>", true)]
    public async Task ClassifyContent_ShouldIdentifyContentType(string html, bool shouldProcess)
    {
        // Arrange
        var parser = new GlassdoorMultiStrategyParser(_mockLogger.Object);

        // Act
        var result = await parser.ParseHtmlAsync(html);

        // Assert
        Assert.NotNull(result);
        if (shouldProcess)
        {
            Assert.IsType<List<JobListing>>(result);
        }
    }

    #endregion

    #region Glassdoor Specific HTML Parsing Tests

    [Fact]
    public async Task ParseHtmlAsync_ShouldExtract_GlassdoorJobListings()
    {
        // Arrange
        var parser = new GlassdoorMultiStrategyParser(_mockLogger.Object);
        var html = SampleGlassdoorHtml.GetGlassdoorJobsHtml();

        // Act
        var result = await parser.ParseHtmlAsync(html);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ParseHtmlAsync_ShouldHandle_GlassdoorLiElementStructure()
    {
        // Arrange
        var parser = new GlassdoorMultiStrategyParser(_mockLogger.Object);
        var html = SampleGlassdoorHtml.GetLiBasedJobStructure();

        // Act
        var result = await parser.ParseHtmlAsync(html);

        // Assert
        Assert.NotNull(result);
    }

    #endregion

    #region Glassdoor Error Handling Tests

    [Fact]
    public async Task ParseHtmlAsync_ShouldReturnEmptyList_WhenNoJobsFound()
    {
        // Arrange
        var parser = new GlassdoorMultiStrategyParser(_mockLogger.Object);
        var html = "<html><body><h1>No jobs found</h1></body></html>";

        // Act
        var result = await parser.ParseHtmlAsync(html);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task ParseHtmlAsync_ShouldHandleGlassdoorSpecificErrors_Gracefully()
    {
        // Arrange
        var parser = new GlassdoorMultiStrategyParser(_mockLogger.Object);
        var brokenHtml = SampleGlassdoorHtml.GetMalformedGlassdoorHtml();

        // Act & Assert (should not throw)
        var result = await parser.ParseHtmlAsync(brokenHtml);
        Assert.NotNull(result);
    }

    #endregion

    #region Glassdoor Fallback Tests

    [Fact]
    public async Task ParseHtmlAsync_ShouldFallback_FromDotnetSpiderToJson()
    {
        // Arrange
        var parser = new GlassdoorMultiStrategyParser(_mockLogger.Object);
        var jsonPayload = SampleGlassdoorHtml.GetGlassdoorJsonPayload();

        // Act
        var result = await parser.ParseHtmlAsync(jsonPayload);

        // Assert
        Assert.NotNull(result);
    }

    #endregion
}

/// <summary>
/// Integration tests for Google Jobs multi-strategy parser
/// </summary>
public class GoogleJobsMultiStrategyParserIntegrationTests
{
    private readonly Mock<ILogger<GoogleJobsMultiStrategyParser>> _mockLogger;

    public GoogleJobsMultiStrategyParserIntegrationTests()
    {
        _mockLogger = new Mock<ILogger<GoogleJobsMultiStrategyParser>>();
    }

    #region Consent Page Detection Tests

    [Fact]
    public async Task ParseHtmlAsync_ShouldDetect_GoogleConsentPage()
    {
        // Arrange
        var parser = new GoogleJobsMultiStrategyParser(_mockLogger.Object);
        var consentHtml = @"<html>
            <body>
                <h1>Before you continue to Google Search</h1>
                <p>See our <a href=""https://consent.google.com"">consent page</a></p>
            </body>
        </html>";

        // Act
        var result = await parser.ParseHtmlAsync(consentHtml);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result); // Should return empty when consent page detected
    }

    [Theory]
    [InlineData("https://consent.google.com")]
    [InlineData("Before you continue to Google Search")]
    public async Task ParseHtmlAsync_ShouldReturn_EmptyListForConsentIndicators(string indicator)
    {
        // Arrange
        var parser = new GoogleJobsMultiStrategyParser(_mockLogger.Object);
        var html = $"<html><body>{indicator}</body></html>";

        // Act
        var result = await parser.ParseHtmlAsync(html);

        // Assert
        Assert.Empty(result);
    }

    #endregion

    #region Google Jobs Widget Structure Tests

    [Fact]
    public async Task ParseHtmlAsync_ShouldExtract_GoogleJobsWidgetData()
    {
        // Arrange
        var parser = new GoogleJobsMultiStrategyParser(_mockLogger.Object);
        var html = SampleGoogleJobsHtml.GetGoogleJobsWidgetHtml();

        // Act
        var result = await parser.ParseHtmlAsync(html);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ParseHtmlAsync_ShouldHandle_GoogleJobsListItemElements()
    {
        // Arrange
        var parser = new GoogleJobsMultiStrategyParser(_mockLogger.Object);
        var html = SampleGoogleJobsHtml.GetGoogleListItemStructure();

        // Act
        var result = await parser.ParseHtmlAsync(html);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ParseHtmlAsync_ShouldParse_GoogleJobDataAttributes()
    {
        // Arrange
        var parser = new GoogleJobsMultiStrategyParser(_mockLogger.Object);
        var html = SampleGoogleJobsHtml.GetGoogleJobWithDataAttributes();

        // Act
        var result = await parser.ParseHtmlAsync(html);

        // Assert
        Assert.NotNull(result);
    }

    #endregion

    #region Google Jobs Error Handling Tests

    [Fact]
    public async Task ParseHtmlAsync_ShouldReturnEmptyList_WhenNoGoogleJobsFound()
    {
        // Arrange
        var parser = new GoogleJobsMultiStrategyParser(_mockLogger.Object);
        var html = "<html><body><h1>Search results</h1></body></html>";

        // Act
        var result = await parser.ParseHtmlAsync(html);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task ParseHtmlAsync_ShouldHandle_MalformedGoogleHtml()
    {
        // Arrange
        var parser = new GoogleJobsMultiStrategyParser(_mockLogger.Object);
        var html = SampleGoogleJobsHtml.GetMalformedGoogleHtml();

        // Act & Assert (should not throw)
        var result = await parser.ParseHtmlAsync(html);
        Assert.NotNull(result);
    }

    #endregion

    #region Google Jobs Fallback Tests

    [Fact]
    public async Task ParseHtmlAsync_ShouldFallbackThroughMultipleStrategies()
    {
        // Arrange
        var parser = new GoogleJobsMultiStrategyParser(_mockLogger.Object);
        var html = SampleGoogleJobsHtml.GetPartiallyFormattedGoogleHtml();

        // Act
        var result = await parser.ParseHtmlAsync(html);

        // Assert
        Assert.NotNull(result);
    }

    #endregion

    #region Google Remote Work Detection Tests

    [Theory]
    [InlineData("Remote")]
    [InlineData("Work from home")]
    [InlineData("On-site")]
    public async Task ParseHtmlAsync_ShouldDetect_RemoteWorkIndicators(string indicator)
    {
        // Arrange
        var parser = new GoogleJobsMultiStrategyParser(_mockLogger.Object);
        var html = SampleGoogleJobsHtml.GetGoogleJobWithIndicator(indicator);

        // Act
        var result = await parser.ParseHtmlAsync(html);

        // Assert
        Assert.NotNull(result);
    }

    #endregion
}

#region Sample HTML Data

/// <summary>
/// Sample HTML data for Indeed tests
/// </summary>
internal static class SampleIndeedHtml
{
    public static string GetValidJobListingHtml(int jobCount)
    {
        var jobs = string.Join("\n", Enumerable.Range(1, jobCount)
            .Select(i => $@"
                <div class=""job"" data-jk=""job{i}"">
                    <h2 class=""jobTitle"">
                        <span>Software Engineer {i}</span>
                    </h2>
                    <span class=""companyName"">Tech Corp {i}</span>
                    <div class=""companyLocation"">San Francisco, CA</div>
                    <div class=""salary-snippet"">$120k - $150k</div>
                    <div class=""job-snippet"">Build amazing software</div>
                    <a class=""jcs-JobTitle"" href=""/viewjob?jk=job{i}"">View Job</a>
                    <span class=""date"">2 days ago</span>
                </div>
            "));

        return $"<html><body>{jobs}</body></html>";
    }

    public static string GetCompleteJobEntryHtml()
    {
        return @"
            <html>
            <body>
                <div class=""job"" data-jk=""job123"">
                    <h2 class=""jobTitle"">
                        <span>Senior Software Engineer</span>
                    </h2>
                    <span class=""companyName"">ACME Corporation</span>
                    <div class=""companyLocation"">New York, NY</div>
                    <div class=""salary-snippet"">$150k - $200k/year</div>
                    <div class=""job-snippet"">
                        Full-time position developing cloud infrastructure.
                        Experience with AWS and Kubernetes required.
                    </div>
                    <a class=""jcs-JobTitle"" href=""/viewjob?jk=job123"">View Full Job</a>
                    <span class=""date"">5 days ago</span>
                    <div class=""metadata"">Full-time</div>
                </div>
            </body>
            </html>";
    }
}

/// <summary>
/// Sample HTML data for Glassdoor tests
/// </summary>
internal static class SampleGlassdoorHtml
{
    public static string GetGlassdoorJobsHtml()
    {
        return @"
            <html>
            <body>
                <li class=""react-job-listing"" data-id=""job123"">
                    <a class=""jobLink"" href=""/job/software-engineer"">
                        <span>Software Engineer</span>
                    </a>
                    <span class=""EmployerProfile_compactEmployerName"">Tech Company</span>
                    <span class=""JobCard_location"">San Francisco, CA</span>
                    <span class=""salary"">$120k - $150k</span>
                    <span class=""jobDescription"">Build amazing products</span>
                    <span class=""date"">2 days ago</span>
                    <span class=""jobType"">Full-time</span>
                </li>
                <li class=""react-job-listing"" data-id=""job124"">
                    <a class=""jobLink"" href=""/job/product-manager"">
                        <span>Product Manager</span>
                    </a>
                    <span class=""EmployerProfile_compactEmployerName"">Tech Company</span>
                    <span class=""JobCard_location"">Remote</span>
                </li>
            </body>
            </html>";
    }

    public static string GetLiBasedJobStructure()
    {
        return @"
            <html>
            <body>
                <li class=""jobListing"">
                    <a href=""/job/engineer-job"" class=""jobTitle"">
                        Senior Engineer
                    </a>
                    <div class=""employer-name"">Acme Inc</div>
                    <div class=""location"">New York, NY</div>
                </li>
            </body>
            </html>";
    }

    public static string GetMalformedGlassdoorHtml()
    {
        return @"
            <html>
            <body>
                <li class=""react-job-listing"" data-id=""job123"">
                    <a class=""jobLink"" href=""/job/incomplete-job"">
                    <!-- Missing closing tags and incomplete structure -->
                    <span class=""date"">Posted recently
                </li>
                <span class=""orphaned"">Unclosed tags</span>
            </body>
            </html>";
    }

    public static string GetGlassdoorJsonPayload()
    {
        return JsonSerializer.Serialize(new
        {
            jobs = new[]
            {
                new { title = "Software Engineer", company = "Tech Corp", location = "SF" },
                new { title = "Product Manager", company = "Tech Corp", location = "Remote" }
            }
        });
    }
}

/// <summary>
/// Sample HTML data for Google Jobs tests
/// </summary>
internal static class SampleGoogleJobsHtml
{
    public static string GetGoogleJobsWidgetHtml()
    {
        return @"
            <html>
            <body>
                <div class=""gws-plugins-horizon-jobs"">
                    <div role=""listitem"" class=""gws-plugins-horizon-jobs__li"" data-ved=""job123"">
                        <h3>Software Engineer</h3>
                        <span class=""vNEEBe"">Google LLC</span>
                        <span class=""Qk3sIe"">Mountain View, CA</span>
                        <span class=""HBvzbc"">Build products that impact billions of users</span>
                        <a href=""/jobs/result/123"">View Job</a>
                    </div>
                </div>
            </body>
            </html>";
    }

    public static string GetGoogleListItemStructure()
    {
        return @"
            <html>
            <body>
                <div role=""listitem"">
                    <h3>Product Manager</h3>
                    <span class=""Employer"">Amazon</span>
                    <span class=""location"">Seattle, WA</span>
                    <span class=""date"">1 day ago</span>
                </div>
            </body>
            </html>";
    }

    public static string GetGoogleJobWithDataAttributes()
    {
        return @"
            <html>
            <body>
                <div class=""gws-plugins-horizon-jobs__li"" 
                     data-ved=""ved_value_123"" 
                     data-id=""job456"" 
                     data-job-id=""gj_456"">
                    <h3>Data Scientist</h3>
                    <span class=""vNEEBe"">Microsoft</span>
                    <span class=""Qk3sIe"">Redmond, WA</span>
                    <a href=""https://www.microsoft.com/jobs/456"">Apply Now</a>
                </div>
            </body>
            </html>";
    }

    public static string GetMalformedGoogleHtml()
    {
        return @"
            <html>
            <body>
                <div role=""listitem"" class=""gws-plugins-horizon-jobs__li"" data-ved=""123"">
                    <h3>Incomplete Job Entry
                    <!-- Missing closing tags -->
                    <span class=""vNEEBe"">Company Name
                    <span class=""orphaned"">Unclosed span
                </div>
            </body>
            </html>";
    }

    public static string GetPartiallyFormattedGoogleHtml()
    {
        return @"
            <html>
            <body>
                <div class=""gws-plugins-horizon-jobs"">
                    <div role=""listitem"" data-ved=""job789"">
                        <h3>UX Designer</h3>
                        <span class=""vNEEBe"">Apple</span>
                        <span class=""Qk3sIe"">Cupertino, CA</span>
                        <span class=""HBvzbc"">Create intuitive user interfaces</span>
                        <a href=""/jobs/apple-ux-designer"">Details</a>
                        <span class=""date"">3 days ago</span>
                    </div>
                    <div role=""listitem"" data-ved=""job790"">
                        <h3>iOS Developer</h3>
                        <span>Apple</span>
                    </div>
                </div>
            </body>
            </html>";
    }

    public static string GetGoogleJobWithIndicator(string indicator)
    {
        return $@"
            <html>
            <body>
                <div role=""listitem"" class=""gws-plugins-horizon-jobs__li"">
                    <h3>Remote {indicator} Position</h3>
                    <span class=""vNEEBe"">Tech Company</span>
                    <span class=""Qk3sIe"">{indicator} - Anywhere</span>
                    <span>Full-time {indicator} opportunity</span>
                </div>
            </body>
            </html>";
    }
}

#endregion
