using System;
using System.Collections.Generic;
using Microsoft.Extensions.Options;

namespace Ghost.Plugin.Indeed;

/// <summary>
/// Validates IndeedOptions when bound from configuration.
/// Ensures required fields when Enabled == true and numeric ranges.
/// </summary>
public sealed class IndeedOptionsValidator : IValidateOptions<IndeedOptions>
{
    // match nullable signature from interface
    public ValidateOptionsResult Validate(string? name, IndeedOptions options)
    {
        if (options is null)
            return ValidateOptionsResult.Fail("IndeedOptions: options instance is null");

        // If the extension is disabled, accept any configuration
        if (!options.Enabled)
            return ValidateOptionsResult.Success;

        List<string> errors = [];

        // Required fields when enabled
        if (string.IsNullOrWhiteSpace(options.ApiKey))
            errors.Add("ApiKey must be provided when Indeed is enabled.");

        // Numeric ranges
        if (options.DelayMinMs < 0)
            errors.Add("DelayMinMs must be >= 0.");

        if (options.DelayMaxMs < options.DelayMinMs)
            errors.Add("DelayMaxMs must be greater than or equal to DelayMinMs.");

        if (options.MaxRetries < 0)
            errors.Add("MaxRetries must be >= 0.");

        if (errors.Count > 0)
            return ValidateOptionsResult.Fail(errors);

        return ValidateOptionsResult.Success;
    }
}
