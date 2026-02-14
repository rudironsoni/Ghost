using Ghost.Sdk.Spider.Core.Entities;
using Ghost.Sdk.Spider.Core.Entities.Attributes;

namespace Ghost.Plugin.Glassdoor.Entities;

[EntitySelector(
    Expression = "//li[contains(@class,'react-job-listing') or contains(@class,'jobListing') or contains(@class,'JobsList_jobListItem')]",
    Type = SelectorType.XPath)]
public class GlassdoorJobEntity : EntityBase<GlassdoorJobEntity>
{
    [ValueSelector("./@data-id | ./@data-jobid", SelectorType.XPath)]
    [TrimFormatter]
    public string? JobId { get; set; }

    [ValueSelector(
        ".//a[contains(@class,'jobLink') or contains(@class,'jobTitle') or contains(@class,'job-title') or contains(@class,'jobtitle')]",
        SelectorType.XPath)]
    [TrimFormatter]
    [ReplaceFormatter("\n", " ")]
    public string? Title { get; set; }

    [ValueSelector(
        ".//*[contains(@class,'EmployerProfile_compactEmployerName') or contains(@class,'employer-name') or contains(@class,'companyName') or contains(@class,'company')]",
        SelectorType.XPath)]
    [TrimFormatter]
    public string? Company { get; set; }

    [ValueSelector(
        ".//*[contains(@class,'JobCard_location') or contains(@class,'location') or contains(@class,'loc')]",
        SelectorType.XPath)]
    [TrimFormatter]
    [ReplaceFormatter("\n", " ")]
    public string? Location { get; set; }

    [ValueSelector(
        ".//*[contains(@class,'salary') or contains(@class,'pay') or contains(@class,'Salary')]",
        SelectorType.XPath)]
    [TrimFormatter]
    public string? Salary { get; set; }

    [ValueSelector(
        ".//*[contains(@class,'jobDescription') or contains(@class,'jobDesc') or contains(@class,'summary') or contains(@class,'snippet')]",
        SelectorType.XPath)]
    [TrimFormatter]
    [ReplaceFormatter("\n", " ")]
    public string? Description { get; set; }

    [ValueSelector(
        ".//a[contains(@class,'jobLink') or contains(@class,'jobTitle') or contains(@class,'job-title')]/@href",
        SelectorType.XPath)]
    [TrimFormatter]
    [StringFormatter("https://www.glassdoor.com{0}")]
    public string? JobUrl { get; set; }

    [ValueSelector(
        ".//*[contains(@class,'date') or contains(@class,'posted') or contains(@class,'job-age') or contains(@class,'age')]",
        SelectorType.XPath)]
    [TrimFormatter]
    public string? PostedAt { get; set; }

    [ValueSelector(".//span[contains(@class,'remote') or contains(@class,'Remote')]", SelectorType.XPath)]
    [TrimFormatter]
    public string? RemoteLabel { get; set; }

    [ValueSelector(
        ".//*[contains(@class,'jobType') or contains(@class,'job-type') or contains(@class,'employmentStatus') or contains(@class,'full-time') or contains(@class,'part-time')]",
        SelectorType.XPath)]
    [TrimFormatter]
    [ReplaceFormatter("\n", " ")]
    public string? JobType { get; set; }

    [ValueSelector(
        ".//*[contains(@class,'rating') or contains(@class,'Rating')]",
        SelectorType.XPath)]
    [TrimFormatter]
    public string? CompanyRating { get; set; }
}
