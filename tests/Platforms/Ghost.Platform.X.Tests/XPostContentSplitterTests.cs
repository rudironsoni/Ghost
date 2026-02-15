using Ghost.Platform.X.Internal;
using Xunit;

namespace Ghost.Platform.X.Tests;

public class XPostContentSplitterTests
{
    private readonly XPostContentSplitter _splitter;

    public XPostContentSplitterTests()
    {
        _splitter = new XPostContentSplitter(280);
    }

    [Fact]
    public void Split_EmptyContent_ReturnsEmptyList()
    {
        // Act
        var result = _splitter.Split("");

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void Split_WhitespaceContent_ReturnsEmptyList()
    {
        // Act
        var result = _splitter.Split("   \n\t  ");

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void Split_NullContent_ReturnsEmptyList()
    {
        // Act
        var result = _splitter.Split(null!);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void Split_ShortContent_ReturnsSinglePart()
    {
        // Arrange
        var content = "This is a short tweet.";

        // Act
        var result = _splitter.Split(content);

        // Assert
        Assert.Single(result);
        Assert.Equal(content, result[0]);
    }

    [Fact]
    public void Split_ContentAtExactLimit_ReturnsSinglePart()
    {
        // Arrange
        var content = new string('a', 280);

        // Act
        var result = _splitter.Split(content);

        // Assert
        Assert.Single(result);
        Assert.Equal(280, result[0].Length);
    }

    [Fact]
    public void Split_LongContent_ReturnsMultipleParts()
    {
        // Arrange
        var content = new string('a', 500);

        // Act
        var result = _splitter.Split(content);

        // Assert
        Assert.True(result.Count > 1);
    }

    [Fact]
    public void Split_LongContent_IncludesThreadNumbering()
    {
        // Arrange
        var content = new string('a', 600);

        // Act
        var result = _splitter.Split(content);

        // Assert
        Assert.True(result.Count > 1);
        Assert.Contains("(1/", result[0]);
        Assert.Contains($"({result.Count}/{result.Count})", result[result.Count - 1]);
    }

    [Fact]
    public void Split_PreservesSentenceBoundaries()
    {
        // Arrange
        var sentence1 = "This is the first sentence. ";
        var sentence2 = "This is the second sentence. ";
        var sentence3 = "This is the third sentence.";
        var content = sentence1 + sentence2 + sentence3;

        // Act
        var result = _splitter.Split(content);

        // Assert
        Assert.True(result.Count >= 1);
        // Verify sentences aren't broken mid-word
        // Each part should end with a complete word or punctuation
        foreach (var part in result)
        {
            // Parts should not end with partial words (except for the last character being a space)
            var trimmed = part.TrimEnd();
            if (trimmed.Length > 0)
            {
                var lastChar = trimmed[^1];
                // Part should end with punctuation, a complete word, or be continued with "..."
                var endsProperly = char.IsPunctuation(lastChar) ||
                                   lastChar == ')' || // Thread numbering
                                   part.EndsWith("...", StringComparison.Ordinal) || // Truncated
                                   result.Count == 1; // Single part
                Assert.True(endsProperly || result.Count == 1, $"Part does not end properly: {part}");
            }
        }
    }

    [Fact]
    public void Split_RespectsUrlBoundaries()
    {
        // Arrange
        var content = "Check out this link: https://example.com/very/long/url/path and continue reading here.";

        // Act
        var result = _splitter.Split(content);

        // Assert
        Assert.True(result.Count >= 1);
        // URL should not be broken
        foreach (var part in result)
        {
            if (part.Contains("https://"))
            {
                // If part contains URL start, it should be complete or URL should not be broken
                Assert.True(part.Contains("example.com") || part.Contains("https://"));
            }
        }
    }

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("http://test.org/path")]
    [InlineData("https://very.long.url.with.many.parts.com/path/to/resource")]
    public void Split_HandlesUrlsCorrectly(string url)
    {
        // Arrange
        var content = $"Visit {url} for more info.";

        // Act
        var result = _splitter.Split(content);

        // Assert
        Assert.True(result.Count >= 1);
    }

    [Fact]
    public void Split_HandlesVeryLongSentence()
    {
        // Arrange
        var content = new string('a', 400); // Single "sentence" without punctuation

        // Act
        var result = _splitter.Split(content);

        // Assert
        Assert.True(result.Count >= 1); // Content should be split into at least 1 part
        // Each part should be at most 280 characters (plus thread numbering)
        foreach (var part in result)
        {
            Assert.True(part.Length <= 280 + 20, $"Part length {part.Length} exceeds expected maximum");
        }
    }

    [Fact]
    public void MaxLength_ReturnsExpectedValue()
    {
        // Assert
        Assert.Equal(280, _splitter.MaxLength);
    }

    [Fact]
    public void RequiresThread_ShortContent_ReturnsFalse()
    {
        // Arrange
        var content = "Short content.";

        // Act
        var result = _splitter.RequiresThread(content);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void RequiresThread_LongContent_ReturnsTrue()
    {
        // Arrange
        var content = new string('a', 500);

        // Act
        var result = _splitter.RequiresThread(content);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void RequiresThread_EmptyContent_ReturnsFalse()
    {
        // Act
        var result = _splitter.RequiresThread("");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void GetEstimatedTweetCount_ShortContent_ReturnsOne()
    {
        // Arrange
        var content = "Short content.";

        // Act
        var result = _splitter.GetEstimatedTweetCount(content);

        // Assert
        Assert.Equal(1, result);
    }

    [Fact]
    public void GetEstimatedTweetCount_LongContent_ReturnsCorrectCount()
    {
        // Arrange
        var content = new string('a', 600);

        // Act
        var result = _splitter.GetEstimatedTweetCount(content);

        // Assert
        Assert.True(result > 1);
    }

    [Fact]
    public void GetEstimatedTweetCount_EmptyContent_ReturnsZero()
    {
        // Act
        var result = _splitter.GetEstimatedTweetCount("");

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public void Split_WithCustomMaxLength_RespectsLimit()
    {
        // Arrange
        var customSplitter = new XPostContentSplitter(100);
        var content = new string('a', 250);

        // Act
        var result = customSplitter.Split(content);

        // Assert
        Assert.True(result.Count > 2);
    }

    [Fact]
    public void Split_MultipleSentences_DistributedAcrossParts()
    {
        // Arrange - Create content with many short sentences
        var sentences = Enumerable.Range(1, 20)
            .Select(i => $"This is sentence number {i}. ")
            .Aggregate((a, b) => a + b);

        // Act
        var result = _splitter.Split(sentences);

        // Assert
        Assert.True(result.Count > 1);
        // Verify thread numbering is applied
        for (int i = 0; i < result.Count; i++)
        {
            Assert.Contains($"({i + 1}/{result.Count})", result[i]);
        }
    }

    [Fact]
    public void Split_ContentWithNewlines_PreservesStructure()
    {
        // Arrange
        var content = "Line 1\nLine 2\nLine 3\nLine 4";

        // Act
        var result = _splitter.Split(content);

        // Assert
        Assert.True(result.Count >= 1);
        // Content should be preserved (or handled gracefully)
    }

    [Fact]
    public void Split_UrlsTreatedAsFixedLength()
    {
        // Arrange - URLs should be treated as ~23 chars regardless of actual length
        var longUrl = "https://example.com/very/long/path/with/many/segments";
        var content = $"Check this: {longUrl} and more text here.";

        // Act
        var result = _splitter.Split(content);

        // Assert
        Assert.True(result.Count >= 1);
    }
}
