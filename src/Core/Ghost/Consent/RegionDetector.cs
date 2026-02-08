using Microsoft.Playwright;

namespace Ghost.Consent;

/// <summary>
/// Detects the user's geographic region to determine applicable privacy regulations.
/// Supports GDPR (Europe), CCPA (California), LGPD (Brazil), and others.
/// </summary>
public static class RegionDetector
{
    /// <summary>
    /// Privacy regulation types.
    /// </summary>
    public enum PrivacyRegulation
    {
        /// <summary>Unknown or not detected.</summary>
        Unknown,

        /// <summary>General Data Protection Regulation (Europe).</summary>
        GDPR,

        /// <summary>California Consumer Privacy Act (California, USA).</summary>
        CCPA,

        /// <summary>Lei Geral de Proteção de Dados (Brazil).</summary>
        LGPD,

        /// <summary>Personal Information Protection and Electronic Documents Act (Canada).</summary>
        PIPEDA,

        /// <summary>Other region with no specific regulation detected.</summary>
        Other
    }

    /// <summary>
    /// Detects the privacy regulation applicable to the current page context.
    /// </summary>
    /// <param name="page">The page to analyze.</param>
    /// <returns>The detected privacy regulation.</returns>
    public static async Task<PrivacyRegulation> DetectRegulationAsync(IPage page)
    {
        ArgumentNullException.ThrowIfNull(page);

        try
        {
            // Check page content for regulation-specific keywords
            var pageContent = await page.GetContentAsync();
            var regulation = DetectFromContent(pageContent);
            if (regulation != PrivacyRegulation.Unknown)
            {
                return regulation;
            }

            // Check for region-specific CMP configurations
            regulation = await DetectFromCMPAsync(page);
            if (regulation != PrivacyRegulation.Unknown)
            {
                return regulation;
            }

            // Fallback: try to detect from browser geolocation (if available)
            regulation = await DetectFromGeolocationAsync(page);
            return regulation;
        }
        catch
        {
            return PrivacyRegulation.Unknown;
        }
    }

    /// <summary>
    /// Detects regulation from page content (meta tags, text content).
    /// </summary>
    private static PrivacyRegulation DetectFromContent(string content)
    {
        var lowerContent = content.ToLowerInvariant();

        // GDPR indicators
        if (lowerContent.Contains("gdpr") ||
            lowerContent.Contains("general data protection regulation") ||
            lowerContent.Contains("european economic area") ||
            lowerContent.Contains("data protection officer"))
        {
            return PrivacyRegulation.GDPR;
        }

        // CCPA indicators
        if (lowerContent.Contains("ccpa") ||
            lowerContent.Contains("california consumer privacy act") ||
            lowerContent.Contains("do not sell my personal information") ||
            lowerContent.Contains("do not sell or share my personal information"))
        {
            return PrivacyRegulation.CCPA;
        }

        // LGPD indicators
        if (lowerContent.Contains("lgpd") ||
            lowerContent.Contains("lei geral de proteção de dados") ||
            lowerContent.Contains("lei geral de protecao de dados"))
        {
            return PrivacyRegulation.LGPD;
        }

        // PIPEDA indicators
        if (lowerContent.Contains("pipeda") ||
            lowerContent.Contains("personal information protection and electronic documents act"))
        {
            return PrivacyRegulation.PIPEDA;
        }

        return PrivacyRegulation.Unknown;
    }

