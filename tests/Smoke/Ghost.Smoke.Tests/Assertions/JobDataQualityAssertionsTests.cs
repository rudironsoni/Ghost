using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Ghost.Contracts.Jobs;
using Xunit;

namespace Ghost.Smoke.Tests.Assertions;

public class JobDataQualityAssertionsTests
{
    private static readonly DateTimeOffset RecentDate = DateTimeOffset.UtcNow.AddDays(-10);
    private static readonly DateTimeOffset OldDate = DateTimeOffset.UtcNow.AddDays(-100);

    private static JobListing CreateValidJob(string platform = "LinkedIn")
    {
        return new JobListing
        {
            Id = $"{platform}-job12345",
            Title = "Senior Software Engineer",
            Company = "Tech Corp",
            Location = "San Francisco, CA",
            Description = "A great job opportunity",
            Salary = "$150,000 - $200,000",
            JobType = JobType.FullTime,
            ExperienceLevel = ExperienceLevel.Senior,
            PostedAt = RecentDate,
            Remote = true,
            Url = "https://example.com/job/12345",
            Source = platform,
            IsEasyApply = true
        };
    }

    #region AssertRealJobData

    [Fact]
    public void AssertRealJobData_WithValidJob_ShouldNotThrow()
    {
        // Arrange
        var job = CreateValidJob();

        // Act & Assert
        Action act = () => job.AssertRealJobData();
        act.Should().NotThrow();
    }

    [Fact]
    public void AssertRealJobData_WithNullJob_ShouldThrow()
    {
        // Arrange
        JobListing? job = null;

        // Act & Assert
        Action act = () => job!.AssertRealJobData();
        act.Should().Throw<Exception>()
            .WithMessage("*job listing must not be null*");
    }

    [Fact]
    public void AssertRealJobData_WithMissingRequiredField_ShouldThrow()
    {
        // Arrange
        var job = new JobListing
        {
            Id = "LinkedIn-job12345",
            Title = "", // Empty title
            Company = "Tech Corp",
            Url = "https://example.com/job/12345",
            Source = "LinkedIn",
            PostedAt = RecentDate
        };

        // Act & Assert
        Action act = () => job.AssertRealJobData();
        act.Should().Throw<Exception>()
            .WithMessage("*job Title must not be null or empty*");
    }

    #endregion

    #region AssertRealJobResults

    [Fact]
    public void AssertRealJobResults_WithValidJobs_ShouldNotThrow()
    {
        // Arrange
        var jobs = new[]
        {
            CreateValidJob("LinkedIn"),
            CreateValidJob("Indeed"),
            CreateValidJob("Glassdoor")
        };

        // Act & Assert
        Action act = () => jobs.AssertRealJobResults();
        act.Should().NotThrow();
    }

    [Fact]
    public void AssertRealJobResults_WithNullCollection_ShouldThrow()
    {
        // Arrange
        IEnumerable<JobListing>? jobs = null;

        // Act & Assert
        Action act = () => jobs!.AssertRealJobResults();
        act.Should().Throw<Exception>()
            .WithMessage("*job results collection must not be null*");
    }

    [Fact]
    public void AssertRealJobResults_WithEmptyCollection_ShouldThrow()
    {
        // Arrange
        var jobs = Array.Empty<JobListing>();

        // Act & Assert
        Action act = () => jobs.AssertRealJobResults();
        act.Should().Throw<Exception>()
            .WithMessage("*job results must contain at least one job*");
    }

    [Fact]
    public void AssertRealJobResults_WithInvalidJob_ShouldThrow()
    {
        // Arrange
        var jobs = new[]
        {
            CreateValidJob(),
            new JobListing
            {
                Id = "", // Invalid
                Title = "Test",
                Company = "Test",
                Url = "https://example.com",
                Source = "LinkedIn"
            }
        };

        // Act & Assert
        Action act = () => jobs.AssertRealJobResults();
        act.Should().Throw<Exception>()
            .WithMessage("*job Id must not be null or empty*");
    }

    #endregion

    #region AssertFreshData

    [Fact]
    public void AssertFreshData_WithRecentJob_ShouldNotThrow()
    {
        // Arrange
        var job = CreateValidJob() with { PostedAt = DateTimeOffset.UtcNow.AddDays(-10) };

        // Act & Assert
        Action act = () => job.AssertFreshData(TimeSpan.FromDays(90));
        act.Should().NotThrow();
    }

