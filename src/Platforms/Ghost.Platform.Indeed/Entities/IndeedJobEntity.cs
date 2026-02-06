using Ghost.Sdk.Spider.Core.Entities;
using Ghost.Sdk.Spider.Core.Entities.Attributes;
using Ghost.Sdk.Spider.Core.Entities.Formatters;

namespace Ghost.Platform.Indeed.Entities;

[EntitySelector(Expression = "//div[contains(@class,'job_seen_beacon') or contains(@class,'job_')]", Type = SelectorType.XPath)]
public class IndeedJobEntity : EntityBase<IndeedJobEntity>
{
    [ValueSelector("./@data-jk", SelectorType.XPath)]
    [TrimFormatter]
    public string? JobKey { get; set; }

    [ValueSelector(".//h2[contains(@class,'jobTitle')]//span", SelectorType.XPath)]
    [TrimFormatter]
    [ReplaceFormatter("\n", " ")]
    public string? Title { get; set; }

    [ValueSelector(".//span[contains(@class,'companyName')]", SelectorType.XPath)]
    [TrimFormatter]
    public string? Company { get; set; }

    [ValueSelector(".//div[contains(@class,'companyLocation')]", SelectorType.XPath)]
    [TrimFormatter]
    [ReplaceFormatter("\n", " ")]
    public string? Location { get; set; }

    [ValueSelector(".//*[contains(@class,'salary-snippet')]", SelectorType.XPath)]
    [TrimFormatter]
    public string? Salary { get; set; }

    [ValueSelector(".//div[contains(@class,'job-snippet')]", SelectorType.XPath)]
    [TrimFormatter]
    [ReplaceFormatter("\n", " ")]
    public string? Description { get; set; }

    [ValueSelector(".//a[contains(@class,'jcs-JobTitle')]/@href", SelectorType.XPath)]
    [TrimFormatter]
    [StringFormatter("https://www.indeed.com{0}")]
    public string? JobUrl { get; set; }

    [ValueSelector(".//span[contains(@class,'date') or contains(@class,'datePosted')]", SelectorType.XPath)]
    [TrimFormatter]
    public string? PostedAt { get; set; }

    [ValueSelector(".//span[contains(@class,'remote') or contains(@class,'Remote')]", SelectorType.XPath)]
    [TrimFormatter]
    public string? RemoteLabel { get; set; }

    [ValueSelector(".//div[contains(@class,'metadata') or contains(@class,'jobMetaDataGroup')]", SelectorType.XPath)]
    [TrimFormatter]
    [ReplaceFormatter("\n", " ")]
    public string? JobType { get; set; }
}
