using Xunit;
using Ghost.Platform.Google.Jobs.Internal;

namespace Ghost.Platform.Google.Tests;

public class GoogleJobsParserTests
{
    [Fact]
    public void ParsesSampleHtml()
    {
        var sample = "[ [\"Title\", \"Company\", \"Location\", null, null, null, null, null, null, null, null, \"id-123\", null, null, null, null, null, null, null, \"Description text\"] ]";
        var html = $"... {GoogleJobsConstants.WidgetKey} ... {sample} ...";

        var outp = GoogleJobsParser.ParseFromHtml(html);
        Assert.Single(outp);
        Assert.Equal("Title", outp[0].Title);
        Assert.Equal("Company", outp[0].Company);
        Assert.Equal("Location", outp[0].Location);
        Assert.Equal("Description text", outp[0].Description);
    }
}
