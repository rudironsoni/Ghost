using System;
using Microsoft.Extensions.Options;

namespace Ghost.Plugin.Google;

/// <summary>
/// Validates GoogleOptions when bound from configuration.
/// Ensures sub-options are valid when enabled.
/// </summary>
public sealed class GoogleOptionsValidator : IValidateOptions<GoogleOptions>
{
    public ValidateOptionsResult Validate(string? name, GoogleOptions options)
    {
        if (options is null)
            return ValidateOptionsResult.Fail("GoogleOptions: options instance is null");

        var errors = new System.Collections.Generic.List<string>();

        // Validate Gemini sub-options if enabled
        if (options.Gemini?.Enabled == true)
        {
            ValidateGeminiOptions(options.Gemini, errors);
        }

        // Validate Jobs sub-options if enabled
        if (options.Jobs?.Enabled == true)
        {
            ValidateJobsOptions(options.Jobs, errors);
        }

        if (errors.Count > 0)
            return ValidateOptionsResult.Fail(errors);

        return ValidateOptionsResult.Success;
    }

    private static void ValidateGeminiOptions(Gemini.GeminiOptions gemini, System.Collections.Generic.List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(gemini.BaseUrl))
        {
            errors.Add("Google.Gemini.BaseUrl must be provided when Gemini is enabled.");
        }
        else if (!IsValidUri(gemini.BaseUrl))
        {
            errors.Add($"Google.Gemini.BaseUrl is not a valid absolute URI: '{gemini.BaseUrl}'");
        }

        if (gemini.ResponseTimeout <= TimeSpan.Zero)
        {
            errors.Add("Google.Gemini.ResponseTimeout must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(gemini.DefaultModel))
        {
            errors.Add("Google.Gemini.DefaultModel must be provided when Gemini is enabled.");
        }
    }

    private static void ValidateJobsOptions(Jobs.GoogleJobsOptions jobs, System.Collections.Generic.List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(jobs.Country))
        {
            errors.Add("Google.Jobs.Country must be provided when Jobs is enabled.");
        }

        if (jobs.MinDelayMs < 0)
        {
            errors.Add("Google.Jobs.MinDelayMs must be >= 0.");
        }

        if (jobs.MaxDelayMs < 0)
        {
            errors.Add("Google.Jobs.MaxDelayMs must be >= 0.");
        }

        if (jobs.MaxDelayMs < jobs.MinDelayMs)
        {
            errors.Add("Google.Jobs.MaxDelayMs must be greater than or equal to MinDelayMs.");
        }
    }

    private static bool IsValidUri(string uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
            return false;

        return Uri.TryCreate(uri, UriKind.Absolute, out Uri? result)
               && (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps);
    }
}
