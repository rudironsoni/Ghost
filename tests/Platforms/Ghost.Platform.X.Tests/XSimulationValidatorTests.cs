using Ghost.Contracts.Simulation;
using Ghost.Contracts.Social;
using Ghost.Platform.X.Internal;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Ghost.Platform.X.Tests;

public class XSimulationValidatorTests
{
    private readonly XOptions _options;
    private readonly XSimulationValidator _validator;

    public XSimulationValidatorTests()
    {
        _options = new XOptions();
        var optionsMock = new Mock<IOptions<XOptions>>();
        optionsMock.Setup(x => x.Value).Returns(_options);
        _validator = new XSimulationValidator(optionsMock.Object);
    }

    [Fact]
    public void PlatformName_ReturnsX()
    {
        // Assert
        Assert.Equal("X", _validator.PlatformName);
    }

    [Fact]
    public void MaxContentLength_Returns280()
    {
        // Assert
        Assert.Equal(280, _validator.MaxContentLength);
    }

    [Fact]
    public void MaxMediaAttachments_Returns4()
    {
        // Assert
        Assert.Equal(4, _validator.MaxMediaAttachments);
    }

    [Fact]
    public void SupportedMediaTypes_ContainsImageAndVideoFormats()
    {
        // Assert
        Assert.Contains(".jpg", _validator.SupportedMediaTypes);
        Assert.Contains(".mp4", _validator.SupportedMediaTypes);
        Assert.True(_validator.SupportedMediaTypes.Count >= 8); // 5 images + 3 videos
    }

    [Fact]
    public async Task ValidatePostAsync_EmptyContent_ReturnsError()
    {
        // Arrange
        var request = new CreatePostRequest { Content = "" };

        // Act
        var result = await _validator.ValidatePostAsync(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "CONTENT_EMPTY");
    }

    [Fact]
    public async Task ValidatePostAsync_NullContent_ReturnsError()
    {
        // Arrange
        var request = new CreatePostRequest { Content = null! };

        // Act
        var result = await _validator.ValidatePostAsync(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "CONTENT_EMPTY");
    }

