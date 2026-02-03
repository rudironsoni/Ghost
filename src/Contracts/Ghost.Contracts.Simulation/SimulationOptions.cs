using System.ComponentModel.DataAnnotations;

namespace Ghost.Contracts.Simulation;

/// <summary>
/// Configuration options for the simulation framework.
/// </summary>
public class SimulationOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether simulation mode is enabled.
    /// When enabled, all social media actions are simulated without actual execution.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to capture screenshots during simulation.
    /// </summary>
    public bool CaptureScreenshots { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to validate DOM selectors during simulation.
    /// </summary>
    public bool ValidateSelectors { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of recorded actions to keep in memory.
    /// </summary>
    [Range(1, 10000)]
    public int MaxRecordedActions { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the directory path for storing screenshots.
    /// </summary>
    public string? ScreenshotDirectory { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to write simulation results to disk.
    /// </summary>
    public bool WriteToDisk { get; set; }

    /// <summary>
    /// Gets or sets the file path for the simulation log.
    /// </summary>
    public string? SimulationLogPath { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to validate content against platform rules.
    /// </summary>
    public bool ValidateContent { get; set; } = true;

    /// <summary>
    /// Gets or sets the simulation delay in milliseconds between actions.
    /// This simulates realistic timing without actual execution delays.
    /// </summary>
    [Range(0, 60000)]
    public int SimulationDelayMs { get; set; } = 100;

    /// <summary>
    /// Gets or sets a value indicating whether to generate preview HTML of posts.
    /// </summary>
    public bool GeneratePreviews { get; set; } = true;

    /// <summary>
    /// Gets or sets the directory path for storing preview HTML files.
    /// </summary>
    public string? PreviewDirectory { get; set; }
}
