namespace Ghost.Sdk.Spider.Contracts;

/// <summary>
/// Represents the execution state of a spider.
/// </summary>
public enum SpiderState
{
    /// <summary>
    /// Spider has been created but not yet started.
    /// </summary>
    Idle,

    /// <summary>
    /// Spider is actively processing requests.
    /// </summary>
    Running,

    /// <summary>
    /// Spider has been paused and can be resumed.
    /// </summary>
    Paused,

    /// <summary>
    /// Spider has completed all work successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Spider has stopped due to an error.
    /// </summary>
    Failed,

    /// <summary>
    /// Spider has been cancelled by user request.
    /// </summary>
    Cancelled
}
