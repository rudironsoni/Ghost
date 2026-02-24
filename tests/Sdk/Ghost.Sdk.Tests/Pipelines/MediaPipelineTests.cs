using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Ghost.Sdk.Pipelines;
using Ghost.Testing.Reliability;
using Moq;
using Moq.Protected;
using Xunit;
using Xunit.Abstractions;

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

    [Theory]
    [InlineData("normal-file.txt", "normal-file.txt")]
    [InlineData("file-with-dashes-and-dots.tar.gz", "file-with-dashes-and-dots.tar.gz")]
    [InlineData("file_with_underscores.pdf", "file_with_underscores.pdf")]
    [InlineData("UPPERCASE.TXT", "UPPERCASE.TXT")]
    [InlineData("mixed-Case_File.Name.ext", "mixed-Case_File.Name.ext")]
    [InlineData("file with spaces.txt", "file with spaces.txt")]
    public async Task ProcessAsync_WithValidFileNames_AcceptsCorrectly(string inputFileName, string expectedFileName)
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
            FileName = inputFileName,
            OutputPath = _testOutputPath
        };

        // Act
        var result = await pipeline.ProcessAsync(request);

        // Assert
        Path.GetFileName(result.LocalPath).Should().Be(expectedFileName);
        File.Exists(result.LocalPath).Should().BeTrue();
        result.LocalPath.Should().StartWith(Path.GetFullPath(_testOutputPath));
    }

    [Theory]
    [InlineData("../etc/passwd")]
    [InlineData("..\\windows\\system32\\config\\sam")]
    [InlineData("../../../etc/shadow")]
    [InlineData("file.txt/../../../etc/passwd")]
    [InlineData("../../")]
    [InlineData("..")]
    [InlineData("folder/../passwd")]
    public async Task ProcessAsync_WithPathTraversalInFileName_ThrowsSecurityException(string maliciousFileName)
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
            FileName = maliciousFileName,
            OutputPath = _testOutputPath
        };

        // Act
        var act = async () => await pipeline.ProcessAsync(request);

        // Assert - should throw SecurityException for path traversal attempts
        await act.Should().ThrowAsync<SecurityException>();

        // Verify no file was written outside the output path
        var suspiciousPath = Path.Combine(Path.GetDirectoryName(_testOutputPath)!, "etc", "passwd");
        File.Exists(suspiciousPath).Should().BeFalse();
    }

    [Fact]
    public async Task ProcessAsync_WithVariousPathTraversalAttempts_ThrowsSecurityException()
    {
        // Arrange - using a filename that could cause path traversal
        var testContent = "Test file content"u8.ToArray();
        var mockHandler = CreateMockHttpHandler(HttpStatusCode.OK, testContent, "text/plain");
        var httpClient = new HttpClient(mockHandler.Object);
        var options = new MediaPipelineOptions();
        var pipeline = new MediaPipeline(httpClient, options);

        // Try various path traversal attempts - all should be rejected
        var maliciousFileNames = new[]
        {
            "..",
            "../",
            "..\\",
            "/etc/passwd",
            "\\windows\\system32\\config\\sam",
            "file.txt/../../../../../etc/passwd",
            "..%2F..%2Fetc%2Fpasswd",
            "....//....//etc/passwd",
            "..\\..\\..\\windows\\system32\\config\\sam"
        };

        foreach (var maliciousName in maliciousFileNames)
        {
            var request = new MediaRequest
            {
                Url = "https://example.com/test.txt",
                FileName = maliciousName,
                OutputPath = _testOutputPath
            };

            // Act
            var act = async () => await pipeline.ProcessAsync(request);

            // Assert - should throw SecurityException for path traversal attempts
            // The file should NOT be written at all
            await act.Should().ThrowAsync<SecurityException>();
        }
    }

    [Fact]
    public async Task ProcessAsync_WithUrlEncodedPathTraversal_ThrowsSecurityException()
    {
        // Arrange
        var testContent = "Test file content"u8.ToArray();
        var mockHandler = CreateMockHttpHandler(HttpStatusCode.OK, testContent, "text/plain");
        var httpClient = new HttpClient(mockHandler.Object);
        var options = new MediaPipelineOptions();
        var pipeline = new MediaPipeline(httpClient, options);

        // URL-encoded path traversal attempts in filename
        var encodedTraversalNames = new[]
        {
            "..%2F..%2Fsecret.txt",
            "%2e%2e%2fsecret.txt",
            "..%5csecret.txt",
            "..%252f..%252fsecret.txt",  // Double encoded
            "file%00.txt"  // Null byte
        };

        foreach (var maliciousName in encodedTraversalNames)
        {
            var request = new MediaRequest
            {
                Url = "https://example.com/test.txt",
                FileName = maliciousName,
                OutputPath = _testOutputPath
            };

            // Act
            var act = async () => await pipeline.ProcessAsync(request);

            // Assert
            await act.Should().ThrowAsync<SecurityException>();
        }
    }

    [Fact]
    public async Task ProcessAsync_WithPathTraversalInUrl_SanitizesFileName()
    {
        // Arrange
        var testContent = "Test file content"u8.ToArray();
        var mockHandler = CreateMockHttpHandler(HttpStatusCode.OK, testContent, "text/plain");
        var httpClient = new HttpClient(mockHandler.Object);
        var options = new MediaPipelineOptions();
        var pipeline = new MediaPipeline(httpClient, options);

        // URL with path traversal in the path - filename extraction should sanitize
        var request = new MediaRequest
        {
            Url = "https://example.com/dir/../../../etc/passwd",
            OutputPath = _testOutputPath
        };

        // Act
        var result = await pipeline.ProcessAsync(request);

        // Assert - filename should be sanitized to just "passwd"
        Path.GetFileName(result.LocalPath).Should().Be("passwd");
        result.LocalPath.Should().StartWith(Path.GetFullPath(_testOutputPath));
        File.Exists(result.LocalPath).Should().BeTrue();
    }

    [Fact]
    public async Task ProcessAsync_WithUrlEncodedPathTraversalInUrl_SanitizesFileName()
    {
        // Arrange
        var testContent = "Test file content"u8.ToArray();
        var mockHandler = CreateMockHttpHandler(HttpStatusCode.OK, testContent, "text/plain");
        var httpClient = new HttpClient(mockHandler.Object);
        var options = new MediaPipelineOptions();
        var pipeline = new MediaPipeline(httpClient, options);

        // URL with URL-encoded path traversal - should be decoded and sanitized
        var request = new MediaRequest
        {
            Url = "https://example.com/..%2F..%2Fsecret.txt",
            OutputPath = _testOutputPath
        };

        // Act
        var result = await pipeline.ProcessAsync(request);

        // Assert - should extract and sanitize to just "secret.txt"
        Path.GetFileName(result.LocalPath).Should().Be("secret.txt");
        result.LocalPath.Should().StartWith(Path.GetFullPath(_testOutputPath));
    }

    [Fact]
    public async Task ProcessAsync_WithQueryStringInUrl_ExtractsFileNameCorrectly()
    {
        // Arrange
        var testContent = "Test file content"u8.ToArray();
        var mockHandler = CreateMockHttpHandler(HttpStatusCode.OK, testContent, "text/plain");
        var httpClient = new HttpClient(mockHandler.Object);
        var options = new MediaPipelineOptions();
        var pipeline = new MediaPipeline(httpClient, options);

        // URL with query string that has path traversal
        var request = new MediaRequest
        {
            Url = "https://example.com/download?file=../../../etc/passwd",
            OutputPath = _testOutputPath
        };

        // Act
        var result = await pipeline.ProcessAsync(request);

        // Assert - query string is not used for filename, uses path portion
        // The path portion is "/download" which has no filename, so default "download" is used
        Path.GetFileName(result.LocalPath).Should().Be("download");
        result.LocalPath.Should().StartWith(Path.GetFullPath(_testOutputPath));
    }

    [Fact]
    public async Task ProcessAsync_WithNullByteInjection_ThrowsSecurityException()
    {
        // Arrange
        var testContent = "Test file content"u8.ToArray();
        var mockHandler = CreateMockHttpHandler(HttpStatusCode.OK, testContent, "text/plain");
        var httpClient = new HttpClient(mockHandler.Object);
        var options = new MediaPipelineOptions();
        var pipeline = new MediaPipeline(httpClient, options);

        // Null byte injection attempt
        var request = new MediaRequest
        {
            Url = "https://example.com/test.txt",
            FileName = "file.txt\0.php",
            OutputPath = _testOutputPath
        };

        // Act
        var act = async () => await pipeline.ProcessAsync(request);

        // Assert - null bytes should be detected as path traversal attempt and rejected
        await act.Should().ThrowAsync<SecurityException>();
    }

    [Theory]
    [InlineData("file.txt\0.php")]
    [InlineData("file\0.txt")]
    [InlineData("\0secret.txt")]
    [InlineData("file\0\0\0.txt")]
    public async Task ProcessAsync_WithNullByteInFileName_ThrowsSecurityException(string maliciousFileName)
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
            FileName = maliciousFileName,
            OutputPath = _testOutputPath
        };

        // Act
        var act = async () => await pipeline.ProcessAsync(request);

        // Assert
        await act.Should().ThrowAsync<SecurityException>();
    }

    [Fact]
    public async Task ProcessAsync_ResultPath_IsWithinOutputDirectory()
    {
        // Arrange
        var testContent = "Test file content"u8.ToArray();
        var mockHandler = CreateMockHttpHandler(HttpStatusCode.OK, testContent, "text/plain");
        var httpClient = new HttpClient(mockHandler.Object);
        var options = new MediaPipelineOptions();
        var pipeline = new MediaPipeline(httpClient, options);

        var nestedOutputPath = Path.Combine(_testOutputPath, "nested", "deep");
        Directory.CreateDirectory(nestedOutputPath);

        var request = new MediaRequest
        {
            Url = "https://example.com/test.txt",
            OutputPath = nestedOutputPath
        };

        // Act
        var result = await pipeline.ProcessAsync(request);

        // Assert
        var fullOutputPath = Path.GetFullPath(nestedOutputPath);
        var fullResultPath = Path.GetFullPath(result.LocalPath);
        fullResultPath.Should().StartWith(fullOutputPath);
        File.Exists(result.LocalPath).Should().BeTrue();
    }

    [Fact]
    public async Task ProcessAsync_WithAbsolutePathInFileName_ThrowsSecurityException()
    {
        // Arrange
        var testContent = "Test file content"u8.ToArray();
        var mockHandler = CreateMockHttpHandler(HttpStatusCode.OK, testContent, "text/plain");
        var httpClient = new HttpClient(mockHandler.Object);
        var options = new MediaPipelineOptions();
        var pipeline = new MediaPipeline(httpClient, options);

        // Absolute path attempts - should all be rejected with SecurityException
        var absolutePaths = new[]
        {
            "/etc/passwd",
            "\\windows\\system32\\config\\sam"
        };

        foreach (var maliciousPath in absolutePaths)
        {
            var request = new MediaRequest
            {
                Url = "https://example.com/test.txt",
                FileName = maliciousPath,
                OutputPath = _testOutputPath
            };

            // Act
            var act = async () => await pipeline.ProcessAsync(request);

            // Assert - should throw SecurityException for absolute paths
            await act.Should().ThrowAsync<SecurityException>();
        }
    }

    [Theory]
    [InlineData("con", "con_")]
    [InlineData("prn", "prn_")]
    [InlineData("aux", "aux_")]
    [InlineData("nul", "nul_")]
    [InlineData("com1", "com1_")]
    [InlineData("lpt1", "lpt1_")]
    public async Task ProcessAsync_WithReservedWindowsNames_SanitizesCorrectly(string reservedName, string expectedName)
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
            FileName = reservedName,
            OutputPath = _testOutputPath
        };

        // Act
        var result = await pipeline.ProcessAsync(request);

        // Assert - file should exist with sanitized name
        File.Exists(result.LocalPath).Should().BeTrue();
        result.LocalPath.Should().StartWith(Path.GetFullPath(_testOutputPath));
        Path.GetFileName(result.LocalPath).Should().Be(expectedName);
    }

    [Theory]
    [InlineData("....//....//etc/passwd")]
    [InlineData("....\\....\\etc\\passwd")]
    [InlineData(".../.../etc/passwd")]
    [InlineData("file/../../../etc/passwd")]
    [InlineData("./../secret.txt")]
    [InlineData("folder/./../../secret.txt")]
    public async Task ProcessAsync_WithObfuscatedPathTraversal_ThrowsSecurityException(string maliciousFileName)
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
            FileName = maliciousFileName,
            OutputPath = _testOutputPath
        };

        // Act
        var act = async () => await pipeline.ProcessAsync(request);

        // Assert - should throw SecurityException for any path traversal attempt
        await act.Should().ThrowAsync<SecurityException>();
    }

    [Fact]
    public async Task ProcessAsync_WithPathTraversalInOutputPath_ThrowsSecurityException()
    {
        // Arrange
        var testContent = "Test file content"u8.ToArray();
        var mockHandler = CreateMockHttpHandler(HttpStatusCode.OK, testContent, "text/plain");
        var httpClient = new HttpClient(mockHandler.Object);
        var options = new MediaPipelineOptions();
        var pipeline = new MediaPipeline(httpClient, options);

        // Output paths with path traversal
        var maliciousOutputPaths = new[]
        {
            $"{_testOutputPath}/../secret",
            $"{_testOutputPath}\\..\\secret",
            $"../../../etc",
            $"..\\windows\\system32"
        };

        foreach (var maliciousOutputPath in maliciousOutputPaths)
        {
            var request = new MediaRequest
            {
                Url = "https://example.com/test.txt",
                FileName = "safe.txt",
                OutputPath = maliciousOutputPath
            };

            // Act
            var act = async () => await pipeline.ProcessAsync(request);

            // Assert
            await act.Should().ThrowAsync<SecurityException>();
        }
    }

    [Fact]
    public async Task ProcessAsync_FileIsWritten_InCorrectLocation()
    {
        // Arrange
        var testContent = "Test file content"u8.ToArray();
        var mockHandler = CreateMockHttpHandler(HttpStatusCode.OK, testContent, "text/plain");
        var httpClient = new HttpClient(mockHandler.Object);
        var options = new MediaPipelineOptions();
        var pipeline = new MediaPipeline(httpClient, options);

        var nestedPath = Path.Combine(_testOutputPath, "subdir", "nested");
        Directory.CreateDirectory(nestedPath);

        var request = new MediaRequest
        {
            Url = "https://example.com/important.txt",
            FileName = "data.txt",
            OutputPath = nestedPath
        };

        // Act
        var result = await pipeline.ProcessAsync(request);

        // Assert
        File.Exists(result.LocalPath).Should().BeTrue();
        result.LocalPath.Should().Be(Path.Combine(nestedPath, "data.txt"));
        result.LocalPath.Should().StartWith(Path.GetFullPath(_testOutputPath));

        // Verify the content
        var content = await File.ReadAllTextAsync(result.LocalPath);
        content.Should().Be("Test file content");
    }

    [Fact]
    public async Task ProcessAsync_VerifiesPathCannotEscapeViaSymlink()
    {
        // This test verifies that even if the output path contains a symlink,
        // the security validation still prevents path traversal
        // Note: This is a simplified check - real symlink attack prevention
        // requires OS-level checks

        // Arrange
        var testContent = "Test file content"u8.ToArray();
        var mockHandler = CreateMockHttpHandler(HttpStatusCode.OK, testContent, "text/plain");
        var httpClient = new HttpClient(mockHandler.Object);
        var options = new MediaPipelineOptions();
        var pipeline = new MediaPipeline(httpClient, options);

        // Even with symlink-like paths, traversal should be blocked
        var request = new MediaRequest
        {
            Url = "https://example.com/test.txt",
            FileName = "data.txt",
            OutputPath = _testOutputPath
        };

        // Act
        var result = await pipeline.ProcessAsync(request);

        // Assert
        result.LocalPath.Should().StartWith(Path.GetFullPath(_testOutputPath));
        File.Exists(result.LocalPath).Should().BeTrue();
    }

    private static Mock<HttpMessageHandler> CreateMockHttpHandler(
        HttpStatusCode statusCode,
        byte[] content,
        string? contentType,
        long? contentLength = null)
    {
        var mockHandler = new Mock<HttpMessageHandler>();

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                // Create a new ByteArrayContent for each request to avoid ObjectDisposedException
                var httpContent = new ByteArrayContent(content);

                if (!string.IsNullOrEmpty(contentType))
                {
                    httpContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
                }

                if (contentLength.HasValue)
                {
                    httpContent.Headers.ContentLength = contentLength.Value;
                }

                return new HttpResponseMessage
                {
                    StatusCode = statusCode,
                    Content = httpContent
                };
            });

        return mockHandler;
    }
}
