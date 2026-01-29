using Ghost.Models;
using System.Collections.Generic;

namespace Ghost.Platform.Indeed.Internal;

internal static class IndeedConstants
{
    public const string ApiUrl = "https://apis.indeed.com/graphql";
    public const string ApiKey = "161092c2017b5bbab13edb12461a62d5a833871e7cad6d9d475304573de67ac8";

    // Simplified: copy the JobSearch query placeholder (should be replaced with full query if available)
    public const string JobSearchQuery = @"query JobSearch($what: String!, $where: String, $pageSize: Int, $cursor: String) {\n  jobSearch(what: $what, where: $where, pageSize: $pageSize, cursor: $cursor) {\n    results {\n      id\n      title\n      employer { name }\n      location { formatted { long } }\n      description { html }\n      compensation { baseSalary { range { min max currency } } }\n    }\n    pageInfo { nextCursor }\n  }\n}";

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
            ["Api-Key"] = ApiKey,
            ["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
            ["indeed-co"] = indeedCo,
            ["indeed-locale"] = locale
        };
    }
}
