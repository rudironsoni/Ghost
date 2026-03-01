namespace Ghost.Contracts.Simulation;

/// <summary>
/// Represents the result of a simulated social media action.
/// </summary>
public class SimulationResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the action would succeed.
    /// </summary>
    public bool WouldSucceed { get; set; }

    /// <summary>
    /// Gets or sets the list of validation errors, if any.
    /// </summary>
    public IReadOnlyList<string> ValidationErrors { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets the simulated post ID that would be returned.
    /// </summary>
    public string? SimulatedPostId { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the simulation was executed.
    /// </summary>
    public DateTime SimulatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the target platform name (e.g., "X", "LinkedIn").
    /// </summary>
    public string Platform { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the action being simulated (e.g., "CreatePost", "SendMessage").
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the simulated duration of the action.
    /// </summary>
    public TimeSpan SimulatedDuration { get; set; }

    /// <summary>
    /// Gets or sets the screenshot data captured during simulation, if enabled.
    /// </summary>
    public byte[]? Screenshot { get; set; }

    /// <summary>
    /// Gets or sets the selector validation report.
    /// </summary>
    public string? SelectorValidationReport { get; set; }

    /// <summary>
    /// Gets or sets the content preview HTML, if generated.
    /// </summary>
    public string? PreviewHtml { get; set; }

    /// <summary>
    /// Gets or sets additional metadata about the simulation.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = [];

    /// <summary>
    /// Gets or sets the simulated response data.
    /// </summary>
    public object? SimulatedResponse { get; set; }

    /// <summary>
    /// Gets or sets the warnings generated during simulation.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Creates a successful simulation result.
    /// </summary>
    public static SimulationResult Success(string platform, string action, string? postId = null)
    {
        return new SimulationResult
        {
            WouldSucceed = true,
            Platform = platform,
            Action = action,
            SimulatedPostId = postId ?? Guid.NewGuid().ToString("N")[..16],
            SimulatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates a failed simulation result with validation errors.
    /// </summary>
    public static SimulationResult Failure(string platform, string action, IEnumerable<string> errors)
    {
        return new SimulationResult
        {
            WouldSucceed = false,
            Platform = platform,
            Action = action,
            ValidationErrors = errors.ToList().AsReadOnly(),
            SimulatedAt = DateTime.UtcNow
        };
    }
}
