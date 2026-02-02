using System.Text.Json;
using Xunit;
using Ghost.Platform.Indeed.Internal;

namespace Ghost.Platform.Indeed.Tests;

public class IndeedJobParserTests
{
    [Fact]
    public void ParsesSampleResponse()
    {
        var json = @"{
  ""data"": {
    ""jobSearch"": {
      ""results"": [
        {
          ""id"": ""abc123"",
          ""title"": ""Software Engineer"",
          ""employer"": { ""name"": ""ACME"" },
          ""location"": { ""formatted"": { ""long"": ""New York, NY"" } },
          ""description"": { ""html"": ""<p>Job</p>"" },
          ""compensation"": { ""baseSalary"": { ""range"": { ""min"": 50000, ""max"": 100000, ""currency"": ""USD"" } } }
        }
      ],
      ""pageInfo"": { ""nextCursor"": null }
    }
  }
}";

        using var doc = JsonDocument.Parse(json);
        var list = IndeedJobParser.ParseJobs(doc.RootElement);
        var arr = System.Linq.Enumerable.ToArray(list);
        Assert.Single(arr);
        Assert.Equal("Software Engineer", arr[0].Title);
        Assert.Equal("ACME", arr[0].Company);
        Assert.Contains("New York", arr[0].Location);
        Assert.Equal("Job", arr[0].Description);
        Assert.Contains("50000", arr[0].Salary);
    }
}
