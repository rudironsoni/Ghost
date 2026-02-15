namespace Ghost.WebApi.Security;

public sealed class AdminApiKeyOptions
{
    public const string SectionName = "Ghost:Security:AdminApiKey";

    public bool Enabled { get; set; }

    public string HeaderName { get; set; } = "X-Ghost-Admin-Key";

    public string? ApiKey { get; set; }
}
