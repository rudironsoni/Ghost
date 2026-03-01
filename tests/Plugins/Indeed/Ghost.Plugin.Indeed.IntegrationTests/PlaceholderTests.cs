using Ghost.Testing.Reliability;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Plugin.Indeed.IntegrationTests;

public class PlaceholderTests : ReliabilityTestBase
{
    public PlaceholderTests(ITestOutputHelper output) : base(output) { }
    [Fact]
    public void Project_Loads_Successfully()
    {
        Assert.True(true);
    }
}
