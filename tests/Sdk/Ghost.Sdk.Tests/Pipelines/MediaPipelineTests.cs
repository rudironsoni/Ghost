using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Ghost.Sdk.Pipelines;
using Moq;
using Moq.Protected;
using Xunit;

namespace Ghost.Sdk.Tests.Pipelines;

[Trait("Category", "Unit")]
public class MediaPipelineTests : IDisposable
{
    private readonly string _testOutputPath;

    public MediaPipelineTests()
    {
        _testOutputPath = Path.Combine(Path.GetTempPath(), $"ghost_media_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testOutputPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testOutputPath))
        {
            Directory.Delete(_testOutputPath, recursive: true);
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Constructor_WithNullHttpClient_ThrowsArgumentNullException()
    {
        // Arrange
        var options = new MediaPipelineOptions();

        // Act
        var act = () => new MediaPipeline(null!, options);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("httpClient");
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var httpClient = new HttpClient();

        // Act
        var act = () => new MediaPipeline(httpClient, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("options");
    }

    [Fact]
    public async Task ProcessAsync_WithNullRequest_ThrowsArgumentNullException()
    {
        // Arrange
        var httpClient = new HttpClient();
        var options = new MediaPipelineOptions();
        var pipeline = new MediaPipeline(httpClient, options);

        // Act
        var act = async () => await pipeline.ProcessAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("request");
    }

    [Fact]
    public async Task ProcessAsync_WithEmptyUrl_ThrowsArgumentException()
    {
        // Arrange
        var httpClient = new HttpClient();
        var options = new MediaPipelineOptions();
        var pipeline = new MediaPipeline(httpClient, options);
        var request = new MediaRequest { Url = string.Empty };

        // Act
        var act = async () => await pipeline.ProcessAsync(request);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*URL*");
    }

    [Fact]
    public async Task ProcessAsync_WithValidRequest_DownloadsFile()
    {
        // Arrange
        var testContent = "Test file content"u8.ToArray();
        var mockHandler = CreateMockHttpHandler(HttpStatusCode.OK, testContent, "text/plain");
        var httpClient = new HttpClient(mockHandler.Object);
        var options = new MediaPipelineOptions();
        var pipeline = new MediaPipeline(httpClient, options);
        var request = new MediaRequest
        {
            Url = "https://example.com/test.txt",
            OutputPath = _testOutputPath
        };

        // Act
        var result = await pipeline.ProcessAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Url.Should().Be(request.Url);
        result.LocalPath.Should().NotBeNullOrEmpty();
        result.Size.Should().Be(testContent.Length);
        result.ContentType.Should().Be("text/plain");
        File.Exists(result.LocalPath).Should().BeTrue();

        var downloadedContent = await File.ReadAllBytesAsync(result.LocalPath);
        downloadedContent.Should().BeEquivalentTo(testContent);
    }

    [Fact]
    public async Task ProcessAsync_WithChecksumEnabled_CalculatesChecksum()
    {
        // Arrange
        var testContent = "Test file content"u8.ToArray();
        var mockHandler = CreateMockHttpHandler(HttpStatusCode.OK, testContent, "text/plain");
        var httpClient = new HttpClient(mockHandler.Object);
        var options = new MediaPipelineOptions { CalculateChecksum = true };
        var pipeline = new MediaPipeline(httpClient, options);
        var request = new MediaRequest
        {
            Url = "https://example.com/test.txt",
            OutputPath = _testOutputPath
        };

        // Act
        var result = await pipeline.ProcessAsync(request);

        // Assert
        result.Checksum.Should().NotBeNullOrEmpty();
        result.Checksum.Should().HaveLength(64); // SHA256 produces 64 hex characters
    }

    [Fact]
    public async Task ProcessAsync_WithChecksumDisabled_DoesNotCalculateChecksum()
    {
        // Arrange
        var testContent = "Test file content"u8.ToArray();
        var mockHandler = CreateMockHttpHandler(HttpStatusCode.OK, testContent, "text/plain");
        var httpClient = new HttpClient(mockHandler.Object);
        var options = new MediaPipelineOptions { CalculateChecksum = false };
        var pipeline = new MediaPipeline(httpClient, options);
        var request = new MediaRequest
        {
            Url = "https://example.com/test.txt",
            OutputPath = _testOutputPath
        };

        // Act
        var result = await pipeline.ProcessAsync(request);

        // Assert
        result.Checksum.Should().BeNull();
    }

    [Fact]
    public async Task ProcessAsync_WithCustomFileName_UsesProvidedName()
    {
        // Arrange
        var testContent = "Test file content"u8.ToArray();
        var mockHandler = CreateMockHttpHandler(HttpStatusCode.OK, testContent, "text/plain");
        var httpClient = new HttpClient(mockHandler.Object);
        var options = new MediaPipelineOptions();
        var pipeline = new MediaPipeline(httpClient, options);
        var customFileName = "custom_name.txt";
        var request = new MediaRequest
        {
            Url = "https://example.com/original.txt",
            FileName = customFileName,
            OutputPath = _testOutputPath
        };

        // Act
        var result = await pipeline.ProcessAsync(request);

        // Assert
        Path.GetFileName(result.LocalPath).Should().Be(customFileName);
    }

    [Fact]
    public async Task ProcessAsync_WithoutFileName_ExtractsFromUrl()
    {
        // Arrange
        var testContent = "Test file content"u8.ToArray();
        var mockHandler = CreateMockHttpHandler(HttpStatusCode.OK, testContent, "text/plain");
        var httpClient = new HttpClient(mockHandler.Object);
        var options = new MediaPipelineOptions();
        var pipeline = new MediaPipeline(httpClient, options);
        var request = new MediaRequest
        {
            Url = "https://example.com/image.jpg",
            OutputPath = _testOutputPath
        };

        // Act
        var result = await pipeline.ProcessAsync(request);

        // Assert
        Path.GetFileName(result.LocalPath).Should().Be("image.jpg");
    }

    [Fact]
    public async Task ProcessAsync_WithFileSizeExceedingLimit_ThrowsInvalidOperationException()
    {
        // Arrange
        var testContent = new byte[1024 * 1024]; // 1MB
        var mockHandler = CreateMockHttpHandler(HttpStatusCode.OK, testContent, "application/octet-stream", testContent.Length);
        var httpClient = new HttpClient(mockHandler.Object);
        var options = new MediaPipelineOptions { MaxFileSize = 512 * 1024 }; // 512KB limit
        var pipeline = new MediaPipeline(httpClient, options);
        var request = new MediaRequest
        {
            Url = "https://example.com/large.bin",
            OutputPath = _testOutputPath
        };

        // Act
        var act = async () => await pipeline.ProcessAsync(request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*exceeds maximum*");
    }

    [Fact]
    public async Task ProcessAsync_WithAllowedExtensions_AllowsMatchingExtension()
    {
        // Arrange
        var testContent = "Test file content"u8.ToArray();
        var mockHandler = CreateMockHttpHandler(HttpStatusCode.OK, testContent, "image/jpeg");
        var httpClient = new HttpClient(mockHandler.Object);
        var options = new MediaPipelineOptions
        {
            AllowedExtensions = new() { ".jpg", ".jpeg", ".png" }
        };
        var pipeline = new MediaPipeline(httpClient, options);
        var request = new MediaRequest
        {
            Url = "https://example.com/image.jpg",
            OutputPath = _testOutputPath
        };

        // Act
        var result = await pipeline.ProcessAsync(request);

        // Assert
        result.Should().NotBeNull();
        File.Exists(result.LocalPath).Should().BeTrue();
    }

    [Fact]
    public async Task ProcessAsync_WithAllowedExtensions_RejectsNonMatchingExtension()
    {
        // Arrange
        var testContent = "Test file content"u8.ToArray();
        var mockHandler = CreateMockHttpHandler(HttpStatusCode.OK, testContent, "application/octet-stream");
        var httpClient = new HttpClient(mockHandler.Object);
        var options = new MediaPipelineOptions
        {
            AllowedExtensions = new() { ".jpg", ".jpeg", ".png" }
        };
        var pipeline = new MediaPipeline(httpClient, options);
        var request = new MediaRequest
        {
            Url = "https://example.com/file.pdf",
            OutputPath = _testOutputPath
        };

        // Act
        var act = async () => await pipeline.ProcessAsync(request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*extension*.pdf*not allowed*");
    }

    [Fact]
    public async Task ProcessAsync_WithEmptyAllowedExtensions_AllowsAnyExtension()
    {
        // Arrange
        var testContent = "Test file content"u8.ToArray();
        var mockHandler = CreateMockHttpHandler(HttpStatusCode.OK, testContent, "application/pdf");
        var httpClient = new HttpClient(mockHandler.Object);
        var options = new MediaPipelineOptions
        {
            AllowedExtensions = new() // Empty list
        };
        var pipeline = new MediaPipeline(httpClient, options);
        var request = new MediaRequest
        {
            Url = "https://example.com/document.pdf",
            OutputPath = _testOutputPath
        };

        // Act
        var result = await pipeline.ProcessAsync(request);

        // Assert
        result.Should().NotBeNull();
        File.Exists(result.LocalPath).Should().BeTrue();
    }

    [Fact]
    public async Task ProcessAsync_CreatesOutputDirectory_IfNotExists()
    {
        // Arrange
        var testContent = "Test file content"u8.ToArray();
        var mockHandler = CreateMockHttpHandler(HttpStatusCode.OK, testContent, "text/plain");
        var httpClient = new HttpClient(mockHandler.Object);
        var options = new MediaPipelineOptions();
        var pipeline = new MediaPipeline(httpClient, options);
        var nestedPath = Path.Combine(_testOutputPath, "nested", "directory");
        var request = new MediaRequest
        {
            Url = "https://example.com/test.txt",
            OutputPath = nestedPath
        };

        // Act
        var result = await pipeline.ProcessAsync(request);

        // Assert
        Directory.Exists(nestedPath).Should().BeTrue();
        File.Exists(result.LocalPath).Should().BeTrue();
    }

    [Fact]
    public async Task ProcessAsync_WithFailedHttpRequest_ThrowsHttpRequestException()
    {
        // Arrange
        var mockHandler = CreateMockHttpHandler(HttpStatusCode.NotFound, Array.Empty<byte>(), "text/plain");
        var httpClient = new HttpClient(mockHandler.Object);
        var options = new MediaPipelineOptions();
        var pipeline = new MediaPipeline(httpClient, options);
        var request = new MediaRequest
        {
            Url = "https://example.com/notfound.txt",
            OutputPath = _testOutputPath
        };

        // Act
        var act = async () => await pipeline.ProcessAsync(request);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task ProcessAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();
        var mockHandler = CreateMockHttpHandler(HttpStatusCode.OK, "Test"u8.ToArray(), "text/plain");
        var httpClient = new HttpClient(mockHandler.Object);
        var options = new MediaPipelineOptions();
        var pipeline = new MediaPipeline(httpClient, options);
        var request = new MediaRequest
        {
            Url = "https://example.com/test.txt",
            OutputPath = _testOutputPath
        };

        // Act
        var act = async () => await pipeline.ProcessAsync(request, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ProcessAsync_WithUrlWithoutFileName_UsesDefaultName()
    {
        // Arrange
        var testContent = "Test file content"u8.ToArray();
        var mockHandler = CreateMockHttpHandler(HttpStatusCode.OK, testContent, "text/html");
        var httpClient = new HttpClient(mockHandler.Object);
        var options = new MediaPipelineOptions();
        var pipeline = new MediaPipeline(httpClient, options);
        var request = new MediaRequest
        {
            Url = "https://example.com/",
            OutputPath = _testOutputPath
        };

        // Act
        var result = await pipeline.ProcessAsync(request);

        // Assert
        Path.GetFileName(result.LocalPath).Should().Be("download");
    }

    [Fact]
    public async Task ProcessAsync_WithNoContentType_UsesDefaultOctetStream()
    {
        // Arrange
        var testContent = "Test file content"u8.ToArray();
        var mockHandler = CreateMockHttpHandler(HttpStatusCode.OK, testContent, contentType: null);
        var httpClient = new HttpClient(mockHandler.Object);
        var options = new MediaPipelineOptions();
        var pipeline = new MediaPipeline(httpClient, options);
        var request = new MediaRequest
        {
            Url = "https://example.com/test.bin",
            OutputPath = _testOutputPath
        };

        // Act
        var result = await pipeline.ProcessAsync(request);

        // Assert
        result.ContentType.Should().Be("application/octet-stream");
    }

    [Fact]
    public async Task ProcessAsync_MultipleFiles_DownloadsAllSuccessfully()
    {
        // Arrange
        var testContent = "Test file content"u8.ToArray();
        var mockHandler = CreateMockHttpHandler(HttpStatusCode.OK, testContent, "text/plain");
        var httpClient = new HttpClient(mockHandler.Object);
        var options = new MediaPipelineOptions();
        var pipeline = new MediaPipeline(httpClient, options);

        // Act
        var results = new MediaItem[3];
        for (var i = 0; i < 3; i++)
        {
            var request = new MediaRequest
            {
                Url = $"https://example.com/file{i}.txt",
                OutputPath = _testOutputPath
            };
            results[i] = await pipeline.ProcessAsync(request);
        }

        // Assert
        results.Should().AllSatisfy(r =>
        {
            r.Should().NotBeNull();
            File.Exists(r.LocalPath).Should().BeTrue();
        });
    }

    private static Mock<HttpMessageHandler> CreateMockHttpHandler(
        HttpStatusCode statusCode,
        byte[] content,
        string? contentType,
        long? contentLength = null)
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        var httpContent = new ByteArrayContent(content);

        if (!string.IsNullOrEmpty(contentType))
        {
            httpContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        }

        if (contentLength.HasValue)
        {
            httpContent.Headers.ContentLength = contentLength.Value;
        }

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = httpContent
            });

        return mockHandler;
    }
}
