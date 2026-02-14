using Ghost.Contracts.Jobs;
using Ghost.Sdk.Spider.Core.Entities;
using Ghost.Sdk.Spider.Core.Entities.Attributes;

namespace Ghost.Plugin.LinkedIn.Entities;

/// <summary>
/// Entity representing a LinkedIn job posting extracted from job detail pages.
/// Uses Ghost.Sdk.Spider framework for attribute-based extraction.
/// </summary>
/// <remarks>
/// This entity extracts data from LinkedIn job detail pages using multiple selector strategies.
/// Selectors are ordered by priority (most reliable first) based on LinkedIn's DOM structure.
/// See /docs/migration/linkedin-analysis.md for detailed selector documentation.
/// </remarks>
[EntitySelector(
    Expression = "//body",
    Type = SelectorType.XPath,
    TakeFirst = true,
    Required = true)]
public class LinkedInJobEntity : EntityBase<LinkedInJobEntity>
{
    /// <summary>
    /// Gets or sets the unique job ID extracted from data attributes or URL.
    /// </summary>
    /// <remarks>
    /// Extraction priority:
    /// 1. data-entity-urn attribute (pattern: urn:li:jobPosting:{id})
    /// 2. URL path pattern (/jobs/view/{id})
    /// 3. Query parameter (jobId={id})
    /// </remarks>
    [ValueSelector(
        "[data-entity-urn]",
        SelectorType.Css,
        Attribute = "data-entity-urn")]
    [RegexFormatter(@"urn:li:jobPosting:(?<id>[0-9]+)", Group = 1)]
    [TrimFormatter]
    public string? JobId { get; set; }

    /// <summary>
    /// Gets or sets the job title.
    /// </summary>
    /// <remarks>
    /// Selector priority:
    /// 1. .top-card-layout__title
    /// 2. .job-details-jobs-unified-top-card__job-title
    /// 3. h1 (fallback)
    /// </remarks>
    [ValueSelector(
        ".top-card-layout__title",
        SelectorType.Css)]
    [TrimFormatter]
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets the company name.
    /// </summary>
    /// <remarks>
    /// Selector priority:
    /// 1. .top-card-layout__first-subline .topcard__org-name-link
    /// 2. .job-details-jobs-unified-top-card__company-name
    /// 3. .topcard__org-name-link
    /// </remarks>
    [ValueSelector(
        ".top-card-layout__first-subline .topcard__org-name-link, .job-details-jobs-unified-top-card__company-name, .topcard__org-name-link",
        SelectorType.Css)]
    [TrimFormatter]
    public string? Company { get; set; }

    /// <summary>
    /// Gets or sets the job location.
    /// </summary>
    /// <remarks>
    /// Selector priority:
    /// 1. .top-card-layout__first-subline .topcard__flavor--bullet
    /// 2. .job-details-jobs-unified-top-card__bullet
    /// 3. .topcard__flavor--bullet
    /// 4. .job-search-card__location
    /// </remarks>
    [ValueSelector(
        ".top-card-layout__first-subline .topcard__flavor--bullet, .job-details-jobs-unified-top-card__bullet, .topcard__flavor--bullet, .job-search-card__location",
        SelectorType.Css)]
    [TrimFormatter]
    public string? Location { get; set; }

    /// <summary>
    /// Gets or sets the job description (full HTML content).
    /// </summary>
    /// <remarks>
    /// Selector priority:
    /// 1. .show-more-less-html__markup
    /// 2. #job-details
    /// 3. .description__text
    /// 4. .job-description
    /// </remarks>
    [ValueSelector(
        ".show-more-less-html__markup, #job-details, .description__text, .job-description",
        SelectorType.Css,
        InnerHtml = true)]
    [TrimFormatter]
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the salary information.
    /// </summary>
    /// <remarks>
    /// Selector priority:
    /// 1. .main-job-card__salary-info
    /// 2. .job-details-jobs-unified-top-card__salary
    /// 3. .job-details-jobs-unified-top-card__salary-info
    /// 4. .salary-range
    /// Note: Salary information is often missing from LinkedIn job postings.
    /// </remarks>
    [ValueSelector(
        ".main-job-card__salary-info, .job-details-jobs-unified-top-card__salary, .job-details-jobs-unified-top-card__salary-info, .salary-range",
        SelectorType.Css)]
    [TrimFormatter]
    public string? Salary { get; set; }

    /// <summary>
    /// Gets or sets the date when the job was posted.
    /// </summary>
    /// <remarks>
    /// Selector priority:
    /// 1. time[datetime] attribute (ISO 8601 format)
    /// 2. .posted-time-ago__text (relative format like "3 days ago")
    /// Note: Relative dates need custom parsing in formatter.
    /// </remarks>
    [ValueSelector(
        "time[datetime]",
        SelectorType.Css,
        Attribute = "datetime")]
    [DateTimeFormatter]
    public DateTimeOffset? PostedAt { get; set; }

    /// <summary>
    /// Gets or sets the job type (FullTime, PartTime, Contract, Internship).
    /// </summary>
    /// <remarks>
    /// Extracted from job criteria list items.
    /// Selector: .description__job-criteria-list .description__job-criteria-item
    /// Note: Requires custom parsing logic to map text to JobType enum.
    /// </remarks>
    [ValueSelector(
        ".description__job-criteria-list .description__job-criteria-item",
        SelectorType.Css)]
    [TrimFormatter]
    public string? JobTypeRaw { get; set; }

