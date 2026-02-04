namespace Ghost.Sdk.Spider.Scheduling.Contracts;

/// <summary>
/// Defines the contract for spider scheduling.
/// </summary>
/// <remarks>
/// Schedulers manage the execution of spiders on a defined schedule, supporting
/// cron expressions, intervals, and manual triggers.
/// </remarks>
public interface IScheduler
{
    /// <summary>
    /// Schedules a spider to run on a cron schedule.
    /// </summary>
    /// <param name="spiderName">The spider name.</param>
    /// <param name="spider">The spider instance to execute.</param>
    /// <param name="cronExpression">The cron expression (e.g., "0 0 * * *" for daily at midnight).</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation with the schedule ID.</returns>
    Task<string> ScheduleCronAsync(
        string spiderName,
        Engine.Spider spider,
        string cronExpression,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Schedules a spider to run at regular intervals.
    /// </summary>
    /// <param name="spiderName">The spider name.</param>
    /// <param name="spider">The spider instance to execute.</param>
    /// <param name="interval">The interval between executions.</param>
    /// <param name="startDelay">Optional delay before the first execution.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation with the schedule ID.</returns>
    Task<string> ScheduleIntervalAsync(
        string spiderName,
        Engine.Spider spider,
        TimeSpan interval,
        TimeSpan? startDelay = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Schedules a spider to run once at a specific time.
    /// </summary>
    /// <param name="spiderName">The spider name.</param>
    /// <param name="spider">The spider instance to execute.</param>
    /// <param name="runAt">The UTC time to run the spider.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation with the schedule ID.</returns>
    Task<string> ScheduleOnceAsync(
        string spiderName,
        Engine.Spider spider,
        DateTimeOffset runAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Triggers a spider to run immediately.
    /// </summary>
    /// <param name="spiderName">The spider name.</param>
    /// <param name="spider">The spider instance to execute.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation with the execution ID.</returns>
    Task<string> TriggerNowAsync(
        string spiderName,
        Engine.Spider spider,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Unschedules a spider.
    /// </summary>
    /// <param name="scheduleId">The schedule ID returned from a schedule method.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UnscheduleAsync(string scheduleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pauses a scheduled spider.
    /// </summary>
    /// <param name="scheduleId">The schedule ID.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task PauseAsync(string scheduleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes a paused spider.
    /// </summary>
    /// <param name="scheduleId">The schedule ID.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ResumeAsync(string scheduleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets information about all scheduled spiders.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation with the schedule information.</returns>
    Task<IEnumerable<ScheduleInfo>> GetSchedulesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets information about a specific schedule.
    /// </summary>
    /// <param name="scheduleId">The schedule ID.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation with the schedule information.</returns>
    Task<ScheduleInfo?> GetScheduleAsync(string scheduleId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Provides information about a scheduled spider.
/// </summary>
public class ScheduleInfo
{
    /// <summary>
    /// Gets or sets the schedule ID.
    /// </summary>
    /// <value>The unique identifier for the schedule.</value>
    public required string ScheduleId { get; init; }

    /// <summary>
    /// Gets or sets the spider name.
    /// </summary>
    /// <value>The name of the scheduled spider.</value>
    public required string SpiderName { get; init; }

    /// <summary>
    /// Gets or sets the schedule type.
    /// </summary>
    /// <value>The type of schedule (Cron, Interval, Once).</value>
    public required string ScheduleType { get; init; }

    /// <summary>
    /// Gets or sets the schedule expression (cron or interval).
    /// </summary>
    /// <value>The schedule expression string.</value>
    public string? Expression { get; init; }

    /// <summary>
    /// Gets or sets the next scheduled run time.
    /// </summary>
    /// <value>The UTC timestamp of the next execution.</value>
    public DateTimeOffset? NextRunTime { get; init; }

    /// <summary>
    /// Gets or sets the previous run time.
    /// </summary>
    /// <value>The UTC timestamp of the last execution.</value>
    public DateTimeOffset? PreviousRunTime { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether the schedule is paused.
    /// </summary>
    /// <value><c>true</c> if paused; otherwise, <c>false</c>.</value>
    public bool IsPaused { get; init; }

    /// <summary>
    /// Gets or sets the creation timestamp.
    /// </summary>
    /// <value>The UTC timestamp when the schedule was created.</value>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Gets or sets the number of times the spider has been executed.
    /// </summary>
    /// <value>The execution count.</value>
    public int ExecutionCount { get; init; }

    /// <summary>
    /// Gets or sets custom metadata for the schedule.
    /// </summary>
    /// <value>Dictionary of metadata key-value pairs.</value>
    public Dictionary<string, object> Metadata { get; init; } = new();
}
