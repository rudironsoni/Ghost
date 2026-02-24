using Ghost.Testing.Reliability;
using Ghost.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Kernel.Tests;

public class DateParserTests : ReliabilityTestBase
{
    public DateParserTests(ITestOutputHelper output) : base(output) { }

    private readonly DateParser _parser = new();

    [Fact]
    public void ParseMonthYear()
    {
        DateOnly? d = _parser.ParseDate("Jan 2024");
        Assert.NotNull(d);
        Assert.Equal(2024, d.Value.Year);
        Assert.Equal(1, d.Value.Month);
    }

    [Fact]
    public void ParseRangePresent()
    {
        (DateOnly? s, DateOnly? e) = _parser.ParseDateRange("Mar 2020 - Present");
        Assert.NotNull(s);
        Assert.Null(e);
    }

    [Fact]
    public void ParseRelativeDaysAgo()
    {
        DateTime? dt = _parser.ParseRelativeDate("3 days ago");
        Assert.NotNull(dt);
        Assert.True((DateTime.UtcNow - dt.Value).TotalDays >= 2);
    }
}
