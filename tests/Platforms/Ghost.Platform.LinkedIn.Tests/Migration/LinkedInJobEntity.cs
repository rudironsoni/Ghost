using Ghost.Sdk.Spider.Core.Entities;
using Ghost.Sdk.Spider.Core.Entities.Attributes;
using Ghost.Sdk.Spider.Core.Extraction.Selectors;

namespace Ghost.Platform.LinkedIn.Tests.Migration;

/// <summary>
/// Entity for extracting LinkedIn job postings using Ghost.Sdk.Spider.
/// This demonstrates the migration from the old platform-specific approach to the Spider SDK.
/// </summary>
[EntitySelector(Expression = "//html", Type = SelectorType.XPath)]
public class LinkedInJobEntity : EntityBase<LinkedInJobEntity>
{
    /// <summary>
    /// Job title extracted from the main heading
    /// </summary>
    [ValueSelector("//h2[contains(@class, 'top-card-layout__title')]", SelectorType.XPath, TakeFirst = true)]
    [TrimFormatterAttribute(Order = 1)]
    [Field(Required = true)]
    public string? Title { get; set; }

    /// <summary>
    /// Company name from the organization link
    /// </summary>
    [ValueSelector("//a[contains(@class, 'topcard__org-name-link')]", SelectorType.XPath, TakeFirst = true)]
    [TrimFormatterAttribute(Order = 1)]
    [Field(Required = true)]
    public string? Company { get; set; }

    /// <summary>
    /// Job location
    /// </summary>
    [ValueSelector("//span[contains(@class, 'topcard__flavor--bullet')]", SelectorType.XPath, TakeFirst = true)]
    [TrimFormatterAttribute(Order = 1)]
    public string? Location { get; set; }

    /// <summary>
    /// Time since posted (e.g., "1 week ago")
    /// </summary>
    [ValueSelector("//span[contains(@class, 'posted-time-ago__text')]", SelectorType.XPath, TakeFirst = true)]
    [TrimFormatterAttribute(Order = 1)]
    public string? PostedTime { get; set; }

    /// <summary>
    /// Job description HTML content
    /// </summary>
    [ValueSelector("//div[contains(@class, 'description__text')]", SelectorType.XPath, TakeFirst = true)]
    [TrimFormatterAttribute(Order = 1)]
    public string? Description { get; set; }

    /// <summary>
    /// Employment type (Full-time, Part-time, etc.)
    /// </summary>
    [ValueSelector("//span[contains(text(), 'Employment type')]/following-sibling::span", SelectorType.XPath, TakeFirst = true)]
    [TrimFormatterAttribute(Order = 1)]
    public string? EmploymentType { get; set; }

    /// <summary>
    /// Seniority level
    /// </summary>
    [ValueSelector("//span[contains(text(), 'Seniority level')]/following-sibling::span", SelectorType.XPath, TakeFirst = true)]
    [TrimFormatterAttribute(Order = 1)]
    public string? SeniorityLevel { get; set; }

    /// <summary>
    /// Job function/category
    /// </summary>
    [ValueSelector("//span[contains(text(), 'Job function')]/following-sibling::span", SelectorType.XPath, TakeFirst = true)]
    [TrimFormatterAttribute(Order = 1)]
    public string? JobFunction { get; set; }

    /// <summary>
    /// Industries
    /// </summary>
    [ValueSelector("//span[contains(text(), 'Industries')]/following-sibling::span", SelectorType.XPath, TakeFirst = true)]
    [TrimFormatterAttribute(Order = 1)]
    public string? Industries { get; set; }

    /// <summary>
    /// Company logo URL
    /// </summary>
    [ValueSelector("//img[contains(@class, 'artdeco-entity-image')]/@data-delayed-url", SelectorType.XPath, TakeFirst = true)]
    [TrimFormatterAttribute(Order = 1)]
    public string? CompanyLogoUrl { get; set; }

    /// <summary>
    /// Job URL (extracted from link)
    /// </summary>
    [ValueSelector("//a[contains(@class, 'topcard__link')]/@href", SelectorType.XPath, TakeFirst = true)]
    [TrimFormatterAttribute(Order = 1)]
    public string? JobUrl { get; set; }

    /// <summary>
    /// Company URL
    /// </summary>
    [ValueSelector("//a[contains(@class, 'topcard__org-name-link')]/@href", SelectorType.XPath, TakeFirst = true)]
    [TrimFormatterAttribute(Order = 1)]
    public string? CompanyUrl { get; set; }

    /// <summary>
    /// Number of applicants (if available)
    /// </summary>
    [ValueSelector("//span[contains(@class, 'num-applicants__caption')]", SelectorType.XPath, TakeFirst = true)]
    [TrimFormatterAttribute(Order = 1)]
    [RegexFormatter(@"\d+", Order = 2)]
    public string? ApplicantCount { get; set; }

    /// <summary>
    /// Validates that required fields are present
    /// </summary>
    public override bool Validate()
    {
        return !string.IsNullOrWhiteSpace(Title) && 
               !string.IsNullOrWhiteSpace(Company);
    }
}
