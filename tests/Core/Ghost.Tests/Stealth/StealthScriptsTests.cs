using FluentAssertions;
using Ghost.Stealth;
using Xunit;

namespace Ghost.Tests.Stealth;

public class StealthScriptsTests
{
    [Fact]
    public void GetInitScript_ContainsProfileValues()
    {
        var profile = FingerprintGenerator.Generate();
        var script = StealthScripts.GetInitScript(profile);

        script.Should().Contain(profile.Cores.ToString(System.Globalization.CultureInfo.InvariantCulture));
        script.Should().Contain(profile.MemoryGb.ToString(System.Globalization.CultureInfo.InvariantCulture));
        script.Should().Contain(profile.Platform);
        script.Should().Contain(profile.VideoCardVendor);
        script.Should().Contain(profile.VideoCardRenderer);
        
        // Check for specific spoofing techniques
        script.Should().Contain("Object.defineProperty(navigator,'webdriver',{get:()=>undefined});");
        script.Should().Contain("Object.defineProperty(navigator,'hardwareConcurrency'");
        script.Should().Contain("WebGLRenderingContext.prototype.getParameter");
    }
}
