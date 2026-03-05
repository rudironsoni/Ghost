using System;
using Xunit;

namespace Ghost.Testing.End2End;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class End2EndFactAttribute : FactAttribute
{
    public End2EndFactAttribute()
    {
        // E2E tests run by default unless explicitly disabled
        if (IsDisabled())
        {
            Skip = "End2End tests disabled. Unset GHOST_DISABLE_E2E or set it to 'false' to enable.";
        }
    }

    private static bool IsDisabled()
    {
        string? value = Environment.GetEnvironmentVariable("GHOST_DISABLE_E2E");
        // Default to enabled (not disabled) if not set
        return bool.TryParse(value, out bool result) && result;
    }
}
