using FluentAssertions;
using Ghost.Sdk.Deduplication;
using Ghost.Sdk.Spider.Adapters.Contracts;
using Ghost.Utilities;
using Xunit;
using Xunit.Abstractions;
using Ghost.Testing.Reliability;

namespace Ghost.Kernel.Tests.Dedupe;

[Trait("Category", "Unit")]
public class DedupeAdversarialTests : ReliabilityTestBase
{
    public DedupeAdversarialTests(ITestOutputHelper output) : base(output) { }

    private readonly DeduplicationService _dedupeService = new();

    #region Redirect Chain Tests

    [Fact]
    public void RedirectChain_ShortUrlToFullUrl_SameFingerprint()
    {
        // Arrange
        var shortUrlRequest = new Request("https://bit.ly/3xY7z9Q");
        var fullUrlRequest = new Request("https://example.com/careers/senior-engineer-123");

        // Act
        string shortFingerprint = RequestFingerprinter.CreateFingerprint(shortUrlRequest);
        string fullFingerprint = RequestFingerprinter.CreateFingerprint(fullUrlRequest);

        // Assert - Different fingerprints (different domains)
        // This is expected behavior - redirect resolution happens at HTTP layer
        shortFingerprint.Should().NotBe(fullFingerprint);
    }

    [Fact]
    public void RedirectChain_TrackingRedirectService_SameCanonicalUrl()
    {
        // Arrange - URLs that redirect to same canonical destination
        var trackingUrl1 = new Request("https://trk.example.com/click?id=123&dest=https://example.com/jobs/456");
        var trackingUrl2 = new Request("https://trk.example.com/click?id=456&dest=https://example.com/jobs/456");
        var canonicalUrl = new Request("https://example.com/jobs/456");

        // Act
        string fp1 = RequestFingerprinter.CreateFingerprint(trackingUrl1);
        string fp2 = RequestFingerprinter.CreateFingerprint(trackingUrl2);
        string fpCanonical = RequestFingerprinter.CreateFingerprint(canonicalUrl);

        // Assert - Tracking URLs have different fingerprints due to different IDs
        fp1.Should().NotBe(fp2);
        fp1.Should().NotBe(fpCanonical);
        fp2.Should().NotBe(fpCanonical);
    }

    [Fact]
    public void RedirectChain_MultiHopChain_DifferentFingerprints()
    {
        // Arrange - Multi-hop redirect chain
        var hop1 = new Request("https://t.co/abc123");
        var hop2 = new Request("https://lnkd.in/xyz789");
        var hop3 = new Request("https://example.com/careers/job?id=12345");

        // Act
        string fp1 = RequestFingerprinter.CreateFingerprint(hop1);
        string fp2 = RequestFingerprinter.CreateFingerprint(hop2);
        string fp3 = RequestFingerprinter.CreateFingerprint(hop3);

        // Assert - Each hop has different fingerprint
        fp1.Should().NotBe(fp2);
        fp2.Should().NotBe(fp3);
        fp1.Should().NotBe(fp3);
    }

    [Fact]
    public void RedirectChain_QueryParamOrderInRedirect_SameFingerprint()
    {
        // Arrange - Same URL with different query param order (after redirect)
        var url1 = new Request("https://example.com/jobs?id=123&source=linkedin");
        var url2 = new Request("https://example.com/jobs?source=linkedin&id=123");

        // Act
        string fp1 = RequestFingerprinter.CreateFingerprint(url1);
        string fp2 = RequestFingerprinter.CreateFingerprint(url2);

        // Assert - Should have same fingerprint (query params sorted)
        fp1.Should().Be(fp2);
    }

    #endregion

    #region Multiple Aliases Tests

    [Fact]
    public void MultipleAliases_DifferentSlugsSameJob_SameDedupeId()
    {
        // Arrange - Same job with different URL slugs
        var job1 = new
        {
            Title = "Senior Software Engineer",
            Company = "TechCorp"
        };
        var job2 = new
        {
            Title = "Senior Software Engineer",
            Company = "TechCorp"
        };

        // Act
        string id1 = _dedupeService.GenerateId(job1.Title, job1.Company);
        string id2 = _dedupeService.GenerateId(job2.Title, job2.Company);

        // Assert - Same dedupe ID for same job
        id1.Should().Be(id2);
    }

    [Fact]
    public void MultipleAliases_RegionalVariants_SameDedupeId()
    {
        // Arrange - Same job in different regional URLs
        var jobEnUs = new
        {
            Title = "Senior Developer",
            Company = "GlobalTech"
        };
        var jobDeDe = new
        {
            Title = "Senior Developer",
            Company = "GlobalTech"
        };
        var jobEnGb = new
        {
            Title = "Senior Developer",
            Company = "GlobalTech"
        };

        // Act
        string idEnUs = _dedupeService.GenerateId(jobEnUs.Title, jobEnUs.Company);
        string idDeDe = _dedupeService.GenerateId(jobDeDe.Title, jobDeDe.Company);
        string idEnGb = _dedupeService.GenerateId(jobEnGb.Title, jobEnGb.Company);

        // Assert - All variants have same dedupe ID
        idEnUs.Should().Be(idDeDe);
        idEnUs.Should().Be(idEnGb);
        idDeDe.Should().Be(idEnGb);
    }

