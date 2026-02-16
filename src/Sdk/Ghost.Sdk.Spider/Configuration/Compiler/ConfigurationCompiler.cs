using System.Text.Json;
using FluentValidation.Results;
using Ghost.Sdk.Spider.Configuration.Models;
using Ghost.Sdk.Spider.Configuration.Validation;
using Microsoft.Extensions.Logging;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization.Utilities;

namespace Ghost.Sdk.Spider.Configuration.Compiler;

/// <summary>
/// A type resolver that only allows expected configuration types to be instantiated.
/// This prevents deserialization attacks that attempt to instantiate arbitrary system types.
/// </summary>
internal sealed class SecureTypeResolver : ITypeResolver
{
    // Whitelist of allowed types for deserialization
    private static readonly HashSet<Type> AllowedTypes = new()
    {
        // Root configuration
        typeof(SpiderConfiguration),

        // Model types - Target
        typeof(TargetConfiguration),
        typeof(AuthenticationConfiguration),
        typeof(OAuth2Configuration),

        // Model types - Extraction
        typeof(ExtractionConfiguration),
        typeof(EntityConfiguration),
        typeof(EntityValidationConfiguration),
        typeof(FieldConfiguration),
        typeof(SelectorConfiguration),
        typeof(FormatterConfiguration),

        // Model types - Navigation
        typeof(NavigationConfiguration),
        typeof(PaginationConfiguration),
        typeof(InfiniteScrollConfiguration),

        // Model types - Strategies
        typeof(StrategiesConfiguration),
        typeof(RetryStrategyConfiguration),
        typeof(RateLimitConfiguration),
        typeof(DomainRateLimitConfiguration),
        typeof(CachingConfiguration),

        // Model types - Pipeline
        typeof(PipelineConfiguration),
        typeof(PipelineStageConfiguration),

        // Model types - Storage
        typeof(StorageConfiguration),
        typeof(PostgreSqlConfiguration),
        typeof(ElasticsearchConfiguration),

        // Model types - Schedule
        typeof(ScheduleConfiguration),

        // Model types - Monitoring
        typeof(MonitoringConfiguration),
        typeof(LoggingConfiguration),
        typeof(TelemetryConfiguration),
        typeof(HealthCheckConfiguration),
        typeof(AlertConfiguration),
        typeof(AlertRuleConfiguration),

        // Model types - Limits
        typeof(LimitsConfiguration),

        // Collection types
        typeof(List<string>),
        typeof(List<int>),
        typeof(List<EntityConfiguration>),
        typeof(List<FieldConfiguration>),
        typeof(List<SelectorConfiguration>),
        typeof(List<FormatterConfiguration>),
        typeof(List<PipelineStageConfiguration>),
        typeof(List<AlertRuleConfiguration>),
        typeof(Dictionary<string, object>),
        typeof(Dictionary<string, string>),
        typeof(Dictionary<string, DomainRateLimitConfiguration>),

        // Primitive and common types
        typeof(string),
        typeof(int),
        typeof(uint),
        typeof(long),
        typeof(ulong),
        typeof(short),
        typeof(ushort),
        typeof(byte),
        typeof(sbyte),
        typeof(float),
        typeof(double),
        typeof(decimal),
        typeof(bool),
        typeof(char),
        typeof(DateTime),
        typeof(DateTimeOffset),
        typeof(TimeSpan),
        typeof(Guid),
        typeof(Uri),
        typeof(object)
    };

