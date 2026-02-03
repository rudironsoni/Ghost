using Ghost.Contracts.Social;

namespace Ghost.Contracts.Simulation;

/// <summary>
/// Interface for platform-specific simulation validators.
/// </summary>
public interface IXPlatformSimulationValidator
{
    /// <summary>
    /// Gets the platform name this validator supports.
    /// </summary>
    string PlatformName { get; }

    /// <summary>
    /// Validates a post creation request against platform rules.
    /// </summary>
    /// <param name="request">The post creation request.</param>
    /// <returns>The validation result.</returns>
    Task<ValidationResult> ValidatePostAsync(CreatePostRequest request);

    /// <summary>
    /// Validates that required DOM selectors are present on the page.
    /// </summary>
    /// <param name="page">The browser page.</param>
    /// <returns>A task representing the validation result.</returns>
    Task<ValidationResult> ValidateSelectorsAsync(object page);

    /// <summary>
    /// Generates a preview of how the post would appear.
    /// </summary>
    /// <param name="request">The post creation request.</param>
    /// <returns>The preview HTML string.</returns>
    Task<string> GeneratePreviewAsync(CreatePostRequest request);

    /// <summary>
    /// Simulates the post creation without actual execution.
    /// </summary>
    /// <param name="request">The post creation request.</param>
    /// <returns>The simulation result.</returns>
    Task<SimulationResult> SimulatePostAsync(CreatePostRequest request);

    /// <summary>
    /// Gets the maximum content length allowed by the platform.
    /// </summary>
    int MaxContentLength { get; }

    /// <summary>
    /// Gets the maximum number of media attachments allowed.
    /// </summary>
    int MaxMediaAttachments { get; }

    /// <summary>
    /// Gets the supported media types.
    /// </summary>
    IReadOnlyList<string> SupportedMediaTypes { get; }
}
