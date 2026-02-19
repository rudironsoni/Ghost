using System;
using Xunit;

namespace Ghost.Testing.End2End;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class End2EndFactAttribute : FactAttribute
{
    public End2EndFactAttribute()
    {
        if (!IsEnabled())
        {
            Skip = "End2End tests are disabled by default. Set GHOST_E2E=1 to enable external/browser End2End runs.";
        }
    }

    private static bool IsEnabled()
    {
        string? value = Environment.GetEnvironmentVariable("GHOST_E2E");
        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }
}