    // Type name patterns that are blocked (case-insensitive)
    private static readonly string[] BlockedTypePatterns = new[]
    {
        "System.IO.",
        "System.Diagnostics.",
        "System.Data.",
        "System.Reflection.",
        "System.Runtime.",
        "System.Security.",
        "System.Net.",
        "System.ComponentModel",
        "System.Windows",
        "System.Web",
        "Microsoft.Win32",
        "Microsoft.CSharp",
        "System.Activator",
        "System.AppDomain",
        "System.Threading",
        "System.Collections.ArrayList",
        "System.Collections.Hashtable",
        "System.Text.StringBuilder",
        "System.Xml",
        "System.Configuration",
        "System.CodeDom",
        "System.Management",
        "System.ServiceProcess",
        "System.Deployment",
        "System.Drawing",
        "System.Media",
        "System.Speech",
        "System.Management.Automation",
        "System.DirectoryServices",
        "System.EnterpriseServices",
        "System.IdentityModel",
        "System.ServiceModel",
        "System.Workflow",
        "System.Xaml",
        "System.Windows.Forms",
        "System.Web.UI",
        "System.Web.Services",
        "System.Web.Security",
        "System.Web.SessionState",
        "System.Web.Caching",
        "System.Web.Http",
        "System.Web.Mvc",
        "System.Web.Routing",
        "System.Web.Handlers",
        "System.Web.Script",
        "System.Web.Hosting",
        "System.Web.Profile",
        "System.Web.Mail",
        "System.Web.Compilation",
        "System.Web.Configuration",
        "System.Web.UI.WebControls",
        "System.Web.UI.HtmlControls"
    };

    /// <inheritdoc />
    public Type Resolve(Type type, object? value)
    {
        // If the type is already allowed, return it
        if (IsAllowedType(type))
        {
            return type;
        }

        // Check if the type name contains blocked patterns
        string typeName = type.FullName ?? type.Name;
        foreach (string pattern in BlockedTypePatterns)
        {
            if (typeName.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                throw new YamlException(
                    $"Security violation: Type '{typeName}' is not allowed for deserialization. " +
                    "This type is in the blocked list to prevent code execution attacks.");
            }
        }

        // Check if it's a generic type
        if (type.IsGenericType)
        {
            Type genericType = type.GetGenericTypeDefinition();

            // Allow common generic collections if their type arguments are safe
            if (genericType == typeof(List<>) ||
                genericType == typeof(IList<>) ||
                genericType == typeof(ICollection<>) ||
                genericType == typeof(IEnumerable<>) ||
                genericType == typeof(IDictionary<,>) ||
                genericType == typeof(Dictionary<,>) ||
                genericType == typeof(KeyValuePair<,>) ||
                genericType == typeof(Nullable<>) ||
                genericType == typeof(ArraySegment<>))
            {
                // Recursively check type arguments
                foreach (Type arg in type.GetGenericArguments())
                {
                    Resolve(arg, null);
                }
                return type;
            }
        }

        // Allow arrays if the element type is allowed
        if (type.IsArray)
        {
            Resolve(type.GetElementType()!, null);
            return type;
        }

        // Allow enums
        if (type.IsEnum)
        {
            return type;
        }

        // Block all other types
        throw new YamlException(
            $"Security violation: Type '{typeName}' is not in the allowed types whitelist. " +
            "Only configuration model types and primitive types are permitted for deserialization.");
    }

    /// <summary>
    /// Checks if a type is in the allowed list.
    /// </summary>
    private static bool IsAllowedType(Type type)
    {
        if (AllowedTypes.Contains(type))
        {
            return true;
        }

        // Allow enums
        if (type.IsEnum)
        {
            return true;
        }

        // Allow nullable types if the underlying type is allowed
        if (Nullable.GetUnderlyingType(type) is Type underlyingType && IsAllowedType(underlyingType))
        {
            return true;
        }

        return false;
    }
}

