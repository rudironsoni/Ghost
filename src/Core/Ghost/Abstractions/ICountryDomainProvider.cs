using Ghost.Models;

namespace Ghost.Abstractions;

public interface ICountryDomainProvider
{
    string GetDomain(CountryCode country);
    string GetLocale(CountryCode country);
}