    [Fact]
    public void AssertFreshData_WithDefaultMaxAge_ShouldNotThrow()
    {
        // Arrange
        var job = CreateValidJob() with { PostedAt = DateTimeOffset.UtcNow.AddDays(-30) };

        // Act & Assert
        Action act = () => job.AssertFreshData();
        act.Should().NotThrow();
    }

    [Fact]
    public void AssertFreshData_WithOldJob_ShouldThrow()
    {
        // Arrange
        var job = CreateValidJob() with { PostedAt = DateTimeOffset.UtcNow.AddDays(-100) };

        // Act & Assert
        Action act = () => job.AssertFreshData(TimeSpan.FromDays(90));
        act.Should().Throw<Exception>()
            .WithMessage("*job data must be fresh*")
            .WithMessage("*posted within 90 days*");
    }

    [Fact]
    public void AssertFreshData_WithDefaultPostedAt_ShouldThrow()
    {
        // Arrange
        var job = CreateValidJob() with { PostedAt = default };

        // Act & Assert
        Action act = () => job.AssertFreshData();
        act.Should().Throw<Exception>()
            .WithMessage("*PostedAt must be set to a valid date*");
    }

    [Fact]
    public void AssertFreshData_WithNullJob_ShouldThrow()
    {
        // Arrange
        JobListing? job = null;

        // Act & Assert
        Action act = () => job!.AssertFreshData();
        act.Should().Throw<Exception>()
            .WithMessage("*job listing must not be null*");
    }

    #endregion

    #region AssertValidPlatformId

    [Fact]
    public void AssertValidPlatformId_WithValidId_ShouldNotThrow()
    {
        // Arrange
        var job = CreateValidJob("LinkedIn") with { Id = "LinkedIn-job12345" };

        // Act & Assert
        Action act = () => job.AssertValidPlatformId("LinkedIn");
        act.Should().NotThrow();
    }

    [Fact]
    public void AssertValidPlatformId_WithDifferentPlatformPrefix_ShouldThrow()
    {
        // Arrange
        var job = CreateValidJob("LinkedIn") with { Id = "Indeed-job12345" }; // Wrong platform prefix

        // Act & Assert
        Action act = () => job.AssertValidPlatformId("LinkedIn");
        act.Should().Throw<Exception>()
            .WithMessage("*job Id platform prefix must match expected platform*")
            .WithMessage("*Expected: 'LinkedIn'*")
            .WithMessage("*Actual prefix: 'Indeed'*");
    }

    [Fact]
    public void AssertValidPlatformId_WithEmptyId_ShouldThrow()
    {
        // Arrange
        var job = CreateValidJob() with { Id = "" };

        // Act & Assert
        Action act = () => job.AssertValidPlatformId("LinkedIn");
        act.Should().Throw<Exception>()
            .WithMessage("*job Id must not be null or empty*");
    }

    [Fact]
    public void AssertValidPlatformId_WithInvalidFormat_ShouldThrow()
    {
        // Arrange
        var job = CreateValidJob() with { Id = "invalid_id_no_dash" };

        // Act & Assert
        Action act = () => job.AssertValidPlatformId("LinkedIn");
        act.Should().Throw<Exception>()
            .WithMessage("*job Id must follow format*");
    }

    [Fact]
    public void AssertValidPlatformId_WithWhitespaceId_ShouldThrow()
    {
        // Arrange
        var job = CreateValidJob() with { Id = "   " };

        // Act & Assert
        Action act = () => job.AssertValidPlatformId("LinkedIn");
        act.Should().Throw<Exception>()
            .WithMessage("*job Id must not be empty or whitespace*");
    }

    #endregion

    #region AssertNoDuplicateJobs

    [Fact]
    public void AssertNoDuplicateJobs_WithUniqueJobs_ShouldNotThrow()
    {
        // Arrange
        var jobs = new[]
        {
            CreateValidJob("LinkedIn"),
            CreateValidJob("Indeed"),
            CreateValidJob("Glassdoor")
        };

        // Act & Assert
        Action act = () => jobs.AssertNoDuplicateJobs();
        act.Should().NotThrow();
    }