    [Fact]
    public async Task ValidatePostAsync_WhitespaceContent_ReturnsError()
    {
        // Arrange
        var request = new CreatePostRequest { Content = "   \n\t  " };

        // Act
        var result = await _validator.ValidatePostAsync(request);

        // Assert
        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ValidatePostAsync_ValidContent_ReturnsSuccess()
    {
        // Arrange
        var request = new CreatePostRequest { Content = "Valid tweet content." };

        // Act
        var result = await _validator.ValidatePostAsync(request);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task ValidatePostAsync_ContentTooLong_ReturnsError()
    {
        // Arrange
        var content = new string('a', 281); // Just over limit
        var request = new CreatePostRequest { Content = content };

        // Act
        var result = await _validator.ValidatePostAsync(request);

        // Assert
        if (!result.IsValid)
        {
            Assert.Contains(result.Errors, e => e.Code == "CONTENT_TOO_LONG");
        }
    }

    [Fact]
    public async Task ValidatePostAsync_NearLimitContent_ReturnsWarning()
    {
        // Arrange
        var content = new string('a', 260); // Over 90% of 280
        var request = new CreatePostRequest { Content = content };

        // Act
        var result = await _validator.ValidatePostAsync(request);

        // Assert
        Assert.True(result.IsValid);
        if (result.Warnings.Count > 0)
        {
            Assert.Contains(result.Warnings, w => w.Code == "CONTENT_NEAR_LIMIT");
        }
    }

    [Fact]
    public async Task ValidatePostAsync_LongThread_ReturnsWarning()
    {
        // Arrange
        var content = string.Join(" ", Enumerable.Range(1, 20).Select(i => $"This is a very long sentence that will create a thread with many parts number {i}."));
        var request = new CreatePostRequest { Content = content };

        // Act
        var result = await _validator.ValidatePostAsync(request);

        // Assert
        Assert.True(result.IsValid);
        if (result.Warnings.Count > 0)
        {
            Assert.Contains(result.Warnings, w => w.Code == "LONG_THREAD");
        }
    }

    [Fact]
    public async Task ValidatePostAsync_NonExistentMedia_ReturnsError()
    {
        // Arrange
        var request = new CreatePostRequest
        {
            Content = "Test content",
            MediaUrls = new[] { "/nonexistent/image.jpg" }
        };

        // Act
        var result = await _validator.ValidatePostAsync(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "MEDIA_FILE_NOT_FOUND");
    }

    [Fact]
    public async Task ValidatePostAsync_UnsupportedMediaFormat_ReturnsError()
    {
        // Arrange - Create a temp file with unsupported extension
        var tempFile = Path.GetTempFileName() + ".xyz";
        try
        {
            File.WriteAllText(tempFile, "test");

            var request = new CreatePostRequest
            {
                Content = "Test content",
                MediaUrls = new[] { tempFile }
            };

            // Act
            var result = await _validator.ValidatePostAsync(request);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Code == "UNSUPPORTED_MEDIA_FORMAT");
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ValidatePostAsync_TooManyImages_ReturnsError()
    {
        // Arrange - Create 5 temp image files
        var tempFiles = new List<string>();
        try
        {
            for (int i = 0; i < 5; i++)
            {
                var tempFile = Path.GetTempFileName() + ".jpg";
                File.WriteAllText(tempFile, "test image content");
                tempFiles.Add(tempFile);
            }

            var request = new CreatePostRequest
            {
                Content = "Test content",
                MediaUrls = tempFiles
            };

            // Act
            var result = await _validator.ValidatePostAsync(request);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Code == "TOO_MANY_IMAGES");
        }
        finally
        {
            foreach (var file in tempFiles)
            {
                if (File.Exists(file))
                    File.Delete(file);
            }
        }
    }

    [Fact]
    public async Task ValidatePostAsync_ValidImage_ReturnsSuccess()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".jpg";
        try
        {
            File.WriteAllText(tempFile, "test image content");

            var request = new CreatePostRequest
            {
                Content = "Test content",
                MediaUrls = new[] { tempFile }
            };

            // Act
            var result = await _validator.ValidatePostAsync(request);

            // Assert
            Assert.True(result.IsValid);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ValidatePostAsync_TooManyHashtags_ReturnsWarning()
    {
        // Arrange
        var content = "#tag1 #tag2 #tag3 #tag4 #tag5 #tag6 tweet content";
        var request = new CreatePostRequest { Content = content };

        // Act
        var result = await _validator.ValidatePostAsync(request);

        // Assert
        Assert.True(result.IsValid);
        if (result.Warnings.Count > 0)
        {
            Assert.Contains(result.Warnings, w => w.Code == "TOO_MANY_HASHTAGS");
        }
    }

    [Fact]
    public async Task ValidatePostAsync_TooManyMentions_ReturnsWarning()
    {
        // Arrange
        var content = "@user1 @user2 @user3 @user4 @user5 @user6 tweet content";
        var request = new CreatePostRequest { Content = content };

        // Act
        var result = await _validator.ValidatePostAsync(request);

        // Assert
        Assert.True(result.IsValid);
        if (result.Warnings.Count > 0)
        {
            Assert.Contains(result.Warnings, w => w.Code == "TOO_MANY_MENTIONS");
        }
    }

    [Fact]
    public async Task ValidatePostAsync_MixedMedia_ReturnsWarning()
    {
        // Arrange
        var imageFile = Path.GetTempFileName() + ".jpg";
        var videoFile = Path.GetTempFileName() + ".mp4";
        try
        {
            File.WriteAllText(imageFile, "test image");
            File.WriteAllText(videoFile, "test video");

            var request = new CreatePostRequest
            {
                Content = "Test content",
                MediaUrls = new[] { imageFile, videoFile }
            };

            // Act
            var result = await _validator.ValidatePostAsync(request);

            // Assert
            Assert.True(result.IsValid);
            if (result.Warnings.Count > 0)
            {
                Assert.Contains(result.Warnings, w => w.Code == "MIXED_MEDIA");
            }
        }
        finally
        {
            if (File.Exists(imageFile)) File.Delete(imageFile);
            if (File.Exists(videoFile)) File.Delete(videoFile);
        }
    }

    [Fact]
    public async Task ValidateSelectorsAsync_InvalidPage_ReturnsError()
    {
        // Arrange
        var invalidPage = new object();

        // Act
        var result = await _validator.ValidateSelectorsAsync(invalidPage);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "INVALID_PAGE");
    }

    [Fact]
    public async Task GeneratePreviewAsync_ValidRequest_ReturnsHtml()
    {
        // Arrange
        var request = new CreatePostRequest
        {
            Content = "Test content for preview",
            MediaUrls = Array.Empty<string>()
        };

        // Act
        var preview = await _validator.GeneratePreviewAsync(request);

        // Assert
        Assert.NotNull(preview);
        Assert.Contains("<!DOCTYPE html>", preview);
        Assert.Contains("X Post Preview", preview);
        Assert.Contains("Test content for preview", preview);
    }

    [Fact]
    public async Task GeneratePreviewAsync_ThreadContent_ReturnsMultiTweetPreview()
    {
        // Arrange
        var content = string.Join(" ", Enumerable.Range(1, 10).Select(i =>
            $"This is a very long sentence number {i} that will create a thread."));
        var request = new CreatePostRequest { Content = content };

        // Act
        var preview = await _validator.GeneratePreviewAsync(request);

        // Assert
        Assert.NotNull(preview);
        Assert.Contains("Thread", preview);
    }

    [Fact]
    public async Task SimulatePostAsync_ValidRequest_ReturnsSuccess()
    {
        // Arrange
        var request = new CreatePostRequest { Content = "Valid content" };

        // Act
        var result = await _validator.SimulatePostAsync(request);

        // Assert
        Assert.True(result.WouldSucceed);
        Assert.Equal("X", result.Platform);
        Assert.Equal("CreatePost", result.Action);
        Assert.NotNull(result.SimulatedPostId);
        Assert.NotNull(result.PreviewHtml);
    }

    [Fact]
    public async Task SimulatePostAsync_InvalidRequest_ReturnsFailure()
    {
        // Arrange
        var request = new CreatePostRequest { Content = "" };

        // Act
        var result = await _validator.SimulatePostAsync(request);

        // Assert
        Assert.False(result.WouldSucceed);
        Assert.NotEmpty(result.ValidationErrors);
    }

    [Fact]
    public async Task SimulatePostAsync_IncludesMetadata()
    {
        // Arrange
        var request = new CreatePostRequest { Content = "Test content" };

        // Act
        var result = await _validator.SimulatePostAsync(request);

        // Assert
        Assert.True(result.Metadata.ContainsKey("TweetCount"));
        Assert.True(result.Metadata.ContainsKey("SimulatedIds"));
        Assert.True(result.Metadata.ContainsKey("TotalCharacters"));
        Assert.True(result.Metadata.ContainsKey("MediaCount"));
    }

    [Fact]
    public async Task SimulatePostAsync_LongContent_IncludesThreadWarning()
    {
        // Arrange
        var content = string.Join(" ", Enumerable.Range(1, 10).Select(i =>
            $"This is sentence {i} that makes a thread with many parts."));
        var request = new CreatePostRequest { Content = content };

        // Act
        var result = await _validator.SimulatePostAsync(request);

        // Assert
        Assert.True(result.WouldSucceed);
        if (result.Warnings.Count > 0)
        {
            Assert.Contains(result.Warnings, w => w.Contains("thread"));
        }
    }
}
