namespace Ghost.Platform.Glassdoor.Internal;

internal static class GlassdoorConstants
{
    public static string ApiUrl => "https://www.glassdoor.com/graph";

    // Minimal identifier for the GraphQL operation used by Glassdoor - kept as a template placeholder
    public const string QueryTemplate = "JobSearchResultsQuery";

    // Fallback token used when CSRF cannot be obtained
    public const string FallbackToken = "S...";

    // Common headers used when calling the Graph endpoint
    public static readonly (string Name, string Value)[] Headers = new[]
    {
        ("apollographql-client-name", "Glassdoor-Frontend"),
        ("apollographql-client-version", "1.0")
    };
}
