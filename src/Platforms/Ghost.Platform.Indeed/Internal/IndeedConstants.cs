using Ghost.Models;
using System.Collections.Generic;

namespace Ghost.Platform.Indeed.Internal;

internal static class IndeedConstants
{
    public const string ApiUrl = "https://apis.indeed.com/graphql";
    public const string ApiKey = "161092c2017b5bbab13edb12461a62d5a833871e7cad6d9d475304573de67ac8";

    public const string JobSearchQuery = """
        query GetJobData {{
            jobSearch(
                what: "{0}",
                location: "location: {{where: \"{1}\", radius: 50, radiusUnit: MILES}}",
                limit: {2},
                sort: RELEVANCE
            ) {{
                pageInfo {{
                    nextCursor
                }}
                results {{
                    job {{
                        key
                        title
                        employer {{ name }}
                        location {{ formatted {{ long }} }}
                        description {{ html }}
                        compensation {{ baseSalary {{ range {{ min max currency }} }} }}
                        datePublished
                    }}
                }}
            }}
        }}
    """;

    public static Dictionary<string,string> GetHeaders(CountryCode country)
    {
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

        return new Dictionary<string,string>
        {
            ["Host"] = "apis.indeed.com",
            ["indeed-api-key"] = ApiKey,
            // Match JobSpy iPhone User-Agent exactly
            ["User-Agent"] = "Mozilla/5.0 (iPhone; CPU iPhone OS 16_6_1 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Mobile/15E148 Indeed App 193.1",
            ["indeed-co"] = indeedCo,
            ["indeed-locale"] = locale,
            // Add indeed-app-info expected by the API
            ["indeed-app-info"] = "appv=193.1; appid=com.indeed.jobsearch; osv=16.6.1; os=ios; dtype=phone",
            // Ensure JSON content negotiation headers match curl
            ["accept"] = "application/json",
            ["content-type"] = "application/json",
            // Use recommended Accept-Language (can be adapted for locale if desired)
            ["accept-language"] = "en-US,en;q=0.9"
        };
    }
}
