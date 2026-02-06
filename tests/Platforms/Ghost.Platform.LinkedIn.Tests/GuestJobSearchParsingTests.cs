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
    public void JsonLdParser_FormatSalary_HandleNull()
    {
        var html = "";
        var extractor = new Ghost.Utilities.JsonLdExtractor();
        var parser = new Ghost.Platform.LinkedIn.Internal.JsonLdParser(extractor);
        var parsed = parser.Parse(html, "123", "https://www.linkedin.com/jobs/view/123");
        parsed.Should().BeNull();
    }

    [Fact]
    public void ParseExperience_NotApplicable_MapsToUnknown()
    {
        var level = GuestJobSearch_ParseExperience("Not Applicable");
        level.Should().Be(Contracts.Jobs.ExperienceLevel.Unknown);
    }

    private static Contracts.Jobs.ExperienceLevel GuestJobSearch_ParseExperience(string v)
    {
        // use reflection to call private static method ParseExperienceLevel in GuestJobSearch
        var mi = typeof(GuestJobSearch).GetMethod("ParseExperienceLevel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            ?? throw new System.InvalidOperationException("ParseExperienceLevel not found");
        var res = mi.Invoke(null, new object[] { v });
        return res is Contracts.Jobs.ExperienceLevel el ? el : Contracts.Jobs.ExperienceLevel.Unknown;
    }
}
