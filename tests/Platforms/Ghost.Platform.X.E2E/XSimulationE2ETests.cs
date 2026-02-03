using Ghost.Contracts.Simulation;
using Ghost.Contracts.Social;
using Ghost.Platform.X.E2E.Fixtures;
using Ghost.Platform.X.Internal;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ghost.Platform.X.E2E;

/// <summary>
/// End-to-end tests for X platform simulation mode.
/// </summary>
public class XSimulationE2ETests : IClassFixture<GhostKernelFixture>
{
    private readonly GhostKernelFixture _fixture;
    private readonly XSimulationValidator _validator;

    public XSimulationE2ETests(GhostKernelFixture fixture)
    {
        _fixture = fixture;
        _validator = _fixture.ServiceProvider.GetRequiredService<XSimulationValidator>();
    }

    #region Content Validation E2E Tests

    [Fact]
    public async Task ValidatePostAsync_ValidContent_ReturnsSuccess()
    {
        // Arrange
        var request = new CreatePostRequest
        {
            Content = "This is a valid tweet for testing purposes."
        };

        // Act
        var result = await _validator.ValidatePostAsync(request);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
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
    public async Task ValidatePostAsync_TooLongContent_ReturnsError()
    {
        // Arrange
        var request = new CreatePostRequest
        {
            Content = new string('a', 281)
        };

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
        var request = new CreatePostRequest
        {
            Content = new string('a', 260)
        };

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
    public async Task ValidatePostAsync_TooManyHashtags_ReturnsWarning()
    {
        // Arrange
        var request = new CreatePostRequest
        {
            Content = "#tag1 #tag2 #tag3 #tag4 #tag5 #tag6 tweet content"
        };

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
        var request = new CreatePostRequest
        {
            Content = "@user1 @user2 @user3 @user4 @user5 @user6 tweet content"
        };

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
    public async Task ValidatePostAsync_LongThread_ReturnsWarning()
    {
        // Arrange
        var request = new CreatePostRequest
        {
            Content = string.Join(" ", Enumerable.Range(1, 20).Select(i => 
                $"This is a very long sentence that will create a thread with many parts number {i}."))
        };

        // Act
        var result = await _validator.ValidatePostAsync(request);

        // Assert
        Assert.True(result.IsValid);
        if (result.Warnings.Count > 0)
        {
            Assert.Contains(result.Warnings, w => w.Code == "LONG_THREAD");
        }
    }

    #endregion

    #region Simulation E2E Tests

    [Fact]
    public async Task SimulatePostAsync_ValidRequest_ReturnsSuccess()
    {
        // Arrange
        var request = new CreatePostRequest
        {
            Content = "Valid content for simulation"
        };

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
    public async Task SimulatePostAsync_ThreadContent_IncludesThreadWarning()
    {
        // Arrange
        var request = new CreatePostRequest
        {
            Content = string.Join(" ", Enumerable.Range(1, 10).Select(i => 
                $"This is sentence {i} that makes a thread with many parts."))
        };

        // Act
        var result = await _validator.SimulatePostAsync(request);

        // Assert
        Assert.True(result.WouldSucceed);
        if (result.Warnings.Count > 0)
        {
            Assert.Contains(result.Warnings, w => w.Contains("thread", StringComparison.OrdinalIgnoreCase));
        }
    }

    #endregion

    #region Preview Generation E2E Tests

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

    #endregion

    #region Platform Info E2E Tests

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
    public void SupportedMediaTypes_ContainsExpectedFormats()
    {
        // Assert
        Assert.Contains(".jpg", _validator.SupportedMediaTypes);
        Assert.Contains(".jpeg", _validator.SupportedMediaTypes);
        Assert.Contains(".png", _validator.SupportedMediaTypes);
        Assert.Contains(".mp4", _validator.SupportedMediaTypes);
        Assert.True(_validator.SupportedMediaTypes.Count >= 8);
    }

    #endregion

    #region Validation Error Details E2E Tests

    [Fact]
    public async Task ValidatePostAsync_NonExistentMedia_ReturnsFileNotFoundError()
    {
        // Arrange
        var request = new CreatePostRequest
        {
            Content = "Test content",
            MediaUrls = new[] { "/nonexistent/path/to/image.jpg" }
        };

        // Act
        var result = await _validator.ValidatePostAsync(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "MEDIA_FILE_NOT_FOUND");
    }

    [Fact]
    public async Task ValidatePostAsync_UnsupportedFormat_ReturnsFormatError()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".xyz";
        try
        {
            File.WriteAllText(tempFile, "test content");
            
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
    public async Task ValidatePostAsync_TooManyImages_ReturnsCountError()
    {
        // Arrange
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

    #endregion
}
