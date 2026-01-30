using FluentAssertions;
using Ghost.Platform.Glassdoor.Internal;
using Xunit;

namespace Ghost.Platform.Glassdoor.Tests;

public class GlassdoorJobParserTests
{
    [Fact]
    public void ParsesSalaryRangeFromHeaderPayPeriodAdjustedPay()
    {
        var json = """
{
  "results": [
    {
      "jobTitleText": "Software Engineer",
      "employerNameFromSearch": "Acme Corp",
      "jobId": "123",
      "header": {
        "payPeriodAdjustedPay": { "p10": 50000, "p90": 80000, "payCurrency": "EUR" }
      }
    }
  ]
}
""";

        var list = GlassdoorJobParser.ParseSearchResponse(json);
        list.Should().HaveCount(1);
        var job = list[0];
        job.Title.Should().Be("Software Engineer");
        job.Company.Should().Be("Acme Corp");
        job.Salary.Should().Be("50000 - 80000 EUR");
    }
}