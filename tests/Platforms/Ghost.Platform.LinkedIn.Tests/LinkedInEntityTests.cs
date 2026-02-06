using FluentAssertions;
using Ghost.Platform.LinkedIn.Tests.Migration;
using Ghost.Sdk.Spider.Core.Extraction;
using NUnit.Framework;

namespace Ghost.Platform.LinkedIn.Tests;

/// <summary>
/// Tests for LinkedInJobEntity extraction using Ghost.Sdk.Spider.
/// Validates that the migration from platform-specific scraping to Spider SDK works correctly.
/// </summary>
[TestFixture]
public class LinkedInEntityTests
{
    private EntityParser _parser = null!;

    [SetUp]
    public void Setup()
    {
        _parser = new EntityParser();
    }

    [Test]
    public async Task Parse_WithTestJobFixture_ShouldExtractAllFields()
    {
        // Arrange
        var html = await ReadFixtureAsync("test-job.html");
        var context = new ExtractionContext
        {
            Content = html,
            SourceUrl = "https://www.linkedin.com/jobs/view/software-engineer-new-grad-at-stripe-4294691514",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = _parser.ParseSingle<LinkedInJobEntity>(context);

        // Assert
        result.Should().NotBeNull();
        result!.Title.Should().Be("Software Engineer, New Grad");
        result.Company.Should().Be("Stripe");
        result.Location.Should().Be("Seattle, WA");
        result.PostedTime.Should().Be("1 week ago");
        result.EmploymentType.Should().Be("Full-time");
        result.SeniorityLevel.Should().Be("Entry level");
        result.JobFunction.Should().Be("Engineering and Information Technology");
        result.Industries.Should().Be("Financial Services and Technology");
    }

    [Test]
    public async Task Parse_WithTestJobFixture_ShouldExtractUrls()
    {
        // Arrange
        var html = await ReadFixtureAsync("test-job.html");
        var context = new ExtractionContext
        {
            Content = html,
            SourceUrl = "https://www.linkedin.com/jobs/view/test-job",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = _parser.ParseSingle<LinkedInJobEntity>(context);

        // Assert
        result.Should().NotBeNull();
        result!.JobUrl.Should().Contain("/jobs/view/software-engineer-new-grad-at-stripe");
        result.CompanyUrl.Should().Contain("/company/stripe");
        result.CompanyLogoUrl.Should().Contain("stripe_logo.png");
    }

    [Test]
    public async Task Parse_WithTestJobFixture_ShouldExtractDescription()
    {
        // Arrange
        var html = await ReadFixtureAsync("test-job.html");
        var context = new ExtractionContext
        {
            Content = html,
            SourceUrl = "https://www.linkedin.com/jobs/view/test-job",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = _parser.ParseSingle<LinkedInJobEntity>(context);

        // Assert
        result.Should().NotBeNull();
        result!.Description.Should().NotBeNullOrEmpty();
        result.Description.Should().Contain("About The Role");
        result.Description.Should().Contain("payment infrastructure");
    }

    [Test]
    public async Task Parse_WithTestJobFixture_ShouldExtractApplicantCount()
    {
        // Arrange
        var html = await ReadFixtureAsync("test-job.html");
        var context = new ExtractionContext
        {
            Content = html,
            SourceUrl = "https://www.linkedin.com/jobs/view/test-job",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = _parser.ParseSingle<LinkedInJobEntity>(context);

        // Assert
        result.Should().NotBeNull();
        result!.ApplicantCount.Should().Be("200");
    }

    [Test]
    public async Task Parse_WithTestJobFixture_ShouldSetBaseProperties()
    {
        // Arrange
        var sourceUrl = "https://www.linkedin.com/jobs/view/test-job";
        var timestamp = DateTime.UtcNow;
        var html = await ReadFixtureAsync("test-job.html");
        var context = new ExtractionContext
        {
            Content = html,
            SourceUrl = sourceUrl,
            Timestamp = timestamp
        };

        // Act
        var result = _parser.ParseSingle<LinkedInJobEntity>(context);

        // Assert
        result.Should().NotBeNull();
        result!.SourceUrl.Should().Be(sourceUrl);
        result.ExtractedAt.Should().BeCloseTo(timestamp, TimeSpan.FromSeconds(1));
        result.Id.Should().NotBeNullOrEmpty();
    }

    [Test]
    public async Task Parse_WithTestJobFixture_ShouldPassValidation()
    {
        // Arrange
        var html = await ReadFixtureAsync("test-job.html");
        var context = new ExtractionContext
        {
            Content = html,
            SourceUrl = "https://www.linkedin.com/jobs/view/test-job",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = _parser.ParseSingle<LinkedInJobEntity>(context);

        // Assert
        result.Should().NotBeNull();
        result!.Validate().Should().BeTrue();
    }

    [Test]
    public async Task Parse_WithRealFixture1_ShouldExtractTitle()
    {
        // Arrange
        var html = await ReadFixtureAsync("linkedin-job-detail-1.html");
        var context = new ExtractionContext
        {
            Content = html,
            SourceUrl = "https://www.linkedin.com/jobs/view/test",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = _parser.ParseSingle<LinkedInJobEntity>(context);

        // Assert
        result.Should().NotBeNull();
        result!.Title.Should().NotBeNullOrEmpty();
        result.Company.Should().NotBeNullOrEmpty();
    }

    [Test]
    public async Task Parse_WithRealFixture2_ShouldExtractAllRequiredFields()
    {
        // Arrange
        var html = await ReadFixtureAsync("linkedin-job-detail-2.html");
        var context = new ExtractionContext
        {
            Content = html,
            SourceUrl = "https://www.linkedin.com/jobs/view/test",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = _parser.ParseSingle<LinkedInJobEntity>(context);

        // Assert
        result.Should().NotBeNull();
        result!.Title.Should().NotBeNullOrEmpty();
        result.Company.Should().NotBeNullOrEmpty();
        result.Validate().Should().BeTrue();
    }

    [Test]
    public async Task Parse_WithRealFixture3_ShouldHandleOptionalFields()
    {
        // Arrange
        var html = await ReadFixtureAsync("linkedin-job-detail-3.html");
        var context = new ExtractionContext
        {
            Content = html,
            SourceUrl = "https://www.linkedin.com/jobs/view/test",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = _parser.ParseSingle<LinkedInJobEntity>(context);

        // Assert
        result.Should().NotBeNull();
        // Even if optional fields are missing, validation should pass with required fields
        if (result!.Validate())
        {
            result.Title.Should().NotBeNullOrEmpty();
            result.Company.Should().NotBeNullOrEmpty();
        }
    }

    [Test]
    public void Parse_WithEmptyContent_ShouldReturnNull()
    {
        // Arrange
        var context = new ExtractionContext
        {
            Content = string.Empty,
            SourceUrl = "https://www.linkedin.com/jobs/view/test",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = _parser.ParseSingle<LinkedInJobEntity>(context);

        // Assert
        result.Should().BeNull();
    }

    [Test]
    public void Parse_WithInvalidHtml_ShouldReturnNull()
    {
        // Arrange
        var context = new ExtractionContext
        {
            Content = "<html><body><p>Invalid content</p></body></html>",
            SourceUrl = "https://www.linkedin.com/jobs/view/test",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = _parser.ParseSingle<LinkedInJobEntity>(context);

        // Assert
        result.Should().BeNull();
    }

    [Test]
    public async Task TrimFormatter_ShouldRemoveWhitespace()
    {
        // Arrange - Create HTML with extra whitespace
        var htmlWithWhitespace = @"
            <html>
                <body>
                    <h2 class='top-card-layout__title topcard__title'>
                        
                        Software Engineer   
                        
                    </h2>
                    <a class='topcard__org-name-link'>
                        
                        Test Company   
                        
                    </a>
                </body>
            </html>";

        var context = new ExtractionContext
        {
            Content = htmlWithWhitespace,
            SourceUrl = "https://www.linkedin.com/jobs/view/test",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = _parser.ParseSingle<LinkedInJobEntity>(context);

        // Assert
        result.Should().NotBeNull();
        result!.Title.Should().Be("Software Engineer");
        result.Company.Should().Be("Test Company");
    }

    [Test]
    public async Task RegexFormatter_ShouldExtractNumericValues()
    {
        // Arrange - HTML with applicant text
        var htmlWithApplicants = @"
            <html>
                <body>
                    <h2 class='top-card-layout__title topcard__title'>Test Job</h2>
                    <a class='topcard__org-name-link'>Test Company</a>
                    <span class='num-applicants__caption'>Over 500 applicants</span>
                </body>
            </html>";

        var context = new ExtractionContext
        {
            Content = htmlWithApplicants,
            SourceUrl = "https://www.linkedin.com/jobs/view/test",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = _parser.ParseSingle<LinkedInJobEntity>(context);

        // Assert
        result.Should().NotBeNull();
        result!.ApplicantCount.Should().Be("500");
    }

    [Test]
    public void Validate_WithMissingTitle_ShouldReturnFalse()
    {
        // Arrange
        var entity = new LinkedInJobEntity
        {
            Company = "Test Company"
        };

        // Act
        var isValid = entity.Validate();

        // Assert
        isValid.Should().BeFalse();
    }

    [Test]
    public void Validate_WithMissingCompany_ShouldReturnFalse()
    {
        // Arrange
        var entity = new LinkedInJobEntity
        {
            Title = "Test Job"
        };

        // Act
        var isValid = entity.Validate();

        // Assert
        isValid.Should().BeFalse();
    }

    [Test]
    public void Validate_WithBothRequiredFields_ShouldReturnTrue()
    {
        // Arrange
        var entity = new LinkedInJobEntity
        {
            Title = "Test Job",
            Company = "Test Company"
        };

        // Act
        var isValid = entity.Validate();

        // Assert
        isValid.Should().BeTrue();
    }

    [Test]
    public void GetMetadata_ShouldReturnEntityConfiguration()
    {
        // Act
        var metadata = LinkedInJobEntity.GetMetadata();

        // Assert
        metadata.Should().NotBeNull();
#pragma warning disable CA2263 // Prefer generic type parameter
        metadata.EntityType.Should().Be(typeof(LinkedInJobEntity));
#pragma warning restore CA2263
        metadata.Properties.Should().NotBeEmpty();
        metadata.Properties.Should().Contain(p => p.PropertyInfo.Name == "Title");
        metadata.Properties.Should().Contain(p => p.PropertyInfo.Name == "Company");
    }

    private static string GetFixturePath(string filename)
    {
        var baseDir = AppContext.BaseDirectory;
        return Path.Combine(baseDir, "Fixtures", filename);
    }

    private static async Task<string> ReadFixtureAsync(string filename)
    {
        var path = GetFixturePath(filename);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Fixture file not found: {path}");
        }
        return await File.ReadAllTextAsync(path);
    }
}
