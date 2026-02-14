using FluentAssertions;
using Ghost.Plugin.LinkedIn.Tests.Migration;
using Ghost.Sdk.Spider.Adapters.Contracts;
using Ghost.Sdk.Spider.Engine;
using Xunit;
using ExecutionContext = Ghost.Sdk.Spider.Engine.ExecutionContext;

namespace Ghost.Plugin.LinkedIn.Tests;

/// <summary>
/// Tests for LinkedInSpider using Ghost.Sdk.Spider framework.
/// Validates the full spider pipeline with JavaScriptAdapter integration.
/// </summary>
public class LinkedInSpiderTests
{
    private readonly LinkedInSpider _spider;

    public LinkedInSpiderTests()
    {
        _spider = new LinkedInSpider();
    }

    [Fact]
    public void NameShouldReturnLinkedInJobSpider()
    {
        // Assert
        _spider.Name.Should().Be("LinkedInJobSpider");
    }

    [Fact]
    public void GetStartUrlsShouldReturnLinkedInUrls()
    {
        // Act
        var urls = _spider.GetStartUrls().ToList();

        // Assert
        urls.Should().NotBeEmpty();
        urls.Should().Contain(u => u.Contains("linkedin.com/jobs"));
    }

    [Fact]
    public void OptionsShouldHaveLinkedInDomain()
    {
        // Assert
        _spider.Options.AllowedDomains.Should().Contain("linkedin.com");
    }

    [Fact]
    public void OptionsShouldExcludeAdminPages()
    {
        // Assert
        _spider.Options.ExcludePatterns.Should().Contain(@".*/admin/.*");
        _spider.Options.ExcludePatterns.Should().Contain(@".*/logout.*");
    }

