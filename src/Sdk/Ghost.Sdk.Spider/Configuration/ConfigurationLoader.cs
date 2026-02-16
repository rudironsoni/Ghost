using Ghost.Sdk.Spider.Configuration.Compiler;

namespace Ghost.Sdk.Spider.Configuration;

/// <summary>
/// Loads spider configurations from files.
/// </summary>
public sealed class ConfigurationLoader
{
    private readonly ConfigurationCompiler _compiler;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationLoader"/> class.
    /// </summary>
    [Obsolete("Use the constructor with ILogger parameter instead")]
    public ConfigurationLoader()
    {
        _compiler = new ConfigurationCompiler();
    }

    /// <summary>
    /// Loads a configuration from a file.
    /// </summary>
    /// <param name="filePath">The path to the configuration file (.yaml, .yml, or .json).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The loaded configuration.</returns>
    /// <exception cref="ArgumentNullException">Thrown when filePath is null.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    /// <exception cref="InvalidOperationException">Thrown when compilation fails.</exception>
    public async Task<SpiderConfiguration> LoadFromFileAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Configuration file not found: {filePath}", filePath);
        }

        string content = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
        string extension = Path.GetExtension(filePath).ToLowerInvariant();

        ConfigurationCompilationResult result = extension switch
        {
            ".yaml" or ".yml" => _compiler.CompileFromYaml(content),
            ".json" => _compiler.CompileFromJson(content),
            _ => throw new InvalidOperationException(
                $"Unsupported configuration file format: {extension}. Supported formats: .yaml, .yml, .json")
        };

        if (!result.IsSuccess)
        {
            string errorMessage = string.Join(Environment.NewLine, result.Errors);
            throw new InvalidOperationException(
                $"Configuration validation failed:{Environment.NewLine}{errorMessage}");
        }

        return result.Configuration!;
    }

    /// <summary>
    /// Loads a configuration from a YAML string.
    /// </summary>
    /// <param name="yamlContent">The YAML configuration content.</param>
    /// <returns>The loaded configuration.</returns>
    /// <exception cref="InvalidOperationException">Thrown when compilation fails.</exception>
    public SpiderConfiguration LoadFromYaml(string yamlContent)
    {
        ConfigurationCompilationResult result = _compiler.CompileFromYaml(yamlContent);

        if (!result.IsSuccess)
        {
            string errorMessage = string.Join(Environment.NewLine, result.Errors);
            throw new InvalidOperationException(
                $"Configuration validation failed:{Environment.NewLine}{errorMessage}");
        }

        return result.Configuration!;
    }

    /// <summary>
    /// Loads a configuration from a JSON string.
    /// </summary>
    /// <param name="jsonContent">The JSON configuration content.</param>
    /// <returns>The loaded configuration.</returns>
    /// <exception cref="InvalidOperationException">Thrown when compilation fails.</exception>
    public SpiderConfiguration LoadFromJson(string jsonContent)
    {
        ConfigurationCompilationResult result = _compiler.CompileFromJson(jsonContent);

        if (!result.IsSuccess)
        {
            string errorMessage = string.Join(Environment.NewLine, result.Errors);
            throw new InvalidOperationException(
                $"Configuration validation failed:{Environment.NewLine}{errorMessage}");
        }

        return result.Configuration!;
    }

    /// <summary>
    /// Tries to load a configuration from a file.
    /// </summary>
    /// <param name="filePath">The path to the configuration file.</param>
    /// <param name="configuration">The loaded configuration if successful.</param>
    /// <param name="errors">The list of errors if unsuccessful.</param>
    /// <returns>True if loading was successful; otherwise, false.</returns>
    public bool TryLoadFromFile(
        string filePath,
        out SpiderConfiguration? configuration,
        out IReadOnlyList<string> errors)
    {
        configuration = null;
        List<string> errorList = [];

        try
        {
            if (!File.Exists(filePath))
            {
                errorList.Add($"Configuration file not found: {filePath}");
                errors = errorList;
                return false;
            }

            string content = File.ReadAllText(filePath);
            string extension = Path.GetExtension(filePath).ToLowerInvariant();

            ConfigurationCompilationResult result = extension switch
            {
                ".yaml" or ".yml" => _compiler.CompileFromYaml(content),
                ".json" => _compiler.CompileFromJson(content),
                _ => ConfigurationCompilationResult.Failure(
                    $"Unsupported configuration file format: {extension}")
            };

            configuration = result.Configuration;
            errors = result.Errors;
            return result.IsSuccess;
        }
        catch (Exception ex)
        {
            errorList.Add($"Unexpected error loading configuration: {ex.Message}");
            errors = errorList;
            return false;
        }
    }

    /// <summary>
    /// Validates a configuration file without loading it.
    /// </summary>
    /// <param name="filePath">The path to the configuration file.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A list of validation errors, or an empty list if valid.</returns>
    public async Task<IReadOnlyList<string>> ValidateFileAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            return new[] { $"Configuration file not found: {filePath}" };
        }

        try
        {
            string content = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
            string extension = Path.GetExtension(filePath).ToLowerInvariant();

            ConfigurationCompilationResult result = extension switch
            {
                ".yaml" or ".yml" => _compiler.CompileFromYaml(content),
                ".json" => _compiler.CompileFromJson(content),
                _ => ConfigurationCompilationResult.Failure(
                    $"Unsupported configuration file format: {extension}")
            };

            return result.Errors;
        }
        catch (Exception ex)
        {
            return new[] { $"Unexpected error validating configuration: {ex.Message}" };
        }
    }
}
