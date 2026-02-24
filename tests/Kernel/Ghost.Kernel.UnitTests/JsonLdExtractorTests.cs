using System.Text.Json;
using Ghost.Testing.Reliability;
using Ghost.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Kernel.Tests;

public class JsonLdExtractorTests : ReliabilityTestBase
{
    public JsonLdExtractorTests(ITestOutputHelper output) : base(output) { }

    private readonly JsonLdExtractor _ext = new();

    [Fact]
    public void ExtractRawReturnsElements()
    {
        var html = "<html><head><script type=\"application/ld+json\">{ \"@type\": \"Person\", \"name\": \"Alice\" }</script></head></html>";
        var items = _ext.ExtractRaw(html).ToList();
        Assert.Single(items);
        Assert.Equal("Person", items[0].GetProperty("@type").GetString());
    }

    [Fact]
    public void ExtractTyped()
    {
        var html = "<script type=\"application/ld+json\">[{ \"@type\": \"Thing\", \"id\": 1 }]</script>";
        var objs = _ext.Extract<JsonElement>(html).ToList();
        Assert.Single(objs);
        Assert.Equal("Thing", objs[0].GetProperty("@type").GetString());
    }
}
