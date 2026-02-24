using FluentAssertions;
using Ghost.Sdk.Spider.Configuration;
using Ghost.Sdk.Spider.Configuration.Compiler;
using Ghost.Sdk.Spider.Configuration.Models;
using Xunit;
using Ghost.Testing.Reliability;
using Xunit.Abstractions;

namespace Ghost.Sdk.Spider.Tests.Unit.Configuration;

/// <summary>
/// Comprehensive tests for ConfigurationCompiler covering YAML and JSON compilation.
/// </summary>
public class ConfigurationCompilerTests : ReliabilityTestBase
{
    public ConfigurationCompilerTests(ITestOutputHelper output) : base(output) { }
    private readonly ConfigurationCompiler _compiler;

    public ConfigurationCompilerTests()
    {
        _compiler = new ConfigurationCompiler();
    }

    [Fact]
    public void Constructor_ShouldInitialize()
    {
        // Arrange & Act
        var compiler = new ConfigurationCompiler();

        // Assert
        compiler.Should().NotBeNull();
    }

    [Fact]
    public void CompileFromYaml_WithValidYaml_ShouldSucceed()
    {
        // Arrange
        var yaml = @"
name: TestSpider
version: '1.0.0'
description: A test spider
target:
  startUrls:
    - https://example.com
extraction:
  selectors:
    - name: title
      path: //h1
";

        // Act
        var result = _compiler.CompileFromYaml(yaml);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Configuration.Should().NotBeNull();
        result.Configuration!.Name.Should().Be("TestSpider");
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void CompileFromYaml_WithEmptyString_ShouldFail()
    {
        // Act
        var result = _compiler.CompileFromYaml(string.Empty);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Configuration.Should().BeNull();
        result.Errors.Should().NotBeEmpty();
        result.Errors.Should().Contain(e => e.Contains("empty"));
    }

    [Fact]
    public void CompileFromYaml_WithNullString_ShouldFail()
    {
        // Act
        var result = _compiler.CompileFromYaml(null!);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Configuration.Should().BeNull();
    }

    [Fact]
    public void CompileFromYaml_WithWhitespaceOnly_ShouldFail()
    {
        // Act
        var result = _compiler.CompileFromYaml("   ");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Configuration.Should().BeNull();
    }

    [Fact]
    public void CompileFromYaml_WithInvalidYaml_ShouldFail()
    {
        // Arrange
        var invalidYaml = @"
name: TestSpider
  invalid indentation:
    - broken
";

        // Act
        var result = _compiler.CompileFromYaml(invalidYaml);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Configuration.Should().BeNull();
        result.Errors.Should().NotBeEmpty();
        result.Errors.Should().Contain(e => e.Contains("YAML") || e.Contains("parsing"));
    }

    [Fact]
    public void CompileFromJson_WithValidJson_ShouldSucceed()
    {
        // Arrange
        var json = @"
{
    ""name"": ""TestSpider"",
    ""version"": ""1.0.0"",
    ""description"": ""A test spider"",
    ""target"": {
        ""startUrls"": [""https://example.com""]
    },
    ""extraction"": {
        ""selectors"": [
            {
                ""name"": ""title"",
                ""path"": ""//h1""
            }
        ]
    }
}";

        // Act
        var result = _compiler.CompileFromJson(json);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Configuration.Should().NotBeNull();
        result.Configuration!.Name.Should().Be("TestSpider");
    }

    [Fact]
    public void CompileFromJson_WithEmptyString_ShouldFail()
    {
        // Act
        var result = _compiler.CompileFromJson(string.Empty);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Configuration.Should().BeNull();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void CompileFromJson_WithNullString_ShouldFail()
    {
        // Act
        var result = _compiler.CompileFromJson(null!);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Configuration.Should().BeNull();
    }

    [Fact]
    public void CompileFromJson_WithInvalidJson_ShouldFail()
    {
        // Arrange
        var invalidJson = @"{ ""name"": ""TestSpider"", broken json }";

        // Act
        var result = _compiler.CompileFromJson(invalidJson);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Configuration.Should().BeNull();
        result.Errors.Should().NotBeEmpty();
        result.Errors.Should().Contain(e => e.Contains("JSON") || e.Contains("parsing"));
    }

    [Fact]
    public void CompileFromJson_WithCommentsAndTrailingCommas_ShouldSucceed()
    {
        // Arrange
        var json = @"
{
    // This is a comment
    ""name"": ""TestSpider"",
    ""version"": ""1.0.0"",
    ""target"": {
        ""startUrls"": [""https://example.com""]
    }, // trailing comma
}";

        // Act
        var result = _compiler.CompileFromJson(json);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Configuration.Should().NotBeNull();
    }

    [Fact]
    public void CompileFromYaml_WithCamelCaseProperties_ShouldDeserialize()
    {
        // Arrange
        var yaml = @"
name: TestSpider
version: '1.0.0'
target:
  startUrls:
    - https://example.com
";

        // Act
        var result = _compiler.CompileFromYaml(yaml);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Configuration.Should().NotBeNull();
    }

    [Fact]
    public void CompileFromJson_WithCaseInsensitiveProperties_ShouldDeserialize()
    {
        // Arrange
        var json = @"
{
    ""NAME"": ""TestSpider"",
    ""VERSION"": ""1.0.0"",
    ""TARGET"": {
        ""STARTURLS"": [""https://example.com""]
    }
}";

        // Act
        var result = _compiler.CompileFromJson(json);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Configuration!.Name.Should().Be("TestSpider");
    }

    [Fact]
    public void CompileFromYaml_WithUnmatchedProperties_ShouldIgnoreThem()
    {
        // Arrange
        var yaml = @"
name: TestSpider
version: '1.0.0'
target:
  startUrls:
    - https://example.com
unknownProperty: someValue
anotherUnknown: 123
";

        // Act
        var result = _compiler.CompileFromYaml(yaml);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Configuration.Should().NotBeNull();
    }

    [Fact]
    public void CompileFromYaml_WithComplexNestedStructure_ShouldDeserialize()
    {
        // Arrange
        var yaml = @"
name: ComplexSpider
version: '1.0.0'
target:
  startUrls:
    - https://example.com
extraction:
  engine: AngleSharp
  defaultSelectorType: CSS
  selectors:
    - name: title
      type: XPath
      expression: //h1
  entities:
    - name: Article
      fields:
        - name: title
          type: String
          selector:
            type: CSS
            expression: h1
";

        // Act
        var result = _compiler.CompileFromYaml(yaml);

        // Assert
        result.IsSuccess.Should().BeTrue(because: result.Errors.Any() ? string.Join(", ", result.Errors) : "no errors");
        result.Configuration.Should().NotBeNull();
    }

    [Fact]
    public void CompileFromJson_WithComplexNestedStructure_ShouldDeserialize()
    {
        // Arrange
        var json = @"
{
    ""name"": ""ComplexSpider"",
    ""version"": ""1.0.0"",
    ""target"": {
        ""startUrls"": [""https://example.com""]
    },
    ""extraction"": {
        ""selectors"": [
            {
                ""name"": ""title"",
                ""path"": ""//h1""
            }
        ]
    }
}";

        // Act
        var result = _compiler.CompileFromJson(json);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Configuration.Should().NotBeNull();
    }

    [Fact]
    public void Validate_WithValidConfiguration_ShouldReturnValid()
    {
        // Arrange
        var config = new SpiderConfiguration
        {
            Id = Guid.NewGuid().ToString(),
            Name = "TestSpider",
            Version = "1.0.0",
            Target = new TargetConfiguration
            {
                StartUrls = new List<string> { "https://example.com" }
            }
        };

        // Act
        var result = _compiler.Validate(config);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ConfigurationCompilationResult_Success_ShouldHaveCorrectProperties()
    {
        // Arrange
        var config = new SpiderConfiguration
        {
            Name = "TestSpider",
            Version = "1.0.0"
        };

        // Act
        var result = ConfigurationCompilationResult.Success(config);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Configuration.Should().Be(config);
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ConfigurationCompilationResult_FailureWithString_ShouldHaveCorrectProperties()
    {
        // Act
        var result = ConfigurationCompilationResult.Failure("Test error");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Configuration.Should().BeNull();
        result.Errors.Should().ContainSingle();
        result.Errors[0].Should().Be("Test error");
    }

    [Fact]
    public void ConfigurationCompilationResult_FailureWithMultipleErrors_ShouldHaveAll()
    {
        // Arrange
        var errors = new[] { "Error 1", "Error 2", "Error 3" };

        // Act
        var result = ConfigurationCompilationResult.Failure(errors);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Configuration.Should().BeNull();
        result.Errors.Should().HaveCount(3);
        result.Errors.Should().Contain(errors);
    }
}
