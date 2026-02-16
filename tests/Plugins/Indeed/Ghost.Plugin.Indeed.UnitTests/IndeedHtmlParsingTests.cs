using System.Diagnostics;
using Ghost.Plugin.Indeed.Internal;
using Xunit;

namespace Ghost.Plugin.Indeed.Tests;

public class IndeedHtmlParsingTests
{
    [Theory]
    [InlineData("<p>Simple text</p>", "Simple text")]
    [InlineData("<div><p>Nested tags</p></div>", "Nested tags")]
    [InlineData("Text <b>bold</b> and <i>italic</i>", "Text bold and italic")]
    [InlineData("&amp; &lt; &gt; &quot; &#39;", "& < > \" '")]
    [InlineData("<script>alert('xss')</script>Safe", "Safe")]
    [InlineData("<style>body{}</style>Content", "Content")]
    [InlineData("Multiple<br>Line<br/>Breaks", "Multiple\nLine\nBreaks")]
    [InlineData("  Extra  spaces  ", "Extra spaces")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void StripHtmlTagsRemovesTagsAndNormalizes(string? input, string expected)
    {
        string result = HtmlSanitizer.StripHtmlTags(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("<p>Line1</p><p>Line2</p>", "Line1\n\nLine2")]
    [InlineData("<ul><li>One</li><li>Two</li></ul>", "One\nTwo")]
    [InlineData("<h1>Title</h1><div>Body</div>", "Title\n\nBody")]
    [InlineData("A&nbsp;B", "A B")]
    [InlineData("Text&nbsp;&nbsp;with&nbsp;spaces", "Text with spaces")]
    [InlineData("<div>Keep <span>inline</span> text</div>", "Keep inline text")]
    [InlineData("<script>var x = 1;</script><style>h1{}</style>Visible", "Visible")]
    [InlineData("<div>Mixed<br />breaks</div>", "Mixed\nbreaks")]
    [InlineData("<p>Trailing<br></p>", "Trailing")]
    public void StripHtmlTagsHandlesCommonPatterns(string input, string expected)
    {
        string result = HtmlSanitizer.StripHtmlTags(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("&amp;", "&")]
    [InlineData("&lt;div&gt;", "<div>")]
    [InlineData("&#39;", "'")]
    public void DecodeHtmlEntitiesUsesHtmlDecode(string input, string expected)
    {
        string result = HtmlSanitizer.DecodeHtmlEntities(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void StripHtmlTagsPerformsUnderOneMillisecondOnAverage()
    {
        string html = "<div><p>Job &amp; description</p><ul><li>One</li><li>Two</li></ul><script>bad()</script></div>";

        for (int i = 0; i < 10; i++)
            _ = HtmlSanitizer.StripHtmlTags(html);

        var stopwatch = Stopwatch.StartNew();
        int iterations = 1000;
        string? last = null;
        for (int i = 0; i < iterations; i++)
            last = HtmlSanitizer.StripHtmlTags(html);
        stopwatch.Stop();

        Assert.Equal("Job & description\n\nOne\nTwo", last);
        double averageMs = stopwatch.Elapsed.TotalMilliseconds / iterations;
        Assert.True(averageMs < 1, $"Expected < 1ms avg but was {averageMs:F4}ms");
    }
}
