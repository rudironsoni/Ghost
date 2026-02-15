using Ghost.Sdk.Spider.Core.Entities;
using Ghost.Sdk.Spider.Core.Entities.Attributes;
using Ghost.Sdk.Spider.Core.Entities.Formatters;

namespace Ghost.Plugin.Google.Jobs.Entities;

[EntitySelector(
    Expression = "//div[@role='listitem' or contains(@class,'gws-plugins-horizon-jobs__li-ed') or contains(@class,'gws-plugins-horizon-jobs__li')]",
    Type = SelectorType.XPath)]
public class GoogleJobsEntity : EntityBase<GoogleJobsEntity>
{
    [ValueSelector("./@data-id | ./@data-job-id | ./@data-entityid | ./@data-ved", SelectorType.XPath)]
    [TrimFormatter]
    public string? JobId { get; set; }

    [ValueSelector(".//h3 | .//*[@role='heading'] | .//*[contains(@class,'BjJfJf')] | .//*[@jsname='Cpkphb']", SelectorType.XPath)]
    [TrimFormatter]
    [ReplaceFormatter("\n", " ")]
    public string? Title { get; set; }

    [ValueSelector(".//*[contains(@class,'vNEEBe') or @jsname='V7iZ7c' or contains(@class,'company') or contains(@class,'Employer')]", SelectorType.XPath)]
    [TrimFormatter]
    [ReplaceFormatter("\n", " ")]
    public string? Company { get; set; }

    [ValueSelector(".//*[contains(@class,'Qk3sIe') or @jsname='s2gQvd' or contains(@class,'location') or contains(@class,'Location')]", SelectorType.XPath)]
    [TrimFormatter]
    [ReplaceFormatter("\n", " ")]
    public string? Location { get; set; }

    [ValueSelector(".//*[contains(@class,'salary') or contains(@class,'Salary') or contains(@class,'pay') or contains(@class,'compensation')]", SelectorType.XPath)]
    [TrimFormatter]
    public string? Salary { get; set; }

    [ValueSelector(".//*[contains(@class,'HBvzbc') or @jsname='o7OJ4' or contains(@class,'YgLbBe') or contains(@class,'jobDescription') or contains(@class,'jobDesc') or contains(@class,'snippet')]", SelectorType.XPath)]
    [TrimFormatter]
    [ReplaceFormatter("\n", " ")]
    public string? Description { get; set; }

    [ValueSelector(".//a[contains(@href,'/search') and (contains(@href,'ibp=htl') or contains(@href,'udm=8'))]/@href | .//a[contains(@href,'jobs')]/@href", SelectorType.XPath)]
    [TrimFormatter]
    [StringFormatter("https://www.google.com{0}")]
    public string? JobUrl { get; set; }

    [ValueSelector(".//*[contains(@class,'date') or contains(@class,'posted') or contains(@class,'gws-plugins-horizon-jobs__posted-date')]", SelectorType.XPath)]
    [TrimFormatter]
    public string? PostedAt { get; set; }

    [ValueSelector(".//span[contains(@class,'remote') or contains(@class,'Remote')]", SelectorType.XPath)]
    [TrimFormatter]
    public string? RemoteLabel { get; set; }

    [ValueSelector(".//*[contains(@class,'jobType') or contains(@class,'job-type') or contains(@class,'employment') or contains(@class,'full-time') or contains(@class,'part-time') or contains(@class,'contract')]", SelectorType.XPath)]
    [TrimFormatter]
    [ReplaceFormatter("\n", " ")]
    public string? JobType { get; set; }
}
