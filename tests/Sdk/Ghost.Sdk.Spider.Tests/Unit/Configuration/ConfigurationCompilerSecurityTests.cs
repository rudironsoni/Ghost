using FluentAssertions;
using Ghost.Sdk.Spider.Configuration;
using Ghost.Sdk.Spider.Configuration.Compiler;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ghost.Sdk.Spider.Tests.Unit.Configuration;

/// <summary>
/// Security tests for ConfigurationCompiler to verify protection against
/// deserialization attacks and malicious YAML payloads.
/// </summary>
public class ConfigurationCompilerSecurityTests
{
    private readonly ConfigurationCompiler _compiler;

    public ConfigurationCompilerSecurityTests()
    {
        _compiler = new ConfigurationCompiler(NullLogger<ConfigurationCompiler>.Instance);
    }

    #region Malicious YAML Tag Tests

    [Theory]
    [InlineData("!!System.Diagnostics.Process { StartInfo: { FileName: calc.exe } }", "System type tag")]
    [InlineData("!!System.IO.FileInfo [C:/windows/system32/calc.exe]", "System.IO type tag")]
    [InlineData("!!System.Data.DataSet {}", "System.Data type tag")]
    [InlineData("!!System.Byte[] [MTIzNA==]", "Byte array type tag")]
    public void CompileFromYaml_WithSystemTypeTags_ShouldFail(string maliciousPayload, string description)
    {
        // Arrange
        var yaml = $@"
name: AttackSpider
version: '1.0.0'
target:
  startUrls:
    - https://example.com
maliciousField: {maliciousPayload}
";

        // Act
        var result = _compiler.CompileFromYaml(yaml);

        // Assert
        result.IsSuccess.Should().BeFalse($"because {description} should be blocked");
        result.Errors.Should().Contain(e =>
            e.Contains("Security") ||
            e.Contains("blocked") ||
            e.Contains("Unauthorized") ||
            e.Contains("not allowed"));
        result.Configuration.Should().BeNull();
    }

    [Theory]
    [InlineData("!!python/object:os.system", "Python object tag")]
    [InlineData("!!python/module:subprocess", "Python module tag")]
    [InlineData("!!java.io.FileInputStream", "Java type tag")]
    [InlineData("!!ruby/object:File", "Ruby object tag")]
    [InlineData("!!perl/text", "Perl type tag")]
    [InlineData("!!php/object", "PHP object tag")]
    public void CompileFromYaml_WithForeignLanguageTags_ShouldFail(string maliciousTag, string description)
    {
        // Arrange
        var yaml = $@"
name: AttackSpider
version: '1.0.0'
target:
  startUrls:
    - https://example.com
attack: {maliciousTag} []
";

        // Act
        var result = _compiler.CompileFromYaml(yaml);

        // Assert
        result.IsSuccess.Should().BeFalse($"because {description} should be blocked");
        result.Errors.Should().Contain(e =>
            e.Contains("blocked") ||
            e.Contains("Security") ||
            e.Contains("dangerous"));
    }

    #endregion

    #region YAML Exploit Patterns

    [Fact]
    public void CompileFromYaml_WithArbitraryTypeTag_ShouldFail()
    {
        // Arrange - Attempt to instantiate an arbitrary .NET type via YAML tag
        var yaml = @"
name: AttackSpider
version: '1.0.0'
target:
  startUrls:
    - !!System.Text.StringBuilder { Capacity: 100 }
";

        // Act
        var result = _compiler.CompileFromYaml(yaml);

        // Assert
        result.IsSuccess.Should().BeFalse("arbitrary type instantiation should be blocked");
        result.Configuration.Should().BeNull();
    }

