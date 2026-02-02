using DotnetSpider.DataFlow.Parser;
using DotnetSpider.DataFlow.Parser.Formatters;
using DotnetSpider.DataFlow.Storage.Entity;
using DotnetSpider.Selector;

namespace Ghost.Platform.Indeed.Entities;

[Schema("indeed", "jobs")]
[EntitySelector(Expression = "//div[contains(@class,'job_seen_beacon') or contains(@class,'job_')]", Type = SelectorType.XPath)]
public class IndeedJobEntity : EntityBase<IndeedJobEntity>
{
    [ValueSelector(Expression = "./@data-jk", Type = SelectorType.XPath)]
    [TrimFormatter]
    public string? JobKey { get; set; }

    [ValueSelector(Expression = ".//h2[contains(@class,'jobTitle')]//span", Type = SelectorType.XPath)]
    [TrimFormatter]
    [ReplaceFormatter(OldValue = "\n", NewValue = " ")]
    public string? Title { get; set; }

    [ValueSelector(Expression = ".//span[contains(@class,'companyName')]", Type = SelectorType.XPath)]
    [TrimFormatter]
    public string? Company { get; set; }

    [ValueSelector(Expression = ".//div[contains(@class,'companyLocation')]", Type = SelectorType.XPath)]
    [TrimFormatter]
    [ReplaceFormatter(OldValue = "\n", NewValue = " ")]
    public string? Location { get; set; }

    [ValueSelector(Expression = ".//*[contains(@class,'salary-snippet')]", Type = SelectorType.XPath)]
    [TrimFormatter]
    public string? Salary { get; set; }

    [ValueSelector(Expression = ".//div[contains(@class,'job-snippet')]", Type = SelectorType.XPath)]
    [TrimFormatter]
    [ReplaceFormatter(OldValue = "\n", NewValue = " ")]
    public string? Description { get; set; }

    [ValueSelector(Expression = ".//a[contains(@class,'jcs-JobTitle')]/@href", Type = SelectorType.XPath)]
    [TrimFormatter]
    [StringFormatter(FormatStr = "https://www.indeed.com{0}")]
    public string? JobUrl { get; set; }

    [ValueSelector(Expression = ".//span[contains(@class,'date') or contains(@class,'datePosted')]", Type = SelectorType.XPath)]
    [TrimFormatter]
    public string? PostedAt { get; set; }

    [ValueSelector(Expression = ".//span[contains(@class,'remote') or contains(@class,'Remote')]", Type = SelectorType.XPath)]
    [TrimFormatter]
    public string? RemoteLabel { get; set; }

    [ValueSelector(Expression = ".//div[contains(@class,'metadata') or contains(@class,'jobMetaDataGroup')]", Type = SelectorType.XPath)]
    [TrimFormatter]
    [ReplaceFormatter(OldValue = "\n", NewValue = " ")]
    public string? JobType { get; set; }
}
