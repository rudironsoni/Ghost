using System;
using FluentAssertions;
using Xunit;

namespace Ghost.Platform.LinkedIn.Tests.Internal;

public class ParsingTests
{
    [Theory]
    [InlineData("Jan 2020 - Present", 2020, 1, false, 0)]
    [InlineData("2015 - 2019", 2015, 1, true, 2019)]
    [InlineData("May 2022", 2022, 5, false, 0)]
    public void DateParserParse_ValidInputs(string input, int expStartYear, int expStartMonth, bool hasEnd, int expEndYear)
    {
        var (start, end) = Ghost.Platform.LinkedIn.Internal.DateParser.Parse(input);

        start.Should().NotBeNull();
        start!.Value.Year.Should().Be(expStartYear);
        start.Value.Month.Should().Be(expStartMonth);

        if (hasEnd)
        {
            end.Should().NotBeNull();
            if (expEndYear != 0)
                end!.Value.Year.Should().Be(expEndYear);
        }
        else
        {
            end.Should().BeNull();
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("Not a date")]
    [InlineData("Foo - Bar")]
    public void DateParserParse_InvalidInputs_ShouldReturnNulls(string input)
    {
        var (start, end) = Ghost.Platform.LinkedIn.Internal.DateParser.Parse(input);

        // As per spec, parser returns result with Start/End null for invalids
        start.Should().BeNull();
        end.Should().BeNull();
    }
}