/// <summary>
/// Compiles spider configurations from YAML or JSON into C# objects.
/// </summary>
public sealed class ConfigurationCompiler
{
    private readonly IDeserializer _yamlDeserializer;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SpiderConfigurationValidator _validator;
    private readonly ILogger<ConfigurationCompiler>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationCompiler"/> class.
    /// </summary>
    [Obsolete("Use the constructor with ILogger for proper logging support.", error: false)]
    public ConfigurationCompiler()
    {
        _yamlDeserializer = CreateSecureYamlDeserializer();
        _jsonOptions = CreateJsonOptions();
        _validator = new SpiderConfigurationValidator();
        _logger = null;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationCompiler"/> class with logging support.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public ConfigurationCompiler(ILogger<ConfigurationCompiler> logger)
    {
        _yamlDeserializer = CreateSecureYamlDeserializer();
        _jsonOptions = CreateJsonOptions();
        _validator = new SpiderConfigurationValidator();
        _logger = logger;
    }

    /// <summary>
    /// Creates a secure YAML deserializer with type constraints.
    /// </summary>
    /// <returns>A secure YAML deserializer.</returns>
    private static IDeserializer CreateSecureYamlDeserializer()
    {
        // Security: Configure deserializer with strict type constraints
        // Using a whitelist-based type resolver to prevent arbitrary object instantiation attacks
        return new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .WithTypeResolver(new SecureTypeResolver())
            .IgnoreUnmatchedProperties()
            .Build();
    }

    /// <summary>
    /// Creates JSON serializer options.
    /// </summary>
    /// <returns>JSON serializer options.</returns>
    private static JsonSerializerOptions CreateJsonOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };
    }

    /// <summary>
    /// Compiles a YAML configuration string into a <see cref="SpiderConfiguration"/>.
    /// Uses secure deserialization with type constraints to prevent arbitrary object instantiation attacks.
    /// </summary>
    /// <param name="yamlContent">The YAML configuration content.</param>
    /// <returns>A result containing the compiled configuration or validation errors.</returns>
#pragma warning disable CA1848, CA1873 // Logging performance warnings not critical for configuration loading
    public ConfigurationCompilationResult CompileFromYaml(string yamlContent)
    {
        _logger?.LogDebug("Starting YAML configuration compilation");

        if (string.IsNullOrWhiteSpace(yamlContent))
        {
            _logger?.LogWarning("Configuration compilation failed: content is empty");
            return ConfigurationCompilationResult.Failure("Configuration content is empty");
        }

        // Security: Validate YAML structure before deserialization to block malicious tags
        if (!ValidateYamlStructure(yamlContent, out string? structureError))
        {
            _logger?.LogWarning("Configuration compilation failed: YAML structure validation failed - {Error}", structureError);
            return ConfigurationCompilationResult.Failure($"YAML structure validation failed: {structureError}");
        }

        try
        {
            _logger?.LogDebug("Deserializing YAML configuration with type constraints");

            // Security: Use Deserialize<T> with specific expected type to prevent arbitrary object instantiation
            SpiderConfiguration config = _yamlDeserializer.Deserialize<SpiderConfiguration>(yamlContent);

            if (config == null)
            {
                _logger?.LogWarning("Configuration compilation failed: deserialization returned null");
                return ConfigurationCompilationResult.Failure("Failed to deserialize YAML configuration");
            }

            _logger?.LogInformation("Successfully deserialized YAML configuration for spider '{SpiderName}'", config.Name ?? "(unnamed)");

            // Auto-generate ID if not provided
            if (string.IsNullOrWhiteSpace(config.Id))
            {
                config.Id = Guid.NewGuid().ToString();
                _logger?.LogDebug("Generated new ID for configuration: {ConfigId}", config.Id);
            }

            ValidationResult validationResult = _validator.Validate(config);

            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}").ToList();
                _logger?.LogWarning("Configuration validation failed with {ErrorCount} errors: {Errors}",
                    errors.Count, string.Join("; ", errors));
                return ConfigurationCompilationResult.Failure(validationResult);
            }

            _logger?.LogInformation("Configuration compilation successful for spider '{SpiderName}' (ID: {ConfigId})",
                config.Name, config.Id);
            return ConfigurationCompilationResult.Success(config);
        }
        catch (YamlDotNet.Core.YamlException ex) when (ex.Message.Contains("Security violation") || ex.Message.Contains("type"))
        {
            // Security: Log and report type constraint violations
            _logger?.LogError(ex, "Security violation during YAML deserialization");
            return ConfigurationCompilationResult.Failure($"Security error: Unauthorized type in YAML. {ex.Message}");
        }
        catch (YamlDotNet.Core.YamlException ex)
        {
            _logger?.LogWarning(ex, "YAML parsing error during configuration compilation");
            return ConfigurationCompilationResult.Failure($"YAML parsing error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error during YAML configuration compilation");
            return ConfigurationCompilationResult.Failure($"Unexpected error: {ex.Message}");
        }
    }
