using System;
using Ghost.Abstractions;
using Ghost.Models;

namespace Ghost.Platform.LinkedIn.Internal;

internal sealed class LinkedInCountryProvider : ICountryDomainProvider
{
    public string GetDomain(CountryCode country)
    {
        return country switch
        {
            CountryCode.US => "https://www.linkedin.com",
            _ => $"https://{country.ToString().ToLowerInvariant()}.linkedin.com"
        };
    }

    public string GetLocale(CountryCode country)
    {
        return country switch
        {
            CountryCode.ES => "es-ES",
            CountryCode.US => "en-US",
            CountryCode.UK => "en-GB",
            _ => "en-US"
        };
    }
}
