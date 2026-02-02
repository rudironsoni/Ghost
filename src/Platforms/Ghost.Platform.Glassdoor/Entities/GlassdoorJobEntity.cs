using DotnetSpider.DataFlow.Parser;
using DotnetSpider.DataFlow.Parser.Formatters;
using DotnetSpider.DataFlow.Storage.Entity;
using DotnetSpider.Selector;

namespace Ghost.Platform.Glassdoor.Entities;

[Schema("glassdoor", "jobs")]
[EntitySelector(
    Expression = "//li[contains(@class,'react-job-listing') or contains(@class,'jobListing') or contains(@class,'JobsList_jobListItem')]",
    Type = SelectorType.XPath)]
public class GlassdoorJobEntity : EntityBase<GlassdoorJobEntity>
{
    [ValueSelector(Expression = "./@data-id | ./@data-jobid", Type = SelectorType.XPath)]
    [TrimFormatter]
    public string? JobId { get; set; }

    [ValueSelector(
        Expression = ".//a[contains(@class,'jobLink') or contains(@class,'jobTitle') or contains(@class,'job-title') or contains(@class,'jobtitle')]",
        Type = SelectorType.XPath)]
    [TrimFormatter]
    [ReplaceFormatter(OldValue = "\n", NewValue = " ")]
    public string? Title { get; set; }

    [ValueSelector(
        Expression = ".//*[contains(@class,'EmployerProfile_compactEmployerName') or contains(@class,'employer-name') or contains(@class,'companyName') or contains(@class,'company')]",
        Type = SelectorType.XPath)]
    [TrimFormatter]
    public string? Company { get; set; }

    [ValueSelector(
        Expression = ".//*[contains(@class,'JobCard_location') or contains(@class,'location') or contains(@class,'loc')]",
        Type = SelectorType.XPath)]
    [TrimFormatter]
    [ReplaceFormatter(OldValue = "\n", NewValue = " ")]
    public string? Location { get; set; }

    [ValueSelector(
        Expression = ".//*[contains(@class,'salary') or contains(@class,'pay') or contains(@class,'Salary')]",
        Type = SelectorType.XPath)]
    [TrimFormatter]
    public string? Salary { get; set; }

    [ValueSelector(
        Expression = ".//*[contains(@class,'jobDescription') or contains(@class,'jobDesc') or contains(@class,'summary') or contains(@class,'snippet')]",
        Type = SelectorType.XPath)]
    [TrimFormatter]
    [ReplaceFormatter(OldValue = "\n", NewValue = " ")]
    public string? Description { get; set; }

    [ValueSelector(
        Expression = ".//a[contains(@class,'jobLink') or contains(@class,'jobTitle') or contains(@class,'job-title')]/@href",
        Type = SelectorType.XPath)]
    [TrimFormatter]
    [StringFormatter(FormatStr = "https://www.glassdoor.com{0}")]
    public string? JobUrl { get; set; }

    [ValueSelector(
        Expression = ".//*[contains(@class,'date') or contains(@class,'posted') or contains(@class,'job-age') or contains(@class,'age')]",
        Type = SelectorType.XPath)]
    [TrimFormatter]
    public string? PostedAt { get; set; }

    [ValueSelector(Expression = ".//span[contains(@class,'remote') or contains(@class,'Remote')]", Type = SelectorType.XPath)]
    [TrimFormatter]
    public string? RemoteLabel { get; set; }

    [ValueSelector(
        Expression = ".//*[contains(@class,'jobType') or contains(@class,'job-type') or contains(@class,'employmentStatus') or contains(@class,'full-time') or contains(@class,'part-time')]",
        Type = SelectorType.XPath)]
    [TrimFormatter]
    [ReplaceFormatter(OldValue = "\n", NewValue = " ")]
    public string? JobType { get; set; }

    [ValueSelector(
        Expression = ".//*[contains(@class,'rating') or contains(@class,'Rating')]",
        Type = SelectorType.XPath)]
    [TrimFormatter]
    public string? CompanyRating { get; set; }
}