    [Fact]
    public void AssertNoDuplicateJobs_WithDuplicates_ShouldThrow()
    {
        // Arrange
        var job1 = CreateValidJob("LinkedIn");
        var job2 = CreateValidJob("Indeed") with { Id = job1.Id }; // Same ID

        var jobs = new[] { job1, job2 };

        // Act & Assert
        Action act = () => jobs.AssertNoDuplicateJobs();
        act.Should().Throw<Exception>()
            .WithMessage("*job results must not contain duplicate IDs*")
            .WithMessage("*Found 1 duplicate(s)*");
    }

    [Fact]
    public void AssertNoDuplicateJobs_WithMultipleDuplicates_ShouldThrow()
    {
        // Arrange
        var job1 = CreateValidJob("LinkedIn");
        var job2 = CreateValidJob("Indeed") with { Id = job1.Id };
        var job3 = CreateValidJob("Glassdoor") with { Id = job1.Id };

        var jobs = new[] { job1, job2, job3 };

        // Act & Assert
        Action act = () => jobs.AssertNoDuplicateJobs();
        act.Should().Throw<Exception>()
            .WithMessage("*Found 1 duplicate(s)*")
            .WithMessage("*3 occurrences*");
    }

    [Fact]
    public void AssertNoDuplicateJobs_WithNullCollection_ShouldThrow()
    {
        // Arrange
        IEnumerable<JobListing>? jobs = null;

        // Act & Assert
        Action act = () => jobs!.AssertNoDuplicateJobs();
        act.Should().Throw<Exception>()
            .WithMessage("*job results collection must not be null*");
    }

    #endregion

    #region AssertRequiredFields

    [Fact]
    public void AssertRequiredFields_WithAllFields_ShouldNotThrow()
    {
        // Arrange
        var job = CreateValidJob();

        // Act & Assert
        Action act = () => job.AssertRequiredFields();
        act.Should().NotThrow();
    }

    [Fact]
    public void AssertRequiredFields_WithNullId_ShouldThrow()
    {
        // Arrange
        var job = new JobListing
        {
            Id = null!,
            Title = "Test",
            Company = "Test",
            Url = "https://example.com",
            Source = "LinkedIn"
        };

        // Act & Assert
        Action act = () => job.AssertRequiredFields();
        act.Should().Throw<Exception>()
            .WithMessage("*job Id must not be null or empty*");
    }

    [Fact]
    public void AssertRequiredFields_WithEmptyId_ShouldThrow()
    {
        // Arrange
        var job = CreateValidJob() with { Id = "" };

        // Act & Assert
        Action act = () => job.AssertRequiredFields();
        act.Should().Throw<Exception>()
            .WithMessage("*job Id must not be null or empty*");
    }

    [Fact]
    public void AssertRequiredFields_WithWhitespaceId_ShouldThrow()
    {
        // Arrange
        var job = CreateValidJob() with { Id = "   " };

        // Act & Assert
        Action act = () => job.AssertRequiredFields();
        act.Should().Throw<Exception>()
            .WithMessage("*job Id must not be whitespace only*");
    }

    [Fact]
    public void AssertRequiredFields_WithShortTitle_ShouldThrow()
    {
        // Arrange
        var job = CreateValidJob() with { Title = "Dev" }; // Less than 5 chars

        // Act & Assert
        Action act = () => job.AssertRequiredFields();
        act.Should().Throw<Exception>()
            .WithMessage("*job Title must have reasonable length (5-200 chars)*")
            .WithMessage("*Actual length: 3*");
    }

    [Fact]
    public void AssertRequiredFields_WithLongTitle_ShouldThrow()
    {
        // Arrange
        var job = CreateValidJob() with { Title = new string('A', 201) }; // More than 200 chars

        // Act & Assert
        Action act = () => job.AssertRequiredFields();
        act.Should().Throw<Exception>()
            .WithMessage("*job Title must have reasonable length (5-200 chars)*")
            .WithMessage("*Actual length: 201*");
    }

