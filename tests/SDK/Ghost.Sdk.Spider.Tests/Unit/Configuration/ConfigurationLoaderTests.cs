using FluentAssertions;
using Ghost.Sdk.Spider.Configuration;
using Ghost.Sdk.Spider.Tests.TestHelpers;
using NUnit.Framework;

namespace Ghost.Sdk.Spider.Tests.Unit.Configuration;

[TestFixture]
public class ConfigurationLoaderTests
{
    private ConfigurationLoader _loader = null!;

    [SetUp]
    public void Setup()
    {
        _loader = new ConfigurationLoader();
    }

    [Test]
    public async Task LoadFromFileAsync_WithValidYamlFile_ShouldLoad()
    {
        // Arrange
        var filePath = TestData.GetFixturePath("test-config.yaml");

        // Act
        var config = await _loader.LoadFromFileAsync(filePath);

        // Assert
        config.Should().NotBeNull();
        config.Name.Should().NotBeNullOrEmpty();
    }

    [Test]
    public async Task LoadFromFileAsync_WithValidJsonFile_ShouldLoad()
    {
        // Arrange
        var filePath = TestData.GetFixturePath("test-config.json");

        // Act
        var config = await _loader.LoadFromFileAsync(filePath);

        // Assert
        config.Should().NotBeNull();
        config.Name.Should().NotBeNullOrEmpty();
    }

    [Test]
    public async Task LoadFromFileAsync_WithNonExistentFile_ShouldThrow()
    {
        // Arrange
        var filePath = "nonexistent-config.yaml";

        // Act & Assert
        await Assert.ThatAsync(
            async () => await _loader.LoadFromFileAsync(filePath),
            Throws.TypeOf<FileNotFoundException>());
    }

    [Test]
    public void LoadFromFileAsync_WithNullPath_ShouldThrow()
    {
        // Act & Assert
        Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _loader.LoadFromFileAsync(null!));
    }

    [Test]
    public async Task LoadFromFileAsync_WithUnsupportedFormat_ShouldThrow()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, "test content");

        try
        {
            // Act & Assert
            await Assert.ThatAsync(
                async () => await _loader.LoadFromFileAsync(tempFile),
                Throws.TypeOf<InvalidOperationException>());
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
    public void LoadFromYaml_WithValidContent_ShouldLoad()
    {
        // Arrange
        var yaml = @"
id: test-spider-id
name: TestSpider
version: 1.0.0
target:
  startUrls:
    - https://example.com
limits:
  maxDepth: 3
  maxPages: 100
";

        // Act
        var config = _loader.LoadFromYaml(yaml);

        // Assert
        config.Should().NotBeNull();
        config.Name.Should().Be("TestSpider");
        config.Version.Should().Be("1.0.0");
    }

    [Test]
    public void LoadFromJson_WithValidContent_ShouldLoad()
    {
        // Arrange
        var json = @"
{
    ""id"": ""test-spider-id"",
    ""name"": ""TestSpider"",
    ""version"": ""1.0.0"",
    ""target"": {
        ""startUrls"": [
            ""https://example.com""
        ]
    },
    ""limits"": {
        ""maxDepth"": 3,
        ""maxPages"": 100
    }
}";

        // Act
        var config = _loader.LoadFromJson(json);

        // Assert
        config.Should().NotBeNull();
        config.Name.Should().Be("TestSpider");
        config.Version.Should().Be("1.0.0");
    }

    [Test]
    public void LoadFromYaml_WithInvalidContent_ShouldThrow()
    {
        // Arrange
        var invalidYaml = @"
name: TestSpider
targets: [invalid yaml structure
";

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => _loader.LoadFromYaml(invalidYaml));
    }

    [Test]
    public void LoadFromJson_WithInvalidContent_ShouldThrow()
    {
        // Arrange
        var invalidJson = "{ invalid json }";

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => _loader.LoadFromJson(invalidJson));
    }

    [Test]
    public async Task TryLoadFromFile_WithValidFile_ShouldReturnTrue()
    {
        // Arrange
        var filePath = TestData.GetFixturePath("test-config.yaml");

        // Act
        var success = _loader.TryLoadFromFile(filePath, out var config, out var errors);

        // Assert
        success.Should().BeTrue();
        config.Should().NotBeNull();
        errors.Should().BeEmpty();
    }

    [Test]
    public void TryLoadFromFile_WithInvalidFile_ShouldReturnFalse()
    {
        // Arrange
        var filePath = "nonexistent.yaml";

        // Act
        var success = _loader.TryLoadFromFile(filePath, out var config, out var errors);

        // Assert
        success.Should().BeFalse();
        config.Should().BeNull();
        errors.Should().NotBeEmpty();
    }

    [Test]
    public async Task ValidateFileAsync_WithValidFile_ShouldReturnEmptyErrors()
    {
        // Arrange
        var filePath = TestData.GetFixturePath("test-config.yaml");

        // Act
        var errors = await _loader.ValidateFileAsync(filePath);

        // Assert
        errors.Should().BeEmpty();
    }

    [Test]
    public async Task ValidateFileAsync_WithInvalidFile_ShouldReturnErrors()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, "invalid: [yaml content");

        try
        {
            // Act
            var errors = await _loader.ValidateFileAsync(tempFile + ".yaml");

            // Assert
            errors.Should().NotBeEmpty();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
    public async Task ValidateFileAsync_WithNonExistentFile_ShouldReturnErrors()
    {
        // Arrange
        var filePath = "nonexistent-file.yaml";

        // Act
        var errors = await _loader.ValidateFileAsync(filePath);

        // Assert
        errors.Should().NotBeEmpty();
        errors.Should().Contain(e => e.Contains("not found"));
    }

    [Test]
    public async Task LoadFromFileAsync_ShouldHandleCancellation()
    {
        // Arrange
        var filePath = TestData.GetFixturePath("test-config.yaml");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThatAsync(
            async () => await _loader.LoadFromFileAsync(filePath, cts.Token),
            Throws.InstanceOf<OperationCanceledException>());
    }
}