    [Fact]
    public void OptionsShouldHaveReasonableDefaults()
    {
        // Assert
        _spider.Options.MaxDepth.Should().Be(2);
        _spider.Options.MaxConcurrency.Should().Be(5);
        _spider.Options.RequestDelay.Should().Be(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task ProcessResponseAsyncWithValidJobPageShouldExtractJob()
    {
        // Arrange
        var html = await ReadFixtureAsync("test-job.html");
        var response = CreateResponse(
            url: "https://www.linkedin.com/jobs/view/test-job",
            content: html,
            contentType: ContentType.JavaScript
        );
        var context = new ExecutionContext("Test", new SpiderOptions());

        // Act
        await _spider.ProcessResponseAsync(response, context);

        // Assert
        _spider.ExtractedJobs.Should().HaveCount(1);
        var job = _spider.ExtractedJobs[0];
        job.Title.Should().Be("Software Engineer, New Grad");
        job.Company.Should().Be("Stripe");
    }

    [Fact]
    public async Task ProcessResponseAsyncWithMultipleJobPagesShouldExtractAllJobs()
    {
        // Arrange
        var html1 = await ReadFixtureAsync("test-job.html");
        var html2 = CreateJobHtml("Backend Engineer", "Microsoft", "Redmond, WA");

        var response1 = CreateResponse("https://www.linkedin.com/jobs/view/job1", html1, ContentType.JavaScript);
        var response2 = CreateResponse("https://www.linkedin.com/jobs/view/job2", html2, ContentType.JavaScript);
        var context = new ExecutionContext("Test", new SpiderOptions());

        // Act
        await _spider.ProcessResponseAsync(response1, context);
        await _spider.ProcessResponseAsync(response2, context);

        // Assert
        _spider.ExtractedJobs.Should().HaveCount(2);
        _spider.ExtractedJobs.Should().Contain(j => j.Company == "Stripe");
        _spider.ExtractedJobs.Should().Contain(j => j.Company == "Microsoft");
    }

    [Fact]
    public async Task ProcessResponseAsyncWithInvalidJobShouldNotExtract()
    {
        // Arrange - Job without required fields
        var invalidHtml = "<html><body><p>Not a job page</p></body></html>";
        var response = CreateResponse(
            "https://www.linkedin.com/jobs/view/invalid",
            invalidHtml,
            ContentType.JavaScript
        );
        var context = new ExecutionContext("Test", new SpiderOptions());

        // Act
        await _spider.ProcessResponseAsync(response, context);

        // Assert
        _spider.ExtractedJobs.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessResponseAsyncWithNonHtmlResponseShouldNotProcess()
    {
        // Arrange
        var response = CreateResponse(
            "https://www.linkedin.com/api/jobs.json",
            "{\"jobs\": []}",
            ContentType.Json
        );
        var context = new ExecutionContext("Test", new SpiderOptions());

        // Act
        await _spider.ProcessResponseAsync(response, context);

        // Assert
        _spider.ExtractedJobs.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessResponseAsyncWithFailedResponseShouldNotProcess()
    {
        // Arrange
        var response = CreateResponse(
            "https://www.linkedin.com/jobs/view/test",
            "",
            ContentType.JavaScript,
            statusCode: 404,
            isSuccess: false
        );
        var context = new ExecutionContext("Test", new SpiderOptions());

        // Act
        await _spider.ProcessResponseAsync(response, context);

        // Assert
        _spider.ExtractedJobs.Should().BeEmpty();
    }

    [Fact]
    public async Task OnStartAsyncShouldClearExtractedJobs()
    {
        // Arrange
        var context = new ExecutionContext("Test", new SpiderOptions());

        // Add a job first
        var html = await ReadFixtureAsync("test-job.html");
        var response = CreateResponse("https://www.linkedin.com/jobs/view/test", html, ContentType.JavaScript);
        await _spider.ProcessResponseAsync(response, context);

        _spider.ExtractedJobs.Should().HaveCount(1);

        // Act
        await _spider.OnStartAsync(context);

        // Assert
        _spider.ExtractedJobs.Should().BeEmpty();
    }

    [Fact]
    public async Task OnCompleteAsyncShouldBeCallable()
    {
        // Arrange
        var context = new ExecutionContext("Test", new SpiderOptions());
        var result = new SpiderResult
        {
            SpiderName = "LinkedInJobSpider",
            Success = true,
            RequestsProcessed = 10,
            RequestsSucceeded = 10
        };

        // Act
        Func<Task> act = async () => await _spider.OnCompleteAsync(context, result);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task OnErrorAsyncShouldHandleException()
    {
        // Arrange
        var context = new ExecutionContext("Test", new SpiderOptions());
        var exception = new InvalidOperationException("Test error");

        // Act
        Func<Task> act = async () => await _spider.OnErrorAsync(exception, context);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void ShouldFollowUrlWithJobViewUrlShouldReturnTrue()
    {
        // Arrange
        var context = new ExecutionContext("Test", new SpiderOptions());
        var url = "https://www.linkedin.com/jobs/view/software-engineer-at-company-123";

        // Act
        var shouldFollow = _spider.ShouldFollowUrl(url, context);

        // Assert
        shouldFollow.Should().BeTrue();
    }

    [Fact]
    public void ShouldFollowUrlWithJobSearchUrlShouldReturnTrue()
    {
        // Arrange
        var context = new ExecutionContext("Test", new SpiderOptions());
        var url = "https://www.linkedin.com/jobs/search/?keywords=engineer";

        // Act
        var shouldFollow = _spider.ShouldFollowUrl(url, context);

        // Assert
        shouldFollow.Should().BeTrue();
    }

    [Fact]
    public void ShouldFollowUrlWithNonJobUrlShouldReturnFalse()
    {
        // Arrange
        var context = new ExecutionContext("Test", new SpiderOptions());
        var url = "https://www.linkedin.com/feed/";

        // Act
        var shouldFollow = _spider.ShouldFollowUrl(url, context);

        // Assert
        shouldFollow.Should().BeFalse();
    }

    [Fact]
    public void ShouldFollowUrlWithAdminUrlShouldReturnFalse()
    {
        // Arrange
        var context = new ExecutionContext("Test", new SpiderOptions());
        var url = "https://www.linkedin.com/admin/dashboard";

        // Act
        var shouldFollow = _spider.ShouldFollowUrl(url, context);

        // Assert
        shouldFollow.Should().BeFalse();
    }

    [Fact]
    public void ShouldFollowUrlWithLogoutUrlShouldReturnFalse()
    {
        // Arrange
        var context = new ExecutionContext("Test", new SpiderOptions());
        var url = "https://www.linkedin.com/logout";

        // Act
        var shouldFollow = _spider.ShouldFollowUrl(url, context);

        // Assert
        shouldFollow.Should().BeFalse();
    }

    [Fact]
    public void ShouldFollowUrlWithNonLinkedInDomainShouldReturnFalse()
    {
        // Arrange
        var context = new ExecutionContext("Test", new SpiderOptions());
        var url = "https://www.example.com/jobs/view/test";

        // Act
        var shouldFollow = _spider.ShouldFollowUrl(url, context);

        // Assert
        shouldFollow.Should().BeFalse();
    }

    [Fact]
    public void ShouldFollowUrlWithInvalidUrlShouldReturnFalse()
    {
        // Arrange
        var context = new ExecutionContext("Test", new SpiderOptions());
        var url = "not-a-valid-url";

        // Act
        var shouldFollow = _spider.ShouldFollowUrl(url, context);

        // Assert
        shouldFollow.Should().BeFalse();
    }

    [Fact]
    public void ShouldFollowUrlWithNullUrlShouldReturnFalse()
    {
        // Arrange
        var context = new ExecutionContext("Test", new SpiderOptions());

        // Act
        var shouldFollow = _spider.ShouldFollowUrl(null!, context);

        // Assert
        shouldFollow.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessResponseAsyncWithRealFixturesShouldExtractValidJobs()
    {
        // Arrange
        var fixtures = new[]
        {
            "linkedin-job-detail-1.html",
            "linkedin-job-detail-2.html",
            "linkedin-job-detail-3.html"
        };

        var context = new ExecutionContext("Test", new SpiderOptions());

        // Act
        foreach (var fixture in fixtures)
        {
            if (File.Exists(GetFixturePath(fixture)))
            {
                var html = await ReadFixtureAsync(fixture);
                var response = CreateResponse(
                    $"https://www.linkedin.com/jobs/view/{fixture}",
                    html,
                    ContentType.JavaScript
                );
                await _spider.ProcessResponseAsync(response, context);
            }
        }

        // Assert
        _spider.ExtractedJobs.Should().NotBeEmpty();
        _spider.ExtractedJobs.Should().OnlyContain(j => j.Validate());
    }

    [Fact]
    public async Task ExtractedJobsShouldBeReadOnly()
    {
        // Arrange
        var html = await ReadFixtureAsync("test-job.html");
        var response = CreateResponse("https://www.linkedin.com/jobs/view/test", html, ContentType.JavaScript);
        var context = new ExecutionContext("Test", new SpiderOptions());

        await _spider.ProcessResponseAsync(response, context);

        // Act
        var extractedJobs = _spider.ExtractedJobs;

        // Assert
        extractedJobs.Should().BeAssignableTo<IReadOnlyList<LinkedInJobEntity>>();
    }

    private static Response CreateResponse(
        string url,
        string content,
        ContentType contentType,
        int statusCode = 200,
        bool isSuccess = true)
    {
        var contentResult = new ContentResult
        {
            Content = content,
            ContentType = contentType,
            MimeType = contentType switch
            {
                ContentType.JavaScript => "text/html",
                ContentType.StaticHtml => "text/html",
                ContentType.Json => "application/json",
                _ => "text/plain"
            },
            Encoding = "utf-8",
            ContentLength = content.Length,
            ExtractedAt = DateTimeOffset.UtcNow,
            Success = isSuccess
        };

        return new Response(contentResult)
        {
            StatusCode = statusCode,
            ReasonPhrase = statusCode == 200 ? "OK" : "Error",
            FinalUrl = url,
            AdapterName = "JavaScriptAdapter",
            IsSuccess = isSuccess,
            RequestedAt = DateTimeOffset.UtcNow.AddSeconds(-1),
            RespondedAt = DateTimeOffset.UtcNow
        };
    }

    private static string CreateJobHtml(string title, string company, string location)
    {
        return $@"
            <html>
                <body>
                    <section class='top-card-layout'>
                        <div class='top-card-layout__card'>
                            <a href='https://www.linkedin.com/jobs/view/test' class='topcard__link'>
                                <h2 class='top-card-layout__title topcard__title'>{title}</h2>
                            </a>
                            <h4 class='top-card-layout__second-subline'>
                                <div class='topcard__flavor-row'>
                                    <span class='topcard__flavor'>
                                        <a class='topcard__org-name-link' href='https://www.linkedin.com/company/test'>
                                            {company}
                                        </a>
                                    </span>
                                    <span class='topcard__flavor topcard__flavor--bullet'>
                                        {location}
                                    </span>
                                </div>
                            </h4>
                        </div>
                    </section>
                </body>
            </html>";
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
