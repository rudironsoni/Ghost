namespace Ghost.Sdk.Spider.Configuration.Models;

/// <summary>
/// Configuration for data extraction.
/// </summary>
public sealed class ExtractionConfiguration
{
    /// <summary>
    /// Gets or sets the extraction engine (Playwright, AngleSharp, HtmlAgilityPack).
    /// </summary>
    public string Engine { get; set; } = "Playwright";

    /// <summary>
    /// Gets or sets the default selector type (CSS, XPath, JsonPath, JMESPath).
    /// </summary>
    public string DefaultSelectorType { get; set; } = "CSS";

    /// <summary>
    /// Gets or sets whether JavaScript rendering is required.
    /// </summary>
    public bool RequiresJavaScript { get; set; } = true;

    /// <summary>
    /// Gets or sets wait time after page load (milliseconds).
    /// </summary>
    public int WaitAfterLoad { get; set; } = 1000;

    /// <summary>
    /// Gets or sets custom JavaScript to execute before extraction.
    /// </summary>
    public string? PreExtractionScript { get; set; }

    /// <summary>
    /// Gets or sets the entities to extract.
    /// </summary>
    public List<EntityConfiguration> Entities { get; set; } = new();
}
