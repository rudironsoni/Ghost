using System.Collections.Generic;
using Ghost.Models;

namespace Ghost.Platform.Indeed.Internal;

internal static class IndeedConstants
{
    public const string ApiUrl = "https://apis.indeed.com/graphql";


    public const string JobSearchQuery = """
        query GetJobData {{
            jobSearch(
                what: "{0}",
                location: {{where: "{1}", radius: 50, radiusUnit: MILES}},
                limit: {2},
                sort: RELEVANCE
            ) {{
                pageInfo {{
                    nextCursor
                    hasNextPage
                }}
                results {{
                    job {{
                        key
                        title
                        employer {{ name }}
                        location {{ formatted {{ long }} }}
                        description {{ html }}
                        compensation {{
                            baseSalary {{
                                range {{
                                    ... on Range {{
                                        min
                                        max
                                    }}
                                }}
                            }}
                        }}
                        datePublished
                    }}
                }}
            }}
        }}
    """;

    public static Dictionary<string, string> GetHeaders(CountryCode country, string apiKey)
    {
        // Use the provided API key or throw if empty
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new ArgumentException("Indeed API key is required. Please provide a valid API key in configuration.", nameof(apiKey));
        }
        var keyToUse = apiKey;

        var locale = country switch
        {
            CountryCode.US => "en-US",
            CountryCode.UK => "en-GB",
            CountryCode.ES => "es-ES",
            CountryCode.DE => "de-DE",
            CountryCode.FR => "fr-FR",
            _ => "en-US"
        };

        var domain = country switch
        {
            CountryCode.US => "indeed.com",
            CountryCode.UK => "indeed.co.uk",
            CountryCode.ES => "indeed.es",
            CountryCode.DE => "indeed.de",
            CountryCode.FR => "indeed.fr",
            CountryCode.MX => "indeed.com.mx",
            _ => "indeed.com"
        };

        // indeed-co expects an ISO country code (e.g. "US", "ES", "GB")
        var indeedCo = country == CountryCode.UK ? "GB" : country.ToString().ToUpperInvariant();

        // Build an Accept-Language header from the locale (eg "es-ES" -> "es-ES,es;q=0.9")
        var language = locale.Split('-')[0];
        var acceptLanguage = $"{locale},{language};q=0.9";

        return new Dictionary<string, string>
        {
            ["Host"] = "apis.indeed.com",
            ["indeed-api-key"] = keyToUse,
            // Match JobSpy iPhone User-Agent exactly
            ["User-Agent"] = "Mozilla/5.0 (iPhone; CPU iPhone OS 16_6_1 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Mobile/15E148 Indeed App 193.1",
            ["indeed-co"] = indeedCo,
            ["indeed-locale"] = locale,
            // Add indeed-app-info expected by the API
            ["indeed-app-info"] = "appv=193.1; appid=com.indeed.jobsearch; osv=16.6.1; os=ios; dtype=phone",
            // Ensure JSON content negotiation headers match curl
            ["accept"] = "application/json",
            ["content-type"] = "application/json",
            // Use locale-derived Accept-Language to better match user locale
            ["accept-language"] = acceptLanguage
        };
    }
}