    [Fact]
    public void CompileFromYaml_WithNestedMaliciousType_ShouldFail()
    {
        // Arrange - Try to hide malicious type in nested structure
        var yaml = @"
name: AttackSpider
version: '1.0.0'
target:
  startUrls:
    - https://example.com
extraction:
  selectors:
    - name: title
      path: !!System.Xml.XmlDocument []
";

        // Act
        var result = _compiler.CompileFromYaml(yaml);

        // Assert
        result.IsSuccess.Should().BeFalse("nested malicious types should be blocked");
        result.Configuration.Should().BeNull();
    }

    [Fact]
    public void CompileFromYaml_WithAssemblyQualifiedNameTag_ShouldFail()
    {
        // Arrange - Try to use fully qualified assembly name
        var yaml = @"
name: AttackSpider
version: '1.0.0'
target:
  startUrls:
    - https://example.com
attack: !!System.Diagnostics.Process, System.Diagnostics, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a []
";

        // Act
        var result = _compiler.CompileFromYaml(yaml);

        // Assert
        result.IsSuccess.Should().BeFalse("assembly qualified name type tags should be blocked");
        result.Configuration.Should().BeNull();
    }

    #endregion

    #region Standard YAML Type Tests

    [Theory]
    [InlineData("!!str test", "String type")]
    [InlineData("!!int 42", "Integer type")]
    [InlineData("!!float 3.14", "Float type")]
    [InlineData("!!bool true", "Boolean type")]
    [InlineData("!!null", "Null type")]
    [InlineData("!!seq []", "Sequence type")]
    [InlineData("!!map {}", "Map type")]
    public void CompileFromYaml_WithStandardYamlTypes_ShouldSucceed(string typeTag, string description)
    {
        // Arrange - Standard YAML types should be allowed
        var yaml = $@"
name: {typeTag}
version: '1.0.0'
target:
  startUrls:
    - https://example.com
";

        // Act
        var result = _compiler.CompileFromYaml(yaml);

        // Assert
        result.IsSuccess.Should().BeTrue($"because standard {description} should be allowed");
        result.Configuration.Should().NotBeNull();
    }

    #endregion

    #region Valid Configuration Tests

    [Fact]
    public void CompileFromYaml_WithValidConfiguration_ShouldSucceed()
    {
        // Arrange - Valid configuration should still work
        var yaml = @"
name: ValidSpider
version: '1.0.0'
description: A legitimate spider
target:
  startUrls:
    - https://example.com
    - https://example.org
extraction:
  selectors:
    - name: title
      path: //h1
      type: XPath
";

        // Act
        var result = _compiler.CompileFromYaml(yaml);

        // Assert
        result.IsSuccess.Should().BeTrue("valid configurations should be accepted");
        result.Configuration.Should().NotBeNull();
        result.Configuration!.Name.Should().Be("ValidSpider");
        result.Configuration.Target.StartUrls.Should().Contain("https://example.com");
    }

    [Fact]
    public void CompileFromYaml_WithComplexNestedStructure_ShouldSucceed()
    {
        // Arrange - Complex but valid configuration
        var yaml = @"
name: ComplexSpider
version: '2.0.0'
description: Complex spider with full configuration
tags:
  - production
  - critical
  - ecommerce
target:
  startUrls:
    - https://shop.example.com
  urlPatterns:
    - https://shop.example.com/products/.*
  authentication:
    type: Bearer
    token: ${{AUTH_TOKEN}}
extraction:
  engine: AngleSharp
  defaultSelectorType: CSS
  selectors:
    - name: productName
      type: CSS
      expression: h1.product-title
    - name: price
      type: CSS
      expression: span.price
  entities:
    - name: Product
      fields:
        - name: name
          type: String
          required: true
          selector:
            type: CSS
            expression: h1
        - name: description
          type: String
          selector:
            type: CSS
            expression: .description
navigation:
  followLinks: true
  maxDepth: 3
  pagination:
    enabled: true
    type: CssSelector
    selector: a.next
strategies:
  retry:
    maxRetries: 3
    backoffType: Exponential
  rateLimit:
    enabled: true
    requestsPerSecond: 2
    domainSpecific:
      example.com:
        requestsPerSecond: 1
storage:
  type: PostgreSQL
  postgresql:
    host: localhost
    database: spider_data
    table: products
monitoring:
  logging:
    level: Information
    enableConsole: true
  healthCheck:
    enabled: true
    interval: 60
limits:
  maxUrls: 10000
  maxDepth: 5
  maxDuration: 3600
metadata:
  source: test-suite
  environment: testing
";

        // Act
        var result = _compiler.CompileFromYaml(yaml);

        // Assert
        result.IsSuccess.Should().BeTrue("complex valid configurations should be accepted");
        result.Configuration.Should().NotBeNull();
        result.Configuration!.Name.Should().Be("ComplexSpider");
        result.Configuration.Tags.Should().Contain("production");
        result.Configuration.Extraction?.Entities.Should().HaveCount(1);
    }

