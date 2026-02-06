using Ghost;

namespace Ghost.Platform.LinkedIn.Internal;

internal static class LinkedInOptionsExtensions
{
    public static PageOptions? GetPageOptions(this LinkedInOptions options)
    {
        if (options is null) return null;

        if (options.TimezoneId != null || options.Locale != null)
        {
            return new PageOptions
            {
                TimezoneId = options.TimezoneId,
                Locale = options.Locale
            };
        }
        return null;
    }
}
