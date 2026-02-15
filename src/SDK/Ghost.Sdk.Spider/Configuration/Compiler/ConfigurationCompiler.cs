using System.Text.Json;
using FluentValidation.Results;
using Ghost.Sdk.Spider.Configuration.Validation;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Ghost.Sdk.Spider.Configuration.Compiler;

/// <summary>
/// Compiles spider configurations from YAML or JSON into C# objects.
/// </summary>
public sealed class ConfigurationCompiler
{
    private readonly IDeserializer _yamlDeserializer;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SpiderConfigurationValidator _validator;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationCompiler"/> class.
    /// </summary>
    public ConfigurationCompiler()
    {
        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        _validator = new SpiderConfigurationValidator();
    }

    /// <summary>
    /// Compiles a YAML configuration string into a <see cref="SpiderConfiguration"/>.
    /// </summary>
    /// <param name="yamlContent">The YAML configuration content.</param>
    /// <returns>A result containing the compiled configuration or validation errors.</returns>
    public ConfigurationCompilationResult CompileFromYaml(string yamlContent)
    {
        if (string.IsNullOrWhiteSpace(yamlContent))
        {
            return ConfigurationCompilationResult.Failure("Configuration content is empty");
        }

        try
        {
            SpiderConfiguration config = _yamlDeserializer.Deserialize<SpiderConfiguration>(yamlContent);

            if (config == null)
            {
                return ConfigurationCompilationResult.Failure("Failed to deserialize YAML configuration");
            }

            // Auto-generate ID if not provided
            if (string.IsNullOrWhiteSpace(config.Id))
            {
                config.Id = Guid.NewGuid().ToString();
            }

            ValidationResult validationResult = _validator.Validate(config);

            if (!validationResult.IsValid)
            {
                return ConfigurationCompilationResult.Failure(validationResult);
            }

            return ConfigurationCompilationResult.Success(config);
        }
        catch (YamlDotNet.Core.YamlException ex)
        {
            return ConfigurationCompilationResult.Failure($"YAML parsing error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return ConfigurationCompilationResult.Failure($"Unexpected error: {ex.Message}");
        }
    }

    /// <summary>
    /// Compiles a JSON configuration string into a <see cref="SpiderConfiguration"/>.
    /// </summary>
    /// <param name="jsonContent">The JSON configuration content.</param>
    /// <returns>A result containing the compiled configuration or validation errors.</returns>
    public ConfigurationCompilationResult CompileFromJson(string jsonContent)
    {
        if (string.IsNullOrWhiteSpace(jsonContent))
        {
            return ConfigurationCompilationResult.Failure("Configuration content is empty");
        }

        try
        {
            SpiderConfiguration? config = JsonSerializer.Deserialize<SpiderConfiguration>(jsonContent, _jsonOptions);

            if (config == null)
            {
                return ConfigurationCompilationResult.Failure("Failed to deserialize JSON configuration");
            }

            // Auto-generate ID if not provided
            if (string.IsNullOrWhiteSpace(config.Id))
            {
                config.Id = Guid.NewGuid().ToString();
            }

            ValidationResult validationResult = _validator.Validate(config);

            if (!validationResult.IsValid)
            {
                return ConfigurationCompilationResult.Failure(validationResult);
            }

            return ConfigurationCompilationResult.Success(config);
        }
        catch (JsonException ex)
        {
            return ConfigurationCompilationResult.Failure($"JSON parsing error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return ConfigurationCompilationResult.Failure($"Unexpected error: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates a configuration without compilation.
    /// </summary>
    /// <param name="configuration">The configuration to validate.</param>
    /// <returns>The validation result.</returns>
    public ValidationResult Validate(SpiderConfiguration configuration)
    {
        return _validator.Validate(configuration);
    }
}

/// <summary>
/// Result of configuration compilation.
/// </summary>
public sealed class ConfigurationCompilationResult
{
    /// <summary>
    /// Gets a value indicating whether compilation was successful.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets the compiled configuration (null if failed).
    /// </summary>
    public SpiderConfiguration? Configuration { get; }

    /// <summary>
    /// Gets the list of error messages.
    /// </summary>
    public IReadOnlyList<string> Errors { get; }

    private ConfigurationCompilationResult(
        bool isSuccess,
        SpiderConfiguration? configuration,
        IReadOnlyList<string> errors)
    {
        IsSuccess = isSuccess;
        Configuration = configuration;
        Errors = errors;
    }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <param name="configuration">The compiled configuration.</param>
    /// <returns>A successful compilation result.</returns>
    public static ConfigurationCompilationResult Success(SpiderConfiguration configuration)
    {
        return new ConfigurationCompilationResult(true, configuration, Array.Empty<string>());
    }

    /// <summary>
    /// Creates a failed result with a single error message.
    /// </summary>
    /// <param name="error">The error message.</param>
    /// <returns>A failed compilation result.</returns>
    public static ConfigurationCompilationResult Failure(string error)
    {
        return new ConfigurationCompilationResult(false, null, new[] { error });
    }

    /// <summary>
    /// Creates a failed result from validation errors.
    /// </summary>
    /// <param name="validationResult">The validation result.</param>
    /// <returns>A failed compilation result.</returns>
    public static ConfigurationCompilationResult Failure(ValidationResult validationResult)
    {
        var errors = validationResult.Errors
            .Select(e => $"{e.PropertyName}: {e.ErrorMessage}")
            .ToList();

        return new ConfigurationCompilationResult(false, null, errors);
    }

    /// <summary>
    /// Creates a failed result with multiple error messages.
    /// </summary>
    /// <param name="errors">The error messages.</param>
    /// <returns>A failed compilation result.</returns>
    public static ConfigurationCompilationResult Failure(IEnumerable<string> errors)
    {
        return new ConfigurationCompilationResult(false, null, errors.ToList());
    }
}
