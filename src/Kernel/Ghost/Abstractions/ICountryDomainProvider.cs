using Ghost.Models;

namespace Ghost.Abstractions;

public interface ICountryDomainProvider
{
    public string GetDomain(CountryCode country);
    public string GetLocale(CountryCode country);
}
