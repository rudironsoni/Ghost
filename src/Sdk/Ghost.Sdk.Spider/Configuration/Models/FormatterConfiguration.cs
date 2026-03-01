namespace Ghost.Sdk.Spider.Configuration.Models;

/// <summary>
/// Configuration for value formatters.
/// </summary>
public sealed class FormatterConfiguration
{
    /// <summary>
    /// Gets or sets the formatter type (Trim, Lower, Upper, Replace, Regex, DateFormat, NumberFormat, UrlResolve, Custom).
    /// </summary>
    public string Type { get; set; } = "Trim";

    /// <summary>
    /// Gets or sets formatter-specific parameters.
    /// </summary>
    public Dictionary<string, string> Parameters { get; set; } = [];

    /// <summary>
    /// Gets or sets custom formatter code (for Custom type).
    /// </summary>
    public string? CustomCode { get; set; }
}
