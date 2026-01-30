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

    [Fact]
    public void ParsesMultipleJobsFromGraphQLResponse()
    {
         var json = """
{
  "data": {
    "jobSearchResults": {
      "jobs": [
        {
          "jobId": "123",
          "jobTitleText": "Software Engineer",
          "employerNameFromSearch": "Tech Corp",
          "location": "San Francisco, CA",
          "header": {
            "payPeriodAdjustedPay": {
              "p10": 100000,
              "p90": 150000,
              "payCurrency": "USD"
            }
          },
          "jobDescription": "Develop software",
          "postedDate": "2024-01-01",
          "applyUrl": "https://glassdoor.com/apply/123"
        },
        {
          "jobId": "124",
          "jobTitleText": "Product Manager",
          "employerNameFromSearch": "Product Inc",
          "location": "New York, NY",
          "header": {
            "payPeriodAdjustedPay": {
              "p10": 120000,
              "p90": 180000,
              "payCurrency": "USD"
            }
          },
          "jobDescription": "Manage products",
          "postedDate": "2024-01-02",
          "applyUrl": "https://glassdoor.com/apply/124"
        }
      ],
      "totalResults": 2,
      "pageInfo": {
        "hasNextPage": true,
        "endCursor": "cursor123"
      }
    }
  }
}
""";

        var list = GlassdoorJobParser.ParseSearchResponse(json);
        list.Should().HaveCount(2);
        
         var firstJob = list[0];
         firstJob.Title.Should().Be("Software Engineer");
         firstJob.Company.Should().Be("Tech Corp");
         firstJob.Location.Should().Be("San Francisco, CA");
         firstJob.Salary.Should().Be("100000 - 150000 USD");

         var secondJob = list[1];
         secondJob.Title.Should().Be("Product Manager");
         secondJob.Company.Should().Be("Product Inc");
         secondJob.Location.Should().Be("New York, NY");
         secondJob.Salary.Should().Be("120000 - 180000 USD");
    }

    [Fact]
    public void HandlesMissingSalaryDataGracefully()
    {
         var json = """
{
  "data": {
    "jobSearchResults": {
      "jobs": [
        {
          "jobId": "125",
          "jobTitleText": "Data Scientist",
          "employerNameFromSearch": "Data Corp",
          "location": "Seattle, WA",
          "jobDescription": "Analyze data",
          "postedDate": "2024-01-03",
          "applyUrl": "https://glassdoor.com/apply/125"
        }
      ]
    }
  }
}
""";

        var list = GlassdoorJobParser.ParseSearchResponse(json);
        list.Should().HaveCount(1);
        var job = list[0];
        job.Title.Should().Be("Data Scientist");
        job.Company.Should().Be("Data Corp");
        job.Salary.Should().BeNull();
    }

    [Fact]
    public void ReturnsEmptyListForInvalidJson()
    {
        var list = GlassdoorJobParser.ParseSearchResponse("invalid json");
        list.Should().BeEmpty();
    }

    [Fact]
    public void ReturnsEmptyListForNullJson()
    {
        var list = GlassdoorJobParser.ParseSearchResponse(null);
        list.Should().BeEmpty();
    }

    [Fact]
    public void ReturnsEmptyListForEmptyJson()
    {
        var list = GlassdoorJobParser.ParseSearchResponse("");
        list.Should().BeEmpty();
    }

    [Fact]
    public void ParsesJobWithAlternativeCompanyField()
    {
        var json = """
{
  "results": [
    {
      "jobTitleText": "DevOps Engineer",
      "employerName": "Cloud Corp",
      "jobId": "126",
      "location": "Remote"
    }
  ]
}
""";

        var list = GlassdoorJobParser.ParseSearchResponse(json);
        list.Should().HaveCount(1);
        var job = list[0];
        job.Title.Should().Be("DevOps Engineer");
        job.Company.Should().Be("Cloud Corp");
        job.Location.Should().Be("Remote");
    }

    [Fact]
    public void ParsesJobWithAlternativeLocationField()
    {
        var json = """
{
  "results": [
    {
      "jobTitleText": "UX Designer",
      "employerNameFromSearch": "Design Studio",
      "jobId": "127",
      "jobLocationCity": "Portland, OR"
    }
  ]
}
""";

        var list = GlassdoorJobParser.ParseSearchResponse(json);
        list.Should().HaveCount(1);
        var job = list[0];
        job.Title.Should().Be("UX Designer");
        job.Company.Should().Be("Design Studio");
        job.Location.Should().Be("Portland, OR");
    }

    [Fact]
    public void ParsesJobWithSingleSalaryValue()
    {
        var json = """
{
  "results": [
    {
      "jobTitleText": "QA Engineer",
      "employerNameFromSearch": "Test Corp",
      "jobId": "128",
      "header": {
        "payPeriodAdjustedPay": { "p10": 75000, "payCurrency": "USD" }
      }
    }
  ]
}
""";

        var list = GlassdoorJobParser.ParseSearchResponse(json);
        list.Should().HaveCount(1);
        var job = list[0];
        job.Salary.Should().Be("75000 USD");
    }
}