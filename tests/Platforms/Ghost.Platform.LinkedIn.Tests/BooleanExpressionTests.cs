using System;
using FluentAssertions;
using Ghost.Plugin.LinkedIn.Internal;
using Xunit;

namespace Ghost.Plugin.LinkedIn.Tests;

public class BooleanExpressionTests
{
    [Theory]
    [InlineData("software engineer", "software%20engineer")]
    [InlineData("java OR python", "java%20OR%20python")]
    [InlineData("senior AND developer", "senior%20AND%20developer")]
    [InlineData("engineer NOT junior", "engineer%20NOT%20junior")]
    [InlineData("\"machine learning\"", "%22machine%20learning%22")]
    [InlineData("C++", "C%2B%2B")]
    [InlineData(".NET", ".NET")]
    [InlineData("node.js", "node.js")]
    [InlineData("(software OR data) AND engineer", "%28software%20OR%20data%29%20AND%20engineer")]
    [InlineData("(software OR data)AND engineer", "%28software%20OR%20data%29%20AND%20engineer")]
    [InlineData("(software OR data)AND( engineer )", "%28software%20OR%20data%29%20AND%20%28engineer%29")]
    [InlineData("NOT (junior OR intern)", "NOT%20%28junior%20OR%20intern%29")]
    [InlineData("\"machine learning\" AND C++", "%22machine%20learning%22%20AND%20C%2B%2B")]
    [InlineData("java or python", "java%20OR%20python")]
    [InlineData("senior and developer", "senior%20AND%20developer")]
    [InlineData("engineer not junior", "engineer%20NOT%20junior")]
    [InlineData("(c# OR .NET) AND \"full stack\"", "%28c%23%20OR%20.NET%29%20AND%20%22full%20stack%22")]
    [InlineData("\"C++ developer\" OR \".NET engineer\"", "%22C%2B%2B%20developer%22%20OR%20%22.NET%20engineer%22")]
    [InlineData("(python)OR(java)", "%28python%29%20OR%20%28java%29")]
    [InlineData("(python)OR(java) AND sql", "%28python%29%20OR%20%28java%29%20AND%20sql")]
    [InlineData("react   OR   vue", "react%20OR%20vue")]
    [InlineData("golang (remote)", "golang%20%28remote%29")]
    [InlineData("\"data scientist", "%22data%20scientist")]
    [InlineData("data scientist\"", "data%20scientist%22")]
    public void BuildSearchUrlEncodesQueries(string query, string expectedQuery)
    {
        var url = LinkedInQueryBuilder.BuildSearchUrl(query, "Seattle");

        url.Should().Contain($"keywords={expectedQuery}");
    }

    [Theory]
    [InlineData(0, "start=0")]
    [InlineData(25, "start=25")]
    [InlineData(-1, "start=0")]
    public void BuildSearchUrlIncludesOffset(int offset, string expected)
    {
        var url = LinkedInQueryBuilder.BuildSearchUrl("software engineer", "Seattle", offset);

        url.Should().Contain(expected);
    }

    [Theory]
    [InlineData(3600, "f_TPR=r3600")]
    [InlineData(86400, "f_TPR=r86400")]
    public void BuildSearchUrlIncludesPostedWithin(int seconds, string expected)
    {
        var url = LinkedInQueryBuilder.BuildSearchUrl("software engineer", "Seattle", postedWithin: TimeSpan.FromSeconds(seconds));

        url.Should().Contain(expected);
    }

    [Fact]
    public void BuildSearchUrlEmptyQueryStillBuildsUrl()
    {
        var url = LinkedInQueryBuilder.BuildSearchUrl("", "Seattle");

        url.Should().Contain("keywords=");
    }

    [Fact]
    public void BuildSearchUrlNullLocationStillBuildsUrl()
    {
        var url = LinkedInQueryBuilder.BuildSearchUrl("software engineer", null!);

        url.Should().Contain("location=");
    }
}
