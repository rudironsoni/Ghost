using System;
using System.Collections.Generic;

namespace Ghost.Resilience;

/// <summary>
/// Represents a failed scrape job captured by the dead letter queue.
/// </summary>
public class FailedScrapeJob
{
    /// <summary>
    /// Gets or sets the unique job identifier.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    /// Gets or sets the platform name (e.g. LinkedIn, Indeed).
    /// </summary>
    public string Platform { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the query used for the scrape.
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the location used for the scrape.
    /// </summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    public string Error { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the stack trace for the failure.
    /// </summary>
    public string StackTrace { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the retry count.
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// Gets or sets the failure timestamp in UTC.
    /// </summary>
    public DateTime FailedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the last retry attempt in UTC.
    /// </summary>
    public DateTime? LastRetryAt { get; set; }

    /// <summary>
    /// Gets or sets the proxy used for the scrape, when applicable.
    /// </summary>
    public string? ProxyUsed { get; set; }

    /// <summary>
    /// Gets or sets the circuit breaker state at failure time.
    /// </summary>
    public string? CircuitState { get; set; }

    /// <summary>
    /// Gets or sets additional metadata for the failure.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
}
