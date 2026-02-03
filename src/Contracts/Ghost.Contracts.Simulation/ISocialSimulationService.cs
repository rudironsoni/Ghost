using Ghost.Contracts.Social;

namespace Ghost.Contracts.Simulation;

/// <summary>
/// Service for simulating social media actions without actual execution.
/// </summary>
public interface ISocialSimulationService
{
    /// <summary>
    /// Gets a value indicating whether simulation mode is currently enabled.
    /// </summary>
    bool IsSimulationMode { get; }

    /// <summary>
    /// Simulates posting content to a social platform.
    /// </summary>
    /// <param name="request">The post creation request.</param>
    /// <param name="platform">The target platform name.</param>
    /// <returns>A task representing the simulation result.</returns>
    Task<SimulationResult> SimulatePostAsync(CreatePostRequest request, string platform);

    /// <summary>
    /// Simulates a generic social media action.
    /// </summary>
    /// <param name="action">The action name.</param>
    /// <param name="context">The action context.</param>
    /// <param name="platform">The target platform name.</param>
    /// <returns>A task representing the simulation result.</returns>
    Task<SimulationResult> SimulateActionAsync(string action, object context, string platform);

    /// <summary>
    /// Gets all recorded simulation actions.
    /// </summary>
    /// <returns>A read-only list of simulation records.</returns>
    IReadOnlyList<SimulationRecord> GetRecordedActions();

    /// <summary>
    /// Gets recorded simulation actions for a specific platform.
    /// </summary>
    /// <param name="platform">The platform name.</param>
    /// <returns>A read-only list of simulation records.</returns>
    IReadOnlyList<SimulationRecord> GetRecordedActions(string platform);

    /// <summary>
    /// Gets recorded simulation actions for a specific session.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <returns>A read-only list of simulation records.</returns>
    IReadOnlyList<SimulationRecord> GetRecordedActionsBySession(string sessionId);

    /// <summary>
    /// Clears all recorded simulation actions.
    /// </summary>
    void ClearRecordedActions();

    /// <summary>
    /// Captures a screenshot of the current state during simulation.
    /// </summary>
    /// <param name="action">The action being performed.</param>
    /// <param name="platform">The platform name.</param>
    /// <returns>A task representing the screenshot data.</returns>
    Task<byte[]> CaptureScreenshotAsync(string action, string platform);

    /// <summary>
    /// Validates content against platform-specific rules.
    /// </summary>
    /// <param name="request">The post creation request.</param>
    /// <param name="platform">The platform name.</param>
    /// <returns>A task representing the validation result.</returns>
    Task<ValidationResult> ValidateContentAsync(CreatePostRequest request, string platform);

    /// <summary>
    /// Generates a preview of how the post would appear.
    /// </summary>
    /// <param name="request">The post creation request.</param>
    /// <param name="platform">The platform name.</param>
    /// <returns>A task representing the preview HTML.</returns>
    Task<string> GeneratePreviewAsync(CreatePostRequest request, string platform);

    /// <summary>
    /// Starts a new simulation session.
    /// </summary>
    /// <param name="sessionId">Optional session identifier.</param>
    /// <returns>The session identifier.</returns>
    string StartSession(string? sessionId = null);

    /// <summary>
    /// Ends the current simulation session.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    Task EndSessionAsync(string sessionId);

    /// <summary>
    /// Exports simulation records to a file.
    /// </summary>
    /// <param name="filePath">The output file path.</param>
    /// <returns>A task representing the export operation.</returns>
    Task ExportRecordsAsync(string filePath);

    /// <summary>
    /// Gets simulation statistics.
    /// </summary>
    /// <returns>Statistics about recorded simulations.</returns>
    SimulationStatistics GetStatistics();
}

/// <summary>
/// Represents the result of content validation.
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the content is valid.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets or sets the list of validation errors.
    /// </summary>
    public IReadOnlyList<ValidationError> Errors { get; set; } = Array.Empty<ValidationError>();

    /// <summary>
    /// Gets or sets the list of validation warnings.
    /// </summary>
    public IReadOnlyList<ValidationError> Warnings { get; set; } = Array.Empty<ValidationError>();

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static ValidationResult Success()
    {
        return new ValidationResult { IsValid = true };
    }

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    public static ValidationResult Failure(IEnumerable<ValidationError> errors)
    {
        return new ValidationResult
        {
            IsValid = false,
            Errors = errors.ToList().AsReadOnly()
        };
    }
}

/// <summary>
/// Represents a validation error or warning.
/// </summary>
public class ValidationError
{
    /// <summary>
    /// Gets or sets the error code.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the field or property that caused the error.
    /// </summary>
    public string? Field { get; set; }

    /// <summary>
    /// Gets or sets the severity of the error.
    /// </summary>
    public ValidationSeverity Severity { get; set; } = ValidationSeverity.Error;
}

/// <summary>
/// Represents the severity of a validation issue.
/// </summary>
public enum ValidationSeverity
{
    /// <summary>
    /// Informational message.
    /// </summary>
    Info,

    /// <summary>
    /// Warning that doesn't prevent execution.
    /// </summary>
    Warning,

    /// <summary>
    /// Error that prevents execution.
    /// </summary>
    Error
}

/// <summary>
/// Represents simulation statistics.
/// </summary>
public class SimulationStatistics
{
    /// <summary>
    /// Gets or sets the total number of recorded actions.
    /// </summary>
    public int TotalActions { get; set; }

    /// <summary>
    /// Gets or sets the number of successful simulations.
    /// </summary>
    public int SuccessfulSimulations { get; set; }

    /// <summary>
    /// Gets or sets the number of failed simulations.
    /// </summary>
    public int FailedSimulations { get; set; }

    /// <summary>
    /// Gets or sets the number of actions per platform.
    /// </summary>
    public Dictionary<string, int> ActionsPerPlatform { get; set; } = new();

    /// <summary>
    /// Gets or sets the average simulation duration.
    /// </summary>
    public TimeSpan AverageDuration { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the first recorded action.
    /// </summary>
    public DateTime? FirstActionAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the last recorded action.
    /// </summary>
    public DateTime? LastActionAt { get; set; }
}