    [Fact]
    public void AssertRequiredFields_WithEmptyCompany_ShouldThrow()
    {
        // Arrange
        var job = CreateValidJob() with { Company = "" };

        // Act & Assert
        Action act = () => job.AssertRequiredFields();
        act.Should().Throw<Exception>()
            .WithMessage("*job Company must not be null or empty*");
    }

    [Fact]
    public void AssertRequiredFields_WithNullUrl_ShouldThrow()
    {
        // Arrange
        var job = new JobListing
        {
            Id = "LinkedIn-job12345",
            Title = "Software Engineer",
            Company = "Tech Corp",
            Url = null!,
            Source = "LinkedIn"
        };

        // Act & Assert
        Action act = () => job.AssertRequiredFields();
        act.Should().Throw<Exception>()
            .WithMessage("*job Url must not be null or empty*");
    }

    [Fact]
    public void AssertRequiredFields_WithInvalidUrl_ShouldThrow()
    {
        // Arrange
        var job = CreateValidJob() with { Url = "not-a-valid-url" };

        // Act & Assert
        Action act = () => job.AssertRequiredFields();
        act.Should().Throw<Exception>()
            .WithMessage("*job Url must be a valid absolute URL*");
    }

    [Fact]
    public void AssertRequiredFields_WithNullSource_ShouldThrow()
    {
        // Arrange
        var job = new JobListing
        {
            Id = "LinkedIn-job12345",
            Title = "Software Engineer",
            Company = "Tech Corp",
            Url = "https://example.com",
            Source = null!
        };

        // Act & Assert
        Action act = () => job.AssertRequiredFields();
        act.Should().Throw<Exception>()
            .WithMessage("*job Source must not be null or empty*");
    }

    #endregion

    #region AssertUrlReachable

    [Fact]
    public void AssertUrlReachable_WithValidUrl_ShouldNotThrow()
    {
        // Arrange
        var job = CreateValidJob() with { Url = "https://example.com/job/12345" };

        // Act & Assert
        Action act = () => job.AssertUrlReachable();
        act.Should().NotThrow();
    }

    [Fact]
    public void AssertUrlReachable_WithHttpUrl_ShouldNotThrow()
    {
        // Arrange
        var job = CreateValidJob() with { Url = "http://example.com/job/12345" };

        // Act & Assert
        Action act = () => job.AssertUrlReachable();
        act.Should().NotThrow();
    }

    [Fact]
    public void AssertUrlReachable_WithNullUrl_ShouldThrow()
    {
        // Arrange
        var job = new JobListing
        {
            Id = "LinkedIn-job12345",
            Title = "Test",
            Company = "Test",
            Url = null!,
            Source = "LinkedIn"
        };

        // Act & Assert
        Action act = () => job.AssertUrlReachable();
        act.Should().Throw<Exception>()
            .WithMessage("*job Url must not be null or empty*");
    }

    [Fact]
    public void AssertUrlReachable_WithEmptyUrl_ShouldThrow()
    {
        // Arrange
        var job = CreateValidJob() with { Url = "" };

        // Act & Assert
        Action act = () => job.AssertUrlReachable();
        act.Should().Throw<Exception>()
            .WithMessage("*job Url must not be null or empty*");
    }

    [Fact]
    public void AssertUrlReachable_WithInvalidUrl_ShouldThrow()
    {
        // Arrange
        var job = CreateValidJob() with { Url = "not-a-valid-url" };

        // Act & Assert
        Action act = () => job.AssertUrlReachable();
        act.Should().Throw<Exception>()
            .WithMessage("*job Url must be a valid absolute URL format*");
    }

    [Fact]
    public void AssertUrlReachable_WithLocalhostUrl_ShouldThrow()
    {
        // Arrange
        var job = CreateValidJob() with { Url = "http://localhost:5000/job/12345" };

        // Act & Assert
        Action act = () => job.AssertUrlReachable();
        act.Should().Throw<Exception>()
            .WithMessage("*job Url should not point to localhost*");
    }

    [Fact]
    public void AssertUrlReachable_WithFileUrl_ShouldThrow()
    {
        // Arrange
        var job = CreateValidJob() with { Url = "file:///path/to/file.html" };

        // Act & Assert
        Action act = () => job.AssertUrlReachable();
        act.Should().Throw<Exception>()
            .WithMessage("*job Url must be a valid absolute URL format*");
    }

    #endregion
}