#pragma warning restore CA1848, CA1873

    /// <summary>
    /// Validates YAML structure for security concerns before deserialization.
    /// Blocks YAML tags that could lead to arbitrary object instantiation attacks.
    /// </summary>
    /// <param name="yamlContent">The YAML content to validate.</param>
    /// <param name="error">Error message if validation fails.</param>
    /// <returns>True if validation passes, false otherwise.</returns>
    private static bool ValidateYamlStructure(string yamlContent, out string? error)
    {
        error = null;

        try
        {
            using var reader = new StringReader(yamlContent);
            var parser = new Parser(reader);

            // Security: Parse through YAML to check for suspicious type tags
            // that could be used for deserialization attacks
            while (parser.MoveNext())
            {
                ParsingEvent? current = parser.Current;

                // Extract tag from different event types
                string? tagValue = null;
                if (current is Scalar scalar)
                {
                    tagValue = scalar.Tag.IsEmpty ? null : scalar.Tag.Value;
                }
                else if (current is MappingStart mapping)
                {
                    tagValue = mapping.Tag.IsEmpty ? null : mapping.Tag.Value;
                }
                else if (current is SequenceStart sequence)
                {
                    tagValue = sequence.Tag.IsEmpty ? null : sequence.Tag.Value;
                }

                // Security: Validate tags against whitelist
                if (!string.IsNullOrEmpty(tagValue) && !IsAllowedYamlTag(tagValue))
                {
                    error = $"Security violation: Blocked potentially dangerous YAML tag '{tagValue}'. " +
                            "Only standard YAML types and configuration tags are allowed.";
                    return false;
                }
            }

            return true;
        }
        catch (YamlException)
        {
            // YAML parsing errors are expected and will be caught during actual deserialization
            // We don't consider them security violations here
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = $"YAML structure validation error: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Checks if a YAML tag is allowed for deserialization.
    /// </summary>
    /// <param name="tag">The YAML tag to check.</param>
    /// <returns>True if the tag is allowed, false otherwise.</returns>
    private static bool IsAllowedYamlTag(string tag)
    {
        // Security: Block tags that could be used for arbitrary object instantiation
        string[] blockedPatterns = new[]
        {
            // System namespace types (primary attack vector)
            "!!System.",
            "!System.",
            "tag:yaml.org,2002:System",

            // Foreign language serialization (could contain payloads)
            "!!python/",
            "!python/",
            "!!java/",
            "!java/",
            "!!ruby/",
            "!ruby/",
            "!!perl/",
            "!perl/",
            "!!php/",
            "!php/",
            "!!js/",
            "!js/",

            // Binary and code execution types
            "!!binary",
            "!!code",

            // Assembly-qualified type names
            ", Version=",
            ", Culture=",
            ", PublicKeyToken=",
        };

        foreach (string pattern in blockedPatterns)
        {
            if (tag.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        // Security: Only allow standard YAML types and whitelisted application tags
        // YamlDotNet uses full tag URIs like "tag:yaml.org,2002:str" instead of shorthand "!!str"
        string[] allowedPatterns = new[]
        {
            // Standard YAML 1.2 types - shorthand format
            "!!str",
            "!!int",
            "!!float",
            "!!bool",
            "!!null",
            "!!seq",
            "!!map",
            "!!timestamp",
            "!!merge",
            "!!set",
            "!!omap",
            "!!pairs",

            // Standard YAML 1.2 types - full URI format (used by YamlDotNet)
            "tag:yaml.org,2002:str",
            "tag:yaml.org,2002:int",
            "tag:yaml.org,2002:float",
            "tag:yaml.org,2002:bool",
            "tag:yaml.org,2002:null",
            "tag:yaml.org,2002:seq",
            "tag:yaml.org,2002:map",
            "tag:yaml.org,2002:timestamp",
            "tag:yaml.org,2002:merge",
            "tag:yaml.org,2002:set",
            "tag:yaml.org,2002:omap",
            "tag:yaml.org,2002:pairs",

            // Empty/implicit tags
            "!",
            "!!",

            // Custom application-specific tags (no double-bang prefix means custom tag)
        };

        foreach (string pattern in allowedPatterns)
        {
            if (tag.StartsWith(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // Security: Block any other double-bang tags (could be arbitrary .NET types)
        if (tag.StartsWith("!!", StringComparison.Ordinal))
        {
            return false;
        }

        // Security: Block tag:yaml.org,2002: tags that aren't in the allowed list
        // This prevents arbitrary type specification via YAML standard tags
        if (tag.StartsWith("tag:yaml.org,2002:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Single-bang tags without namespace indicators are custom application tags
        // These are generally safe as they require explicit type registration
        if (tag.StartsWith('!') && !tag.StartsWith("!!", StringComparison.Ordinal))
        {
            // Block if it looks like a fully qualified type name
            if (tag.Contains('.') && tag.Contains(','))
            {
                return false;
            }
            return true;
        }

        // Unknown tag format - block for security
        return false;
    }

    /// <summary>
    /// Compiles a JSON configuration string into a <see cref="SpiderConfiguration"/>.
    /// </summary>
    /// <param name="jsonContent">The JSON configuration content.</param>
    /// <returns>A result containing the compiled configuration or validation errors.</returns>
#pragma warning disable CA1848, CA1873 // Logging performance warnings not critical for configuration loading
    public ConfigurationCompilationResult CompileFromJson(string jsonContent)
    {
        _logger?.LogDebug("Starting JSON configuration compilation");

        if (string.IsNullOrWhiteSpace(jsonContent))
        {
            _logger?.LogWarning("Configuration compilation failed: content is empty");
            return ConfigurationCompilationResult.Failure("Configuration content is empty");
        }

        try
        {
            _logger?.LogDebug("Deserializing JSON configuration");

            SpiderConfiguration? config = JsonSerializer.Deserialize<SpiderConfiguration>(jsonContent, _jsonOptions);

            if (config == null)
            {
                _logger?.LogWarning("Configuration compilation failed: deserialization returned null");
                return ConfigurationCompilationResult.Failure("Failed to deserialize JSON configuration");
            }

            _logger?.LogInformation("Successfully deserialized JSON configuration for spider '{SpiderName}'", config.Name ?? "(unnamed)");

            // Auto-generate ID if not provided
            if (string.IsNullOrWhiteSpace(config.Id))
            {
                config.Id = Guid.NewGuid().ToString();
                _logger?.LogDebug("Generated new ID for configuration: {ConfigId}", config.Id);
            }

            ValidationResult validationResult = _validator.Validate(config);

            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}").ToList();
                _logger?.LogWarning("Configuration validation failed with {ErrorCount} errors: {Errors}",
                    errors.Count, string.Join("; ", errors));
                return ConfigurationCompilationResult.Failure(validationResult);
            }

            _logger?.LogInformation("Configuration compilation successful for spider '{SpiderName}' (ID: {ConfigId})",
                config.Name, config.Id);
            return ConfigurationCompilationResult.Success(config);
        }
        catch (JsonException ex)
        {
            _logger?.LogWarning(ex, "JSON parsing error during configuration compilation");
            return ConfigurationCompilationResult.Failure($"JSON parsing error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error during JSON configuration compilation");
            return ConfigurationCompilationResult.Failure($"Unexpected error: {ex.Message}");
        }
    }
#pragma warning restore CA1848, CA1873

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
    public static ConfigurationCompilationResult Failure(FluentValidation.Results.ValidationResult validationResult)
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