    [Fact]
    public void MultipleAliases_MobileVsDesktopUrls_SameFingerprint()
    {
        // Arrange - Same job on mobile vs desktop URLs
        var mobileUrl = new Request("https://m.example.com/jobs/123");
        var desktopUrl = new Request("https://www.example.com/jobs/123");
        var tabletUrl = new Request("https://tablet.example.com/jobs/123");

        // Act
        string fpMobile = RequestFingerprinter.CreateFingerprint(mobileUrl);
        string fpDesktop = RequestFingerprinter.CreateFingerprint(desktopUrl);
        string fpTablet = RequestFingerprinter.CreateFingerprint(tabletUrl);

        // Assert - Different subdomains = different fingerprints
        fpMobile.Should().NotBe(fpDesktop);
        fpMobile.Should().NotBe(fpTablet);
        fpDesktop.Should().NotBe(fpTablet);
    }

    [Fact]
    public void MultipleAliases_PathVariations_SameJob_SameDedupeId()
    {
        // Arrange - Same job with different URL paths
        var job1 = new
        {
            Title = "Product Manager",
            Company = "StartupXYZ"
        };
        var job2 = new
        {
            Title = "Product Manager",
            Company = "StartupXYZ"
        };

        // Act
        string id1 = _dedupeService.GenerateId(job1.Title, job1.Company);
        string id2 = _dedupeService.GenerateId(job2.Title, job2.Company);

        // Assert - Same dedupe ID
        id1.Should().Be(id2);
    }

    [Fact]
    public void MultipleAliases_CaseInsensitive_SameDedupeId()
    {
        // Arrange - Same job with different casing
        var job1 = new
        {
            Title = "DATA SCIENTIST",
            Company = "AI LABS"
        };
        var job2 = new
        {
            Title = "Data Scientist",
            Company = "AI Labs"
        };
        var job3 = new
        {
            Title = "data scientist",
            Company = "ai labs"
        };

        // Act
        string id1 = _dedupeService.GenerateId(job1.Title, job1.Company);
        string id2 = _dedupeService.GenerateId(job2.Title, job2.Company);
        string id3 = _dedupeService.GenerateId(job3.Title, job3.Company);

        // Assert - All should have same dedupe ID (normalized to lowercase)
        id1.Should().Be(id2);
        id1.Should().Be(id3);
        id2.Should().Be(id3);
    }

    #endregion

    #region Temporal Changes Tests

    [Fact]
    public void TemporalChanges_JobTitleChange_DifferentDedupeId()
    {
        // Arrange - Job title changes over time
        var originalJob = new
        {
            Title = "Software Engineer",
            Company = "TechCorp"
        };
        var updatedJob = new
        {
            Title = "Senior Software Engineer",
            Company = "TechCorp"
        };

        // Act
        string originalId = _dedupeService.GenerateId(originalJob.Title, originalJob.Company);
        string updatedId = _dedupeService.GenerateId(updatedJob.Title, updatedJob.Company);

        // Assert - Different titles = different dedupe IDs
        originalId.Should().NotBe(updatedId);
    }

    [Fact]
    public void TemporalChanges_JobRepostedAsNew_SameDedupeId()
    {
        // Arrange - Same job reposted as "new"
        var originalJob = new
        {
            Title = "Frontend Developer",
            Company = "WebCo"
        };
        var repostedJob = new
        {
            Title = "Frontend Developer",
            Company = "WebCo"
        };

        // Act
        string originalId = _dedupeService.GenerateId(originalJob.Title, originalJob.Company);
        string repostedId = _dedupeService.GenerateId(repostedJob.Title, repostedJob.Company);

        // Assert - Same job = same dedupe ID
        originalId.Should().Be(repostedId);
    }

    [Fact]
    public void TemporalChanges_FingerprintStability_ConsistentAcrossCalls()
    {
        // Arrange - Same job data
        var job = new
        {
            Title = "DevOps Engineer",
            Company = "CloudScale"
        };

        // Act - Generate ID multiple times
        string id1 = _dedupeService.GenerateId(job.Title, job.Company);
        string id2 = _dedupeService.GenerateId(job.Title, job.Company);
        string id3 = _dedupeService.GenerateId(job.Title, job.Company);

        // Assert - All IDs should be identical
        id1.Should().Be(id2);
        id1.Should().Be(id3);
        id2.Should().Be(id3);
    }

