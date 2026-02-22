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

        if (!IsPlatformEnabled(platform))
        {
            string envVar = $"GHOST_ENABLE_{platform.ToUpperInvariant()}_TESTS";
            Skip = $"{platform} tests disabled. Set {envVar}=true to enable. " +
                   $"These tests require external API access and cannot run in CI.";
        }
    }

    private static bool IsPlatformEnabled(string platform)
    {
        string envVar = $"GHOST_ENABLE_{platform.ToUpperInvariant()}_TESTS";
        string? value = Environment.GetEnvironmentVariable(envVar);
        return bool.TryParse(value, out bool result) && result;
    }
}
