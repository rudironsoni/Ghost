using Ghost.Sdk.Spider.Core.Entities;
using Ghost.Sdk.Spider.Core.Entities.Attributes;
using Ghost.Sdk.Spider.Core.Extraction.Selectors;

namespace Ghost.Sdk.Spider.Tests.TestHelpers;

/// <summary>
/// Test entity for unit testing entity extraction and parsing
/// </summary>
[EntitySelector(Expression = ".product", Type = SelectorType.Css)]
public class TestProduct : EntityBase<TestProduct>
{
    [ValueSelector(".product-name", SelectorType.Css, TakeFirst = true)]
    [Field(Required = true)]
    public string? Title { get; set; }

    [ValueSelector(".price", SelectorType.Css, TakeFirst = true)]
    [TrimFormatterAttribute]
    public string? Price { get; set; }

    [ValueSelector(".description", SelectorType.Css, TakeFirst = true)]
    public string? Description { get; set; }

    [ValueSelector("@data-id", SelectorType.XPath, TakeFirst = true)]
    public int? ProductId { get; set; }
}

/// <summary>
/// Test entity without entity selector (single entity per page)
/// </summary>
public class TestArticle : EntityBase<TestArticle>
{
    [ValueSelector("//h1[@class='post-title']", SelectorType.XPath, TakeFirst = true)]
    public string? Title { get; set; }

    [ValueSelector(".author", SelectorType.Css, Attribute = "data-author-id", TakeFirst = true)]
    public int? AuthorId { get; set; }

    [ValueSelector("//time/@datetime", SelectorType.XPath, TakeFirst = true)]
    public DateTime? PublishedDate { get; set; }

    [ValueSelector(".views", SelectorType.Css, TakeFirst = true)]
    public int? ViewCount { get; set; }
}

/// <summary>
/// Test entity for JSON data
/// </summary>
[EntitySelector(Expression = "$.data.items[*]", Type = SelectorType.JsonPath)]
public class TestApiItem : EntityBase<TestApiItem>
{
    [ValueSelector("$.id", SelectorType.JsonPath, TakeFirst = true)]
    public new int? Id { get; set; }

    [ValueSelector("$.title", SelectorType.JsonPath, TakeFirst = true)]
    public string? Title { get; set; }

    [ValueSelector("$.value", SelectorType.JsonPath, TakeFirst = true)]
    public decimal? Value { get; set; }
}

/// <summary>
/// Test entity with various formatters
/// </summary>
public class TestFormattedEntity : EntityBase<TestFormattedEntity>
{
    [ValueSelector(".price", SelectorType.Css, TakeFirst = true)]
    [TrimFormatterAttribute(Order = 1)]
    public string? Price { get; set; }

    [ValueSelector(".date", SelectorType.Css, TakeFirst = true)]
    [DateTimeFormatterAttribute(Order = 1)]
    public DateTime? Date { get; set; }

    [ValueSelector(".url-encoded", SelectorType.Css, TakeFirst = true)]
    [ReplaceFormatterAttribute("_", " ", Order = 1)]
    public string? UrlValue { get; set; }
}
