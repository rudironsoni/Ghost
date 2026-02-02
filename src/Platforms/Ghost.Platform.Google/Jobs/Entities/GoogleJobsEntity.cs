using DotnetSpider.DataFlow.Parser;
using DotnetSpider.DataFlow.Parser.Formatters;
using DotnetSpider.DataFlow.Storage.Entity;
using DotnetSpider.Selector;

namespace Ghost.Platform.Google.Jobs.Entities;

[Schema("google", "jobs")]
[EntitySelector(
    Expression = "//div[@role='listitem' or contains(@class,'gws-plugins-horizon-jobs__li-ed') or contains(@class,'gws-plugins-horizon-jobs__li')]",
    Type = SelectorType.XPath)]
public class GoogleJobsEntity : EntityBase<GoogleJobsEntity>
{
    [ValueSelector(Expression = "./@data-id | ./@data-job-id | ./@data-entityid | ./@data-ved", Type = SelectorType.XPath)]
    [TrimFormatter]
    public string? JobId { get; set; }

    [ValueSelector(
        Expression = ".//h3 | .//*[@role='heading'] | .//*[contains(@class,'BjJfJf')] | .//*[@jsname='Cpkphb']",
        Type = SelectorType.XPath)]
    [TrimFormatter]
    [ReplaceFormatter(OldValue = "\n", NewValue = " ")]
    public string? Title { get; set; }

    [ValueSelector(
        Expression = ".//*[contains(@class,'vNEEBe') or @jsname='V7iZ7c' or contains(@class,'company') or contains(@class,'Employer')]",
        Type = SelectorType.XPath)]
    [TrimFormatter]
    [ReplaceFormatter(OldValue = "\n", NewValue = " ")]
    public string? Company { get; set; }

    [ValueSelector(
        Expression = ".//*[contains(@class,'Qk3sIe') or @jsname='s2gQvd' or contains(@class,'location') or contains(@class,'Location')]",
        Type = SelectorType.XPath)]
    [TrimFormatter]
    [ReplaceFormatter(OldValue = "\n", NewValue = " ")]
    public string? Location { get; set; }

    [ValueSelector(
        Expression = ".//*[contains(@class,'salary') or contains(@class,'Salary') or contains(@class,'pay') or contains(@class,'compensation')]",
        Type = SelectorType.XPath)]
    [TrimFormatter]
    public string? Salary { get; set; }

    [ValueSelector(
        Expression = ".//*[contains(@class,'HBvzbc') or @jsname='o7OJ4' or contains(@class,'YgLbBe') or contains(@class,'jobDescription') or contains(@class,'jobDesc') or contains(@class,'snippet')]",
        Type = SelectorType.XPath)]
    [TrimFormatter]
    [ReplaceFormatter(OldValue = "\n", NewValue = " ")]
    public string? Description { get; set; }

    [ValueSelector(
        Expression = ".//a[contains(@href,'/search') and (contains(@href,'ibp=htl') or contains(@href,'udm=8'))]/@href | .//a[contains(@href,'jobs')]/@href",
        Type = SelectorType.XPath)]
    [TrimFormatter]
    [StringFormatter(FormatStr = "https://www.google.com{0}")]
    public string? JobUrl { get; set; }

    [ValueSelector(
        Expression = ".//*[contains(@class,'date') or contains(@class,'posted') or contains(@class,'gws-plugins-horizon-jobs__posted-date')]",
        Type = SelectorType.XPath)]
    [TrimFormatter]
    public string? PostedAt { get; set; }

    [ValueSelector(Expression = ".//span[contains(@class,'remote') or contains(@class,'Remote')]", Type = SelectorType.XPath)]
    [TrimFormatter]
    public string? RemoteLabel { get; set; }

    [ValueSelector(
        Expression = ".//*[contains(@class,'jobType') or contains(@class,'job-type') or contains(@class,'employment') or contains(@class,'full-time') or contains(@class,'part-time') or contains(@class,'contract')]",
        Type = SelectorType.XPath)]
    [TrimFormatter]
    [ReplaceFormatter(OldValue = "\n", NewValue = " ")]
    public string? JobType { get; set; }
}
