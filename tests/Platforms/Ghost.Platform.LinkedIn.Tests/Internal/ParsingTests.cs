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

    [Theory]
    [InlineData("FULL_TIME", Ghost.Contracts.Jobs.JobType.FullTime)]
    [InlineData("PART_TIME", Ghost.Contracts.Jobs.JobType.PartTime)]
    [InlineData("CONTRACT", Ghost.Contracts.Jobs.JobType.Contract)]
    [InlineData("INTERNSHIP", Ghost.Contracts.Jobs.JobType.Internship)]
    [InlineData("Unknown", Ghost.Contracts.Jobs.JobType.Unknown)]
    [InlineData(null, Ghost.Contracts.Jobs.JobType.Unknown)]
    public void ParseJobType_MapsCorrectly(string? input, Ghost.Contracts.Jobs.JobType expected)
    {
        // Wrap JSON in script tag as the Parser expects HTML
        var json = $$"""
        <script type="application/ld+json">
        {
            "@context": "http://schema.org",
            "@type": "JobPosting",
            "employmentType": "{{input}}"
        }
        </script>
        """;

        var result = Ghost.Platform.LinkedIn.Internal.JsonLdParser.Parse(json, "123", "http://url");
        
        result.Should().NotBeNull();
        result!.JobType.Should().Be(expected);
    }
}
