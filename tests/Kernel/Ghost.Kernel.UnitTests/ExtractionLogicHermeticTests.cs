using FluentAssertions;
using Ghost.Kernel;
using Ghost.Testing.Fakes;
using Xunit;
using Xunit.Abstractions;
using Ghost.Testing.Reliability;

namespace Ghost.Kernel.Unit.Tests;

/// <summary>
/// Hermetic unit tests demonstrating conversion from integration tests.
/// These tests verify extraction logic without real browser dependencies.
/// </summary>
public class ExtractionLogicHermeticTests : ReliabilityTestBase
{
    public ExtractionLogicHermeticTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public async Task ShouldExtractJobTitle_UsingHermeticPage()
    {
        // Arrange - Create hermetic kernel and page
        var kernel = new StubGhostKernel();
        IBrowserSession session = await kernel.NewSessionAsync();
        IPage page = await session.NewPageAsync();

        // Setup test data in hermetic page
        var titleElement = new FakeElement();
        titleElement.SetTextContent("Senior Software Engineer");
        ((FakePage)page).RegisterElement("h1.job-title", titleElement);

        // Act
        IElement? element = await page.QuerySelectorAsync("h1.job-title");
        string? title = await element!.GetTextContentAsync();

        // Assert
        title.Should().Be("Senior Software Engineer");

        await kernel.DisposeAsync();
    }

    [Fact]
    public async Task ShouldExtractCompanyName_UsingHermeticPage()
    {
        // Arrange
        var kernel = new StubGhostKernel();
        IBrowserSession session = await kernel.NewSessionAsync();
        IPage page = await session.NewPageAsync();

        var companyElement = new FakeElement();
        companyElement.SetTextContent("Acme Corp");
        ((FakePage)page).RegisterElement(".company-name", companyElement);

        // Act
        IElement? element = await page.QuerySelectorAsync(".company-name");
        string? company = await element!.GetTextContentAsync();

        // Assert
        company.Should().Be("Acme Corp");

        await kernel.DisposeAsync();
    }

    [Fact]
    public async Task ShouldExtractJobUrl_UsingHermeticPage()
    {
        // Arrange
        var kernel = new StubGhostKernel();
        IBrowserSession session = await kernel.NewSessionAsync();
        IPage page = await session.NewPageAsync();

        var linkElement = new FakeElement();
        linkElement.SetAttribute("href", "https://example.com/jobs/12345");
        ((FakePage)page).RegisterElement("a.apply-link", linkElement);

        // Act
        IElement? element = await page.QuerySelectorAsync("a.apply-link");
        string? url = await element!.GetAttributeAsync("href");

        // Assert
        url.Should().Be("https://example.com/jobs/12345");

        await kernel.DisposeAsync();
    }

    [Fact]
    public async Task ShouldExtractMultipleJobCards_UsingHermeticPage()
    {
        // Arrange
        var kernel = new StubGhostKernel();
        IBrowserSession session = await kernel.NewSessionAsync();
        IPage page = await session.NewPageAsync();

        // Setup multiple job cards
        var job1 = new FakeElement();
        job1.SetTextContent("Job 1");
        job1.SetAttribute("data-job-id", "1");

        var job2 = new FakeElement();
        job2.SetTextContent("Job 2");
        job2.SetAttribute("data-job-id", "2");

        // Note: In real scenario, you'd use QuerySelectorAllAsync
        // This demonstrates the pattern
        ((FakePage)page).RegisterElement(".job-card[data-job-id='1']", job1);
        ((FakePage)page).RegisterElement(".job-card[data-job-id='2']", job2);

        // Act
        IElement? element1 = await page.QuerySelectorAsync(".job-card[data-job-id='1']");
        IElement? element2 = await page.QuerySelectorAsync(".job-card[data-job-id='2']");

        string? title1 = await element1!.GetTextContentAsync();
        string? title2 = await element2!.GetTextContentAsync();

        // Assert
        title1.Should().Be("Job 1");
        title2.Should().Be("Job 2");

        await kernel.DisposeAsync();
    }

    [Fact]
    public async Task ShouldHandleMissingElements_UsingHermeticPage()
    {
        // Arrange
        var kernel = new StubGhostKernel();
        IBrowserSession session = await kernel.NewSessionAsync();
        IPage page = await session.NewPageAsync();

        // Don't register any elements - simulate missing element

        // Act
        IElement? element = await page.QuerySelectorAsync(".non-existent");

        // Assert - Should return default element, not null
        element.Should().NotBeNull();
        string? text = await element!.GetTextContentAsync();
        text.Should().BeEmpty();

        await kernel.DisposeAsync();
    }

    [Fact]
    public async Task ShouldExtractSalaryRange_UsingHermeticPage()
    {
        // Arrange
        var kernel = new StubGhostKernel();
        IBrowserSession session = await kernel.NewSessionAsync();
        IPage page = await session.NewPageAsync();

        var salaryElement = new FakeElement();
        salaryElement.SetTextContent("$120k - $180k");
        salaryElement.SetAttribute("data-min", "120000");
        salaryElement.SetAttribute("data-max", "180000");
        ((FakePage)page).RegisterElement(".salary-range", salaryElement);

        // Act
        IElement? element = await page.QuerySelectorAsync(".salary-range");
        string? text = await element!.GetTextContentAsync();
        string? min = await element.GetAttributeAsync("data-min");
        string? max = await element.GetAttributeAsync("data-max");

        // Assert
        text.Should().Be("$120k - $180k");
        min.Should().Be("120000");
        max.Should().Be("180000");

        await kernel.DisposeAsync();
    }

    [Fact]
    public async Task ShouldExtractLocationInfo_UsingHermeticPage()
    {
        // Arrange
        var kernel = new StubGhostKernel();
        IBrowserSession session = await kernel.NewSessionAsync();
        IPage page = await session.NewPageAsync();

        var locationElement = new FakeElement();
        locationElement.SetTextContent("San Francisco, CA");
        locationElement.SetAttribute("data-remote", "true");
        ((FakePage)page).RegisterElement(".location", locationElement);

        // Act
        IElement? element = await page.QuerySelectorAsync(".location");
        string? location = await element!.GetTextContentAsync();
        string? isRemote = await element.GetAttributeAsync("data-remote");

        // Assert
        location.Should().Be("San Francisco, CA");
        isRemote.Should().Be("true");

        await kernel.DisposeAsync();
    }
}