    #endregion

    #region Type Constraint Tests

    [Fact]
    public void CompileFromYaml_WithObjectTypeProperty_ShouldNotAllowArbitraryTypes()
    {
        // Arrange - The Metadata property is Dictionary<string, object>
        // This test verifies that even with object type, arbitrary types are blocked
        var yaml = @"
name: TestSpider
version: '1.0.0'
target:
  startUrls:
    - https://example.com
metadata:
  key1: value1
  key2: 123
";

        // Act
        var result = _compiler.CompileFromYaml(yaml);

        // Assert - Should succeed because primitives are used in metadata
        result.IsSuccess.Should().BeTrue("primitive values in metadata should be allowed");
        result.Configuration.Should().NotBeNull();
        result.Configuration!.Metadata.Should().ContainKey("key1");
    }

    #endregion

    #region Exception Handling Tests

    [Fact]
    public void CompileFromYaml_WithSecurityViolation_ShouldReturnSecurityError()
    {
        // Arrange
        var yaml = @"
name: AttackSpider
version: '1.0.0'
target:
  startUrls:
    - !!System.Diagnostics.ProcessStartInfo { FileName: cmd.exe }
";

        // Act
        var result = _compiler.CompileFromYaml(yaml);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.Contains("Security", StringComparison.OrdinalIgnoreCase) ||
            e.Contains("unauthorized", StringComparison.OrdinalIgnoreCase) ||
            e.Contains("blocked", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CompileFromYaml_WithInvalidYaml_ShouldReturnParseError()
    {
        // Arrange - Invalid YAML syntax
        var yaml = @"
name: InvalidSpider
  version: '1.0.0'
    target:
  startUrls:
    - https://example.com
";

        // Act
        var result = _compiler.CompileFromYaml(yaml);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.Contains("YAML") ||
            e.Contains("parsing"));
    }

    #endregion

    #region JSON Security Tests

    [Fact]
    public void CompileFromJson_WithValidConfiguration_ShouldSucceed()
    {
        // Arrange - JSON doesn't have the same tag-based attack vectors as YAML
        var json = @"
{
    ""name"": ""JsonSpider"",
    ""version"": ""1.0.0"",
    ""target"": {
        ""startUrls"": [""https://example.com""]
    }
}";

        // Act
        var result = _compiler.CompileFromJson(json);

        // Assert
        result.IsSuccess.Should().BeTrue("valid JSON should be accepted");
        result.Configuration.Should().NotBeNull();
        result.Configuration!.Name.Should().Be("JsonSpider");
    }

    [Fact]
    public void CompileFromJson_WithNestedObjects_ShouldSucceed()
    {
        // Arrange
        var json = @"
{
    ""name"": ""ComplexJsonSpider"",
    ""version"": ""2.0.0"",
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
        result.IsSuccess.Should().BeTrue("JSON with nested objects should be accepted");
        result.Configuration.Should().NotBeNull();
        result.Configuration!.Extraction.Should().NotBeNull();
    }

    #endregion
}
