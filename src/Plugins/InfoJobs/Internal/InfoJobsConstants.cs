using System.Globalization;
using System.Text.RegularExpressions;
using Ghost.Contracts.Jobs;

namespace Ghost.Plugin.InfoJobs.Internal;

public static class InfoJobsConstants
{
    public static readonly Dictionary<string, string> SearchHeaders = new()
    {
        ["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36",
        ["Accept"] = "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8",
        ["Accept-Language"] = "es-ES,es;q=0.9,en;q=0.8",
        ["Referer"] = "https://www.infojobs.net/",
        ["Upgrade-Insecure-Requests"] = "1",
        ["Sec-Ch-Ua"] = "\"Chromium\";v=\"133\", \"Google Chrome\";v=\"133\", \"Not?A_Brand\";v=\"99\"",
        ["Sec-Ch-Ua-Platform"] = "\"Windows\"",
        ["Sec-Fetch-Site"] = "same-origin",
        ["Sec-Fetch-Mode"] = "navigate",
        ["Sec-Fetch-User"] = "?1",
        ["Accept-Encoding"] = "gzip, deflate, br"
    };

    public static readonly Dictionary<string, string> ApiHeaders = new()
    {
        ["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
        ["Accept"] = "application/json",
        ["Accept-Language"] = "es-ES,es;q=0.9,en;q=0.8",
        ["Referer"] = "https://www.infojobs.net/",
        ["Sec-Fetch-Dest"] = "empty",
        ["Sec-Fetch-Mode"] = "cors",
        ["Sec-Fetch-Site"] = "same-origin"
    };

    // Spanish salary patterns for parsing
    public static readonly string[] SalaryPatterns = new[]
    {
        "€", // Euro symbol
        "euros",
        "eur",
        "EUR",
        "€/año", // €/year
        "€/mes", // €/month
        "salario",
        "remuneración"
    };

    // Spanish job type patterns
    public static readonly Dictionary<string, JobType> JobTypeMapping = new()
    {
        ["jornada completa"] = JobType.FullTime,
        ["tiempo completo"] = JobType.FullTime,
        ["jornada parcial"] = JobType.PartTime,
        ["tiempo parcial"] = JobType.PartTime,
        ["contrato"] = JobType.Contract,
        ["prácticas"] = JobType.Internship,
        ["becario"] = JobType.Internship,
        ["formación"] = JobType.Internship,
        ["freelance"] = JobType.Contract,
        ["autónomo"] = JobType.Contract,
        ["temporal"] = JobType.Contract,
        ["indefinido"] = JobType.FullTime,
        ["beca"] = JobType.Internship,
        ["obra"] = JobType.Contract,
        ["servicio"] = JobType.Contract
    };

    public static SalaryInfo ParseSpanishSalary(string salaryText)
    {
        if (string.IsNullOrWhiteSpace(salaryText))
            return new SalaryInfo(0, "EUR");

        string[] specialCases = new[] { "no especificado", "a convenir", "salario competitivo", "según experiencia" };
        if (specialCases.Any(sc => salaryText.ToLower(CultureInfo.InvariantCulture).Contains(sc, StringComparison.OrdinalIgnoreCase)))
            return new SalaryInfo(0, "EUR");

        string cleanText = salaryText
            .Replace(".", "")
            .Replace(",", ".")
            .Replace("€", "")
            .Replace("euros", "")
            .Replace("eur", "")
            .Replace("/año", "")
            .Replace("/mes", "")
            .Replace("brutos", "")
            .Replace("netos", "")
            .Replace("anuales", "");

        Match rangeMatch = System.Text.RegularExpressions.Regex.Match(cleanText, @"(\d+)\s*-\s*(\d+)");
        if (rangeMatch.Success)
        {
            decimal min = decimal.Parse(rangeMatch.Groups[1].Value, CultureInfo.InvariantCulture);
            decimal max = decimal.Parse(rangeMatch.Groups[2].Value, CultureInfo.InvariantCulture);
            decimal average = (min + max) / 2;
            return new SalaryInfo(average, "EUR");
        }

        Match singleMatch = System.Text.RegularExpressions.Regex.Match(cleanText, @"(\d+(?:\.\d+)?)");
        if (singleMatch.Success)
        {
            decimal amount = decimal.Parse(singleMatch.Groups[1].Value, CultureInfo.InvariantCulture);
            return new SalaryInfo(amount, "EUR");
        }

        return new SalaryInfo(0, "EUR");
    }

    public static JobType MapSpanishJobType(string spanishType)
    {
        if (string.IsNullOrWhiteSpace(spanishType))
            return JobType.Unknown;

        string lowerType = spanishType.ToLower(CultureInfo.InvariantCulture);

        if (lowerType.Contains("contrato en prácticas", StringComparison.OrdinalIgnoreCase) ||
            lowerType.Contains("contrato de prácticas", StringComparison.OrdinalIgnoreCase))
            return JobType.Internship;

        if (lowerType.Contains("contrato indefinido", StringComparison.OrdinalIgnoreCase))
            return JobType.FullTime;

        if (lowerType.Contains("obra o servicio", StringComparison.OrdinalIgnoreCase))
            return JobType.Contract;

        foreach (KeyValuePair<string, JobType> mapping in JobTypeMapping)
        {
            if (lowerType.Contains(mapping.Key, StringComparison.OrdinalIgnoreCase))
                return mapping.Value;
        }

        return JobType.Unknown;
    }

    public static string[] GetSpanishSalaryPatterns() => SalaryPatterns;

    public static Dictionary<string, JobType> GetSpanishJobTypeMappings() => JobTypeMapping;
}

public record SalaryInfo(decimal Amount, string Currency);
