using FluentAssertions;
using Ghost.Stealth;
using Xunit;

namespace Ghost.Tests.Stealth;

public class StealthScriptsTests
{
    [Fact]
    public void GetInitScriptContainsProfileValues()
    {
        var profile = FingerprintGenerator.Generate();
        var script = StealthScripts.GetInitScript(profile);

        script.Should().Contain(profile.Cores.ToString(System.Globalization.CultureInfo.InvariantCulture));
        script.Should().Contain(profile.MemoryGb.ToString(System.Globalization.CultureInfo.InvariantCulture));
        script.Should().Contain(profile.Platform);
        script.Should().Contain(profile.VideoCardVendor);
        script.Should().Contain(profile.VideoCardRenderer);

        // Check for specific spoofing techniques
        script.Should().Contain("Object.defineProperty(navigator,'webdriver',{");
        script.Should().Contain("Object.defineProperty(navigator,'hardwareConcurrency'");
        script.Should().Contain("WebGLRenderingContext.prototype.getParameter");
    }

    [Fact]
    public void GetCanvasNoiseScriptContainsExpectedOverrides()
    {
        var script = StealthScripts.GetCanvasNoiseScript();

        script.Should().Contain("CanvasRenderingContext2D.prototype.getImageData");
        script.Should().Contain("HTMLCanvasElement.prototype.toDataURL");
        script.Should().Contain("noise"); // Check for the noise logic variable or comment
    }
}
