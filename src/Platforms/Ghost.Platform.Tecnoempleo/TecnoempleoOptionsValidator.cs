using System;
using Microsoft.Extensions.Options;

namespace Ghost.Platform.Tecnoempleo.Jobs;

/// <summary>
/// Validates TecnoempleoOptions when bound from configuration.
/// Ensures required fields, URL formats, numeric ranges and TimeSpan values.
/// Follows the same pattern as InfoJobsOptionsValidator.
/// </summary>
public sealed class TecnoempleoOptionsValidator : IValidateOptions<TecnoempleoOptions>
{
    // nullable name parameter as required
    public ValidateOptionsResult Validate(string? name, TecnoempleoOptions options)
    {
        if (options is null)
            return ValidateOptionsResult.Fail("TecnoempleoOptions: options instance is null");

        // If rate limiting disabled or other flags won't disable whole extension here — always validate required credentials

        var errors = new System.Collections.Generic.List<string>();

        // Required credentials
        if (string.IsNullOrWhiteSpace(options.ClientId))
            errors.Add("ClientId must be provided.");

        if (string.IsNullOrWhiteSpace(options.ClientSecret))
            errors.Add("ClientSecret must be provided.");

        // URL validation
        if (!IsValidUri(options.ApiUrl))
            errors.Add($"ApiUrl is not a valid absolute URI: '{options.ApiUrl}'");

        if (!IsValidUri(options.BaseUrl))
            errors.Add($"BaseUrl is not a valid absolute URI: '{options.BaseUrl}'");

        // Numeric ranges
        if (options.MaxRetries < 0)
            errors.Add("MaxRetries must be >= 0.");

        if (options.MaxRequestsPerMinute < 0)
            errors.Add("MaxRequestsPerMinute must be >= 0.");

        if (options.MaxRequestsPerHour < 0)
            errors.Add("MaxRequestsPerHour must be >= 0.");

        // TimeSpan values should be positive
        if (options.RequestDelay <= TimeSpan.Zero)
            errors.Add("RequestDelay must be a positive TimeSpan.");

        if (options.RetryDelay <= TimeSpan.Zero)
            errors.Add("RetryDelay must be a positive TimeSpan.");

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
