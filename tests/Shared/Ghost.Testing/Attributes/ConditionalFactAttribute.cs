using System;
using Xunit;

namespace Ghost.Testing.Attributes;

/// <summary>
/// A Fact attribute that conditionally skips tests based on environment configuration.
/// Replaces [Fact(Skip = "...")] with conditional execution that can be enabled via environment variables.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class ConditionalFactAttribute : FactAttribute
{
    private static readonly HashSet<string> ValidPlatforms = new(StringComparer.OrdinalIgnoreCase)
    {
        "Indeed",
        "LinkedIn",
        "Google",
        "Glassdoor",
        "InfoJobs",
        "MultiPlatform"
    };

    public ConditionalFactAttribute(string platform)
    {
        if (!ValidPlatforms.Contains(platform))
        {
            throw new ArgumentException(
                $"Invalid platform '{platform}'. Valid platforms: {string.Join(", ", ValidPlatforms)}",
                nameof(platform));
        }

        // Tests run by default unless explicitly disabled via environment variable
        if (IsPlatformDisabled(platform))
        {
            string envVar = $"GHOST_DISABLE_{platform.ToUpperInvariant()}_TESTS";
            Skip = $"{platform} tests disabled. Unset {envVar} or set it to 'false' to enable.";
        }
    }

    private static bool IsPlatformDisabled(string platform)
    {
        string envVar = $"GHOST_DISABLE_{platform.ToUpperInvariant()}_TESTS";
        string? value = Environment.GetEnvironmentVariable(envVar);
        // Default to enabled (false) if not set, check if explicitly disabled
        return bool.TryParse(value, out bool result) && result;
    }
}