    /// <summary>
    /// Gets or sets the experience level required (EntryLevel, MidLevel, Senior, Manager).
    /// </summary>
    /// <remarks>
    /// Extracted from job criteria list items.
    /// Selector: .description__job-criteria-list .description__job-criteria-item
    /// Note: Requires custom parsing logic to map text to ExperienceLevel enum.
    /// </remarks>
    [ValueSelector(
        ".description__job-criteria-list .description__job-criteria-item",
        SelectorType.Css)]
    [TrimFormatter]
    public string? ExperienceLevelRaw { get; set; }

    /// <summary>
    /// Gets or sets the job URL.
    /// </summary>
    /// <remarks>
    /// Typically constructed from: {baseUrl}/jobs/view/{jobId}
    /// Can also be extracted from canonical link or og:url meta tag.
    /// </remarks>
    [ValueSelector(
        "//link[@rel='canonical']",
        SelectorType.XPath,
        Attribute = "href")]
    [TrimFormatter]
    public string? Url { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this is an Easy Apply job.
    /// </summary>
    /// <remarks>
    /// Detected by presence of Easy Apply button.
    /// Selector: .jobs-apply-button--top-card button, .jobs-s-apply button
    /// Note: Requires checking button text content for "Easy Apply".
    /// </remarks>
    [ValueSelector(
        ".jobs-apply-button--top-card button, .jobs-s-apply button",
        SelectorType.Css)]
    [TrimFormatter]
    public string? EasyApplyButton { get; set; }

    /// <summary>
    /// Gets a value indicating whether this job supports Easy Apply based on button detection.
    /// </summary>
    public bool IsEasyApply => !string.IsNullOrWhiteSpace(EasyApplyButton) &&
                               EasyApplyButton.Contains("Easy Apply", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets the JSON-LD structured data for advanced parsing.
    /// </summary>
    /// <remarks>
    /// LinkedIn embeds structured data in &lt;script type="application/ld+json"&gt; tags.
    /// This can be parsed for more reliable extraction of company, location, salary, etc.
    /// Schema type: JobPosting (https://schema.org/JobPosting)
    /// </remarks>
    [ValueSelector(
        "//script[@type='application/ld+json']",
        SelectorType.XPath)]
    [TrimFormatter]
    public string? JsonLdData { get; set; }

    /// <summary>
    /// Validates the extracted entity data.
    /// </summary>
    /// <returns>True if the entity has minimum required fields; otherwise, false.</returns>
    public override bool Validate()
    {
        // At minimum, we need either a JobId or Url, and a Title
        var hasIdentifier = !string.IsNullOrWhiteSpace(JobId) || !string.IsNullOrWhiteSpace(Url);
        var hasTitle = !string.IsNullOrWhiteSpace(Title);

        return hasIdentifier && hasTitle;
    }

    /// <summary>
    /// Parses the JobTypeRaw string to a JobType enum value.
    /// </summary>
    /// <returns>The parsed JobType or Unknown if parsing fails.</returns>
    public JobType ParseJobType()
    {
        if (string.IsNullOrWhiteSpace(JobTypeRaw))
            return JobType.Unknown;

        var normalized = JobTypeRaw.Trim().Replace(" ", "").Replace("-", "");

        return normalized.ToLowerInvariant() switch
        {
            var s when s.Contains("fulltime") || s.Contains("full") => JobType.FullTime,
            var s when s.Contains("parttime") || s.Contains("part") => JobType.PartTime,
            var s when s.Contains("contract") => JobType.Contract,
            var s when s.Contains("internship") || s.Contains("intern") => JobType.Internship,
            _ => JobType.Unknown
        };
    }

    /// <summary>
    /// Parses the ExperienceLevelRaw string to an ExperienceLevel enum value.
    /// </summary>
    /// <returns>The parsed ExperienceLevel or Unknown if parsing fails.</returns>
    public ExperienceLevel ParseExperienceLevel()
    {
        if (string.IsNullOrWhiteSpace(ExperienceLevelRaw))
            return ExperienceLevel.Unknown;

        var normalized = ExperienceLevelRaw.Trim().Replace(" ", "").Replace("-", "");

        return normalized.ToLowerInvariant() switch
        {
            var s when s.Contains("entry") || s.Contains("junior") || s.Contains("associate") => ExperienceLevel.EntryLevel,
            var s when s.Contains("mid") || s.Contains("intermediate") => ExperienceLevel.MidLevel,
            var s when s.Contains("senior") || s.Contains("sr.") => ExperienceLevel.Senior,
            var s when s.Contains("manager") || s.Contains("lead") || s.Contains("director") => ExperienceLevel.Manager,
            _ => ExperienceLevel.Unknown
        };
    }

    /// <summary>
    /// Extracts the Job ID from the URL if JobId property is not set.
    /// </summary>
    /// <returns>The extracted job ID or null if extraction fails.</returns>
    public string? ExtractJobIdFromUrl()
    {
        if (!string.IsNullOrWhiteSpace(JobId))
            return JobId;

        if (string.IsNullOrWhiteSpace(Url))
            return null;

        // Pattern 1: /jobs/view/{id}
        var viewMatch = System.Text.RegularExpressions.Regex.Match(
            Url,
            @"/jobs/(?:view|r)/(?<id>[0-9]+)");
        if (viewMatch.Success)
            return viewMatch.Groups["id"].Value;

        // Pattern 2: ?jobId={id}
        var queryMatch = System.Text.RegularExpressions.Regex.Match(
            Url,
            @"[?&](?:jobId|id)=(?<id>[0-9]+)");
        if (queryMatch.Success)
            return queryMatch.Groups["id"].Value;

        // Pattern 3: URL ending with -{id}
        var endingMatch = System.Text.RegularExpressions.Regex.Match(
            Url,
            @"-(\d{6,})(?:\?|$)");
        if (endingMatch.Success)
            return endingMatch.Groups[1].Value;

        return null;
    }
}