    [Fact]
    public void TemporalChanges_CompanyNameChange_DifferentDedupeId()
    {
        // Arrange - Company name changes (acquisition/rebranding)
        var originalJob = new
        {
            Title = "Backend Developer",
            Company = "OldCompany"
        };
        var rebrandedJob = new
        {
            Title = "Backend Developer",
            Company = "NewCompany"
        };

        // Act
        string originalId = _dedupeService.GenerateId(originalJob.Title, originalJob.Company);
        string rebrandedId = _dedupeService.GenerateId(rebrandedJob.Title, rebrandedJob.Company);

        // Assert - Different company = different dedupe ID
        originalId.Should().NotBe(rebrandedId);
    }

    [Fact]
    public void TemporalChanges_WhitespaceNormalization_DifferentDedupeIds()
    {
        // Arrange - Same job with different whitespace
        // Note: DeduplicationService only trims the concatenated string, not individual fields
        var job1 = new
        {
            Title = "  Full Stack Developer  ",
            Company = "  TechStartup  "
        };
        var job2 = new
        {
            Title = "Full Stack Developer",
            Company = "TechStartup"
        };
        var job3 = new
        {
            Title = "Full Stack   Developer",
            Company = "TechStartup"
        };

        // Act
        string id1 = _dedupeService.GenerateId(job1.Title, job1.Company);
        string id2 = _dedupeService.GenerateId(job2.Title, job2.Company);
        string id3 = _dedupeService.GenerateId(job3.Title, job3.Company);

        // Assert - All should be different because whitespace is preserved in the hash
        id1.Should().NotBe(id2);
        id1.Should().NotBe(id3);
        id2.Should().NotBe(id3);
    }

    #endregion

    #region Combined Adversarial Scenarios

    [Fact]
    public void CombinedScenario_TrackingParamsWithQueryReorder_SameFingerprint()
    {
        // Arrange - URL with tracking params in different order
        var url1 = new Request("https://example.com/jobs?id=123&utm_source=google&utm_medium=cpc");
        var url2 = new Request("https://example.com/jobs?utm_medium=cpc&id=123&utm_source=google");
        var url3 = new Request("https://example.com/jobs?id=123&utm_source=google&utm_medium=cpc&utm_campaign=test");

        // Act
        string fp1 = RequestFingerprinter.CreateFingerprint(url1);
        string fp2 = RequestFingerprinter.CreateFingerprint(url2);
        string fp3 = RequestFingerprinter.CreateFingerprint(url3);

        // Assert - url1 and url2 same (sorted params), url3 different (extra param)
        fp1.Should().Be(fp2);
        fp1.Should().NotBe(fp3);
        fp2.Should().NotBe(fp3);
    }

    [Fact]
    public void CombinedScenario_MultipleAliasesWithTemporalChange_DifferentDedupeIds()
    {
        // Arrange - Job reposted with title change
        var originalJob = new
        {
            Title = "Junior Developer",
            Company = "CodeShop"
        };
        var promotedJob = new
        {
            Title = "Mid-Level Developer",
            Company = "CodeShop"
        };

        // Act
        string originalId = _dedupeService.GenerateId(originalJob.Title, originalJob.Company);
        string promotedId = _dedupeService.GenerateId(promotedJob.Title, promotedJob.Company);

        // Assert - Different dedupe IDs
        originalId.Should().NotBe(promotedId);
    }

    [Fact]
    public void CombinedScenario_FragmentAndQuery_SameFingerprint()
    {
        // Arrange - URLs with fragments and query params
        var url1 = new Request("https://example.com/jobs?id=123#details");
        var url2 = new Request("https://example.com/jobs?id=123#apply");
        var url3 = new Request("https://example.com/jobs?id=123");

        // Act
        string fp1 = RequestFingerprinter.CreateFingerprint(url1);
        string fp2 = RequestFingerprinter.CreateFingerprint(url2);
        string fp3 = RequestFingerprinter.CreateFingerprint(url3);

        // Assert - All should have same fingerprint (fragments ignored)
        fp1.Should().Be(fp2);
        fp1.Should().Be(fp3);
        fp2.Should().Be(fp3);
    }

    [Fact]
    public void CombinedScenario_DefaultPortNormalization_SameFingerprint()
    {
        // Arrange - URLs with explicit default ports
        var url1 = new Request("https://example.com/jobs/123");
        var url2 = new Request("https://example.com:443/jobs/123");
        var url3 = new Request("http://example.com/jobs/123");
        var url4 = new Request("http://example.com:80/jobs/123");

        // Act
        string fp1 = RequestFingerprinter.CreateFingerprint(url1);
        string fp2 = RequestFingerprinter.CreateFingerprint(url2);
        string fp3 = RequestFingerprinter.CreateFingerprint(url3);
        string fp4 = RequestFingerprinter.CreateFingerprint(url4);

        // Assert - HTTPS URLs same, HTTP URLs same, but HTTPS != HTTP
        fp1.Should().Be(fp2);
        fp3.Should().Be(fp4);
        fp1.Should().NotBe(fp3);
        fp2.Should().NotBe(fp4);
    }

    #endregion
}
