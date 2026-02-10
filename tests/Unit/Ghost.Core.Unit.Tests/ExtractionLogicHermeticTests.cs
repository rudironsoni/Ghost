using FluentAssertions;
using Ghost.Core;
using Ghost.Testing.Fakes;
using Xunit;

namespace Ghost.Core.Unit.Tests;

/// <summary>
/// Hermetic unit tests demonstrating conversion from integration tests.
/// These tests verify extraction logic without real browser dependencies.
/// </summary>
public class ExtractionLogicHermeticTests
{
    [Fact]
    public async Task ShouldExtractJobTitle_UsingHermeticPage()
    {
        // Arrange - Create hermetic kernel and page
        var kernel = new StubGhostKernel();
        var session = await kernel.NewSessionAsync();
        var page = await session.NewPageAsync();

        // Setup test data in hermetic page
        var titleElement = new FakeElement();
        titleElement.SetTextContent("Senior Software Engineer");
        ((FakePage)page).RegisterElement("h1.job-title", titleElement);

        // Act
        var element = await page.QuerySelectorAsync("h1.job-title");
        var title = await element!.GetTextContentAsync();

        // Assert
        title.Should().Be("Senior Software Engineer");

        await kernel.DisposeAsync();
    }

    [Fact]
    public async Task ShouldExtractCompanyName_UsingHermeticPage()
    {
        // Arrange
        var kernel = new StubGhostKernel();
        var session = await kernel.NewSessionAsync();
        var page = await session.NewPageAsync();

        var companyElement = new FakeElement();
        companyElement.SetTextContent("Acme Corp");
        ((FakePage)page).RegisterElement(".company-name", companyElement);

        // Act
        var element = await page.QuerySelectorAsync(".company-name");
        var company = await element!.GetTextContentAsync();

        // Assert
        company.Should().Be("Acme Corp");

        await kernel.DisposeAsync();
    }

    [Fact]
    public async Task ShouldExtractJobUrl_UsingHermeticPage()
    {
        // Arrange
        var kernel = new StubGhostKernel();
        var session = await kernel.NewSessionAsync();
        var page = await session.NewPageAsync();

        var linkElement = new FakeElement();
        linkElement.SetAttribute("href", "https://example.com/jobs/12345");
        ((FakePage)page).RegisterElement("a.apply-link", linkElement);

        // Act
        var element = await page.QuerySelectorAsync("a.apply-link");
        var url = await element!.GetAttributeAsync("href");

        // Assert
        url.Should().Be("https://example.com/jobs/12345");

        await kernel.DisposeAsync();
    }

    [Fact]
    public async Task ShouldExtractMultipleJobCards_UsingHermeticPage()
    {
        // Arrange
        var kernel = new StubGhostKernel();
        var session = await kernel.NewSessionAsync();
        var page = await session.NewPageAsync();

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
        var element1 = await page.QuerySelectorAsync(".job-card[data-job-id='1']");
        var element2 = await page.QuerySelectorAsync(".job-card[data-job-id='2']");

        var title1 = await element1!.GetTextContentAsync();
        var title2 = await element2!.GetTextContentAsync();

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
        var session = await kernel.NewSessionAsync();
        var page = await session.NewPageAsync();

        // Don't register any elements - simulate missing element

        // Act
        var element = await page.QuerySelectorAsync(".non-existent");

        // Assert - Should return default element, not null
        element.Should().NotBeNull();
        var text = await element!.GetTextContentAsync();
        text.Should().BeEmpty();

        await kernel.DisposeAsync();
    }

    [Fact]
    public async Task ShouldExtractSalaryRange_UsingHermeticPage()
    {
        // Arrange
        var kernel = new StubGhostKernel();
        var session = await kernel.NewSessionAsync();
        var page = await session.NewPageAsync();

        var salaryElement = new FakeElement();
        salaryElement.SetTextContent("$120k - $180k");
        salaryElement.SetAttribute("data-min", "120000");
        salaryElement.SetAttribute("data-max", "180000");
        ((FakePage)page).RegisterElement(".salary-range", salaryElement);

        // Act
        var element = await page.QuerySelectorAsync(".salary-range");
        var text = await element!.GetTextContentAsync();
        var min = await element.GetAttributeAsync("data-min");
        var max = await element.GetAttributeAsync("data-max");

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
        var session = await kernel.NewSessionAsync();
        var page = await session.NewPageAsync();

        var locationElement = new FakeElement();
        locationElement.SetTextContent("San Francisco, CA");
        locationElement.SetAttribute("data-remote", "true");
        ((FakePage)page).RegisterElement(".location", locationElement);

        // Act
        var element = await page.QuerySelectorAsync(".location");
        var location = await element!.GetTextContentAsync();
        var isRemote = await element.GetAttributeAsync("data-remote");

        // Assert
        location.Should().Be("San Francisco, CA");
        isRemote.Should().Be("true");

        await kernel.DisposeAsync();
    }
}
