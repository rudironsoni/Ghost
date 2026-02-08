namespace Ghost.Platform.Glassdoor.Internal;

public static class GlassdoorConstants
{
    public static string ApiUrl => "https://www.glassdoor.com/graph";

    // Minimal identifier for the GraphQL operation used by Glassdoor - kept as a template placeholder
    public const string QueryTemplate = "JobSearchResultsQuery";

    /// <summary>
    /// Simplified GraphQL query for job search - focuses on essential fields only
    /// This minimal query reduces complexity and improves reliability
    /// </summary>
    public const string JobSearchQuery = """
        query JobSearchResultsQuery(
            $keyword: String,
            $locationId: Int,
            $locationType: LocationTypeEnum,
            $numJobsToShow: Int!,
            $pageCursor: String,
            $pageNumber: Int
        ) {
            jobListings(
                contextHolder: {
                    searchParams: {
                        keyword: $keyword,
                        locationId: $locationId,
                        locationType: $locationType,
                        numPerPage: $numJobsToShow,
                        pageCursor: $pageCursor,
                        pageNumber: $pageNumber,
                        searchType: SR
                    }
                }
            ) {
                jobListings {
                    ...JobViewMinimal
                    __typename
                }
                totalJobsCount
                __typename
            }
        }

        fragment JobViewMinimal on JobListingSearchResult {
            jobview {
                header {
                    jobTitleText
                    locationName
                    employer {
                        name
                        __typename
                    }
                    jobLink
                    easyApply
                    __typename
                }
                job {
                    description
                    listingId
                    __typename
                }
                __typename
            }
            __typename
        }
    """;

    /// <summary>
    /// Alternative minimal query for testing - even simpler structure
    /// </summary>
    public const string JobSearchQueryMinimal = """
        query JobSearchResultsQuery(
            $keyword: String,
            $locationId: Int,
            $locationType: LocationTypeEnum,
            $numJobsToShow: Int!
        ) {
            jobListings(
                contextHolder: {
                    searchParams: {
                        keyword: $keyword,
                        locationId: $locationId,
                        locationType: $locationType,
                        numPerPage: $numJobsToShow,
                        searchType: SR
                    }
                }
            ) {
                jobListings {
                    jobview {
                        header {
                            jobTitleText
                            locationName
                            employer {
                                name
                            }
                            jobLink
                        }
                        job {
                            description
                            listingId
                        }
                    }
                }
                totalJobsCount
            }
        }
    """;

    // Fallback token from JobSpy - used when CSRF cannot be obtained
    // This token allows GraphQL requests to work even without a valid CSRF token
    public const string FallbackToken = "Ft6oHEWlRZrxDww95Cpazw:0pGUrkb2y3TyOpAIqF2vbPmUXoXVkD3oEGDVkvfeCerceQ5-n8mBg3BovySUIjmCPHCaW0H2nQVdqzbtsYqf4Q:wcqRqeegRUa9MVLJGyujVXB7vWFPjdaS1CtrrzJq-ok";

    /// <summary>
    /// Comprehensive browser headers for CSRF token retrieval (GET request to Glassdoor homepage)
    /// These headers simulate a real browser request to avoid blocking by anti-bot measures.
    /// </summary>
    public static readonly Dictionary<string, string> CsrfHeaders = new()
    {
        ["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36",
        ["Accept"] = "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7",
        ["Accept-Language"] = "en-US,en;q=0.9",
        ["Accept-Encoding"] = "gzip, deflate, br",
        ["Connection"] = "keep-alive",
        ["Upgrade-Insecure-Requests"] = "1",
        ["Sec-Fetch-Dest"] = "document",
        ["Sec-Fetch-Mode"] = "navigate",
        ["Sec-Fetch-Site"] = "none",
        ["Sec-Fetch-User"] = "?1",
        ["Sec-Ch-Ua"] = "\"Not_A Brand\";v=\"8\", \"Chromium\";v=\"120\", \"Google Chrome\";v=\"120\"",
        ["Sec-Ch-Ua-Mobile"] = "?0",
        ["Sec-Ch-Ua-Platform"] = "\"Windows\"",
        ["Cache-Control"] = "max-age=0"
    };

    /// <summary>
    /// Comprehensive browser headers for GraphQL queries (POST request to /graph endpoint)
    /// These headers simulate a real browser request to avoid blocking by anti-bot measures.
    /// The gd-csrf-token header is added dynamically by GlassdoorApiClient.
    /// </summary>
    public static readonly Dictionary<string, string> GraphHeaders = new()
    {
        ["User-Agent"] = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/138.0.0.0 Safari/537.36",
        ["Accept"] = "*/*",
        ["Accept-Language"] = "en-US,en;q=0.9",
        ["Accept-Encoding"] = "gzip, deflate, br",
        ["Content-Type"] = "application/json",
        ["Origin"] = "https://www.glassdoor.com",
        ["Referer"] = "https://www.glassdoor.com/",
        ["Connection"] = "keep-alive",
        ["Sec-Fetch-Dest"] = "empty",
        ["Sec-Fetch-Mode"] = "cors",
        ["Sec-Fetch-Site"] = "same-origin",
        ["Sec-Ch-Ua"] = "\"Chromium\";v=\"118\", \"Google Chrome\";v=\"118\", \"Not=A?Brand\";v=\"99\"",
        ["Sec-Ch-Ua-Mobile"] = "?0",
        ["Sec-Ch-Ua-Platform"] = "\"macOS\"",
        // Apollo GraphQL client identifiers (these are required by Glassdoor and JobSpy)
        ["apollographql-client-name"] = "job-search-next",
        ["apollographql-client-version"] = "4.75.0",
        // Additional origin/authority style headers used by JobSpy
        ["authority"] = "www.glassdoor.com",
        ["origin"] = "https://www.glassdoor.com",
        ["referer"] = "https://www.glassdoor.com/",
        ["sec-ch-ua-platform"] = "\"macOS\"",
        ["sec-fetch-dest"] = "empty",
        ["sec-fetch-mode"] = "cors",
        ["sec-fetch-site"] = "same-origin"
    };

    /// <summary>
    /// Alternative headers for consent/blocking page bypass attempts
    /// Different user agents and headers that might bypass blocking mechanisms
    /// </summary>
    public static readonly Dictionary<string, string> AlternativeHeaders = new()
    {
        ["User-Agent"] = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36",
        ["Accept"] = "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7",
        ["Accept-Language"] = "en-US,en;q=0.5",
        ["Accept-Encoding"] = "gzip, deflate, br",
        ["Connection"] = "keep-alive",
        ["Upgrade-Insecure-Requests"] = "1",
        ["Sec-Fetch-Dest"] = "document",
        ["Sec-Fetch-Mode"] = "navigate",
        ["Sec-Fetch-Site"] = "none",
        ["Sec-Fetch-User"] = "?1",
        ["Sec-Ch-Ua"] = "\"Not_A Brand\";v=\"8\", \"Chromium\";v=\"120\", \"Google Chrome\";v=\"120\"",
        ["Sec-Ch-Ua-Mobile"] = "?0",
        ["Sec-Ch-Ua-Platform"] = "\"macOS\"",
        ["Cache-Control"] = "max-age=0",
        ["DNT"] = "1" // Do Not Track header
    };
}
