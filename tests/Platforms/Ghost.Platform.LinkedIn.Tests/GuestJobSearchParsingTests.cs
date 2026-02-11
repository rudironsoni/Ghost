using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Ghost.Contracts.Jobs;
using Ghost.Platform.LinkedIn.Internal;
using Xunit;

namespace Ghost.Platform.LinkedIn.Tests;

public class GuestJobSearchParsingTests
{
    [Fact]
    public void JsonLdParserFormatSalaryHandleNull()
    {
        var html = "";
        var extractor = new Ghost.Utilities.JsonLdExtractor();
        var parser = new Ghost.Platform.LinkedIn.Internal.JsonLdParser(extractor);
        var parsed = parser.Parse(html, "123", "https://www.linkedin.com/jobs/view/123");
        parsed.Should().BeNull();
    }

    [Fact]
    public void ParseExperienceNotApplicableMapsToUnknown()
    {
        var level = GuestJobSearch_ParseExperience("Not Applicable");
        level.Should().Be(Ghost.Contracts.Jobs.ExperienceLevel.Unknown);
    }

    private static Ghost.Contracts.Jobs.ExperienceLevel GuestJobSearch_ParseExperience(string v)
    {
        // use reflection to call private static method ParseExperienceLevel in GuestJobSearch
        var mi = typeof(GuestJobSearch).GetMethod("ParseExperienceLevel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            ?? throw new System.InvalidOperationException("ParseExperienceLevel not found");
        var res = mi.Invoke(null, new object[] { v });
        return res is Ghost.Contracts.Jobs.ExperienceLevel el ? el : Ghost.Contracts.Jobs.ExperienceLevel.Unknown;
    }
}
