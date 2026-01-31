using System;
using Microsoft.Extensions.Options;

namespace Ghost.Platform.InfoJobs.Jobs;

/// <summary>
/// Validates InfoJobsOptions when bound from configuration.
/// Ensures required fields when Enabled == true, URL formats and numeric ranges.
/// </summary>
public sealed class InfoJobsOptionsValidator : IValidateOptions<InfoJobsOptions>
{
    // match nullable signature from interface
    public ValidateOptionsResult Validate(string? name, InfoJobsOptions options)
    {
        if (options is null)
            return ValidateOptionsResult.Fail("InfoJobsOptions: options instance is null");

        // If the extension is disabled, accept any configuration (no further validation)
        if (!options.Enabled)
            return ValidateOptionsResult.Success;

        var errors = new System.Collections.Generic.List<string>();

        // Required credentials
        if (string.IsNullOrWhiteSpace(options.ClientId))
            errors.Add("ClientId must be provided when InfoJobs is enabled.");

        if (string.IsNullOrWhiteSpace(options.ClientSecret))
            errors.Add("ClientSecret must be provided when InfoJobs is enabled.");

        // URL validation
        if (!IsValidUri(options.ApiEndpoint))
            errors.Add($"ApiEndpoint is not a valid absolute URI: '{options.ApiEndpoint}'");

        if (!IsValidUri(options.BaseUrl))
            errors.Add($"BaseUrl is not a valid absolute URI: '{options.BaseUrl}'");

        // Numeric ranges
        if (options.MinDelayMs < 0)
            errors.Add("MinDelayMs must be >= 0.");

        if (options.MaxDelayMs < 0)
            errors.Add("MaxDelayMs must be >= 0.");

        if (options.MaxDelayMs < options.MinDelayMs)
            errors.Add("MaxDelayMs must be greater than or equal to MinDelayMs.");

        if (errors.Count > 0)
            return ValidateOptionsResult.Fail(errors);

        return ValidateOptionsResult.Success;
    }

    private static bool IsValidUri(string uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
            return false;

        return Uri.TryCreate(uri, UriKind.Absolute, out var result)
               && (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps);
    }
}
