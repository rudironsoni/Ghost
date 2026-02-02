using Ghost.Utilities;
using Xunit;

namespace Ghost.Core.Tests;

public class DateParserTests
{
    private readonly DateParser _parser = new();

    [Fact]
    public void ParseMonthYear()
    {
        var d = _parser.ParseDate("Jan 2024");
        Assert.NotNull(d);
        Assert.Equal(2024, d.Value.Year);
        Assert.Equal(1, d.Value.Month);
    }

    [Fact]
    public void ParseRangePresent()
    {
        var (s, e) = _parser.ParseDateRange("Mar 2020 - Present");
        Assert.NotNull(s);
        Assert.Null(e);
    }

    [Fact]
    public void ParseRelativeDaysAgo()
    {
        var dt = _parser.ParseRelativeDate("3 days ago");
        Assert.NotNull(dt);
        Assert.True((DateTime.UtcNow - dt.Value).TotalDays >= 2);
    }
}