    /// <summary>
    /// Detects regulation from CMP configuration and attributes.
    /// </summary>
    private static async Task<PrivacyRegulation> DetectFromCMPAsync(IPage page)
    {
        try
        {
            // Check for GDPR-specific CMP attributes
            var hasGdpr = await page.EvaluateAsync<bool>(@"
                () => {
                    const gdprElements = document.querySelectorAll('[data-gdpr], [data-cmp-gdpr]');
                    return gdprElements.length > 0;
                }
            ");

            if (hasGdpr)
            {
                return PrivacyRegulation.GDPR;
            }

            // Check for CCPA-specific CMP attributes
            var hasCcpa = await page.EvaluateAsync<bool>(@"
                () => {
                    const ccpaElements = document.querySelectorAll('[data-ccpa], [data-cmp-ccpa]');
                    const ccpaLinks = Array.from(document.querySelectorAll('a')).some(a => 
                        a.textContent?.toLowerCase().includes('do not sell')
                    );
                    return ccpaElements.length > 0 || ccpaLinks;
                }
            ");

            if (hasCcpa)
            {
                return PrivacyRegulation.CCPA;
            }

            // Check for LGPD-specific indicators
            var hasLgpd = await page.EvaluateAsync<bool>(@"
                () => {
                    const lgpdElements = document.querySelectorAll('[data-lgpd], [data-cmp-lgpd]');
                    return lgpdElements.length > 0;
                }
            ");

            if (hasLgpd)
            {
                return PrivacyRegulation.LGPD;
            }
        }
        catch
        {
            // Continue to next detection method
        }

        return PrivacyRegulation.Unknown;
    }

    /// <summary>
    /// Attempts to detect regulation from browser geolocation API (if permitted).
    /// </summary>
    private static async Task<PrivacyRegulation> DetectFromGeolocationAsync(IPage page)
    {
        try
        {
            // Try to get timezone as a proxy for location
            var timezone = await page.EvaluateAsync<string>(@"
                () => {
                    try {
                        return Intl.DateTimeFormat().resolvedOptions().timeZone;
                    } catch {
                        return '';
                    }
                }
            ");

            if (!string.IsNullOrEmpty(timezone))
            {
                return MapTimezoneToRegulation(timezone);
            }

            // Try to get locale
            var locale = await page.EvaluateAsync<string>(@"
                () => {
                    try {
                        return navigator.language || navigator.userLanguage || '';
                    } catch {
                        return '';
                    }
                }
            ");

            if (!string.IsNullOrEmpty(locale))
            {
                return MapLocaleToRegulation(locale);
            }
        }
        catch
        {
            // Geolocation not available or blocked
        }

        return PrivacyRegulation.Unknown;
    }

    /// <summary>
    /// Maps timezone to likely privacy regulation.
    /// </summary>
    private static PrivacyRegulation MapTimezoneToRegulation(string timezone)
    {
        var tz = timezone.ToLowerInvariant();

        // European timezones
        if (tz.StartsWith("europe/", StringComparison.OrdinalIgnoreCase) || tz.Contains("brussels") || tz.Contains("paris") ||
            tz.Contains("berlin") || tz.Contains("madrid") || tz.Contains("rome") ||
            tz.Contains("amsterdam") || tz.Contains("stockholm") || tz.Contains("vienna"))
        {
            return PrivacyRegulation.GDPR;
        }

        // California timezone
        if (tz.Contains("los_angeles") || tz.Contains("america/los_angeles"))
        {
            return PrivacyRegulation.CCPA;
        }

        // Brazil timezone
        if (tz.StartsWith("america/sao_paulo", StringComparison.OrdinalIgnoreCase) || tz.Contains("brazil"))
        {
            return PrivacyRegulation.LGPD;
        }

        // Canada timezones
        if (tz.Contains("toronto") || tz.Contains("vancouver") || tz.Contains("montreal"))
        {
            return PrivacyRegulation.PIPEDA;
        }

        return PrivacyRegulation.Unknown;
    }

    /// <summary>
    /// Maps browser locale to likely privacy regulation.
    /// </summary>
    private static PrivacyRegulation MapLocaleToRegulation(string locale)
    {
        var loc = locale.ToLowerInvariant();

        // European locales
        if (loc.StartsWith("de", StringComparison.OrdinalIgnoreCase) || loc.StartsWith("fr", StringComparison.OrdinalIgnoreCase) || loc.StartsWith("es", StringComparison.OrdinalIgnoreCase) ||
            loc.StartsWith("it", StringComparison.OrdinalIgnoreCase) || loc.StartsWith("nl", StringComparison.OrdinalIgnoreCase) || loc.StartsWith("pl", StringComparison.OrdinalIgnoreCase) ||
            loc.StartsWith("pt-pt", StringComparison.OrdinalIgnoreCase) || loc.StartsWith("sv", StringComparison.OrdinalIgnoreCase) || loc.StartsWith("da", StringComparison.OrdinalIgnoreCase))
        {
            return PrivacyRegulation.GDPR;
        }

        // Brazilian Portuguese
        if (loc.StartsWith("pt-br", StringComparison.OrdinalIgnoreCase))
        {
            return PrivacyRegulation.LGPD;
        }

        // Canadian locales
        if (loc.StartsWith("en-ca", StringComparison.OrdinalIgnoreCase) || loc.StartsWith("fr-ca", StringComparison.OrdinalIgnoreCase))
        {
            return PrivacyRegulation.PIPEDA;
        }

        // US English could be CCPA, but not guaranteed
        if (loc.StartsWith("en-us", StringComparison.OrdinalIgnoreCase))
        {
            return PrivacyRegulation.Other; // Could be CCPA but not certain
        }

        return PrivacyRegulation.Unknown;
    }

    /// <summary>
    /// Gets recommended consent strategy based on detected regulation.
    /// </summary>
    /// <param name="regulation">The detected privacy regulation.</param>
    /// <returns>Strategy description.</returns>
    public static string GetConsentStrategy(PrivacyRegulation regulation)
    {
        return regulation switch
        {
            PrivacyRegulation.GDPR => "Strict consent required - look for 'Accept All' or explicit consent buttons",
            PrivacyRegulation.CCPA => "Opt-out model - look for 'Do Not Sell' links or accept all",
            PrivacyRegulation.LGPD => "Strict consent required - similar to GDPR",
            PrivacyRegulation.PIPEDA => "Consent required - look for accept buttons",
            PrivacyRegulation.Other => "Best-effort consent - try generic accept buttons",
            _ => "Unknown regulation - try generic detection"
        };
    }
}
