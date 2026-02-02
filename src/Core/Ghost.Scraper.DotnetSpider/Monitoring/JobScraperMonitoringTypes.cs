using System;
using System.Collections.Generic;

namespace Ghost.Scraper.DotnetSpider.Monitoring;
    /// <summary>
    /// Represents the health status of a scraping platform.
    /// </summary>
    public enum HealthStatus
    {
        /// <summary>
        /// Platform is operating normally.
        /// </summary>
        Healthy = 0,

        /// <summary>
        /// Platform is experiencing minor issues but still operational.
        /// </summary>
        Degraded = 1,

        /// <summary>
        /// Platform is experiencing critical issues and may be unavailable.
        /// </summary>
        Unhealthy = 2
    }

    /// <summary>
    /// Represents the severity level of an alert.
    /// </summary>
    public enum AlertLevel
    {
        /// <summary>
        /// Informational alert, no action required.
        /// </summary>
        Info = 0,

        /// <summary>
        /// Warning alert, should be monitored.
        /// </summary>
        Warning = 1,

        /// <summary>
        /// Critical alert, immediate action may be required.
        /// </summary>
        Critical = 2
    }

    /// <summary>
    /// Represents the health status of a scraping platform.
    /// </summary>
    public class PlatformHealth
    {
        /// <summary>
        /// Gets or sets the name of the platform.
        /// </summary>
        public string? PlatformName { get; set; }

        /// <summary>
        /// Gets or sets the current health status of the platform.
        /// </summary>
        public HealthStatus Status { get; set; }

        /// <summary>
        /// Gets or sets the success rate as a percentage (0-100).
        /// </summary>
        public double SuccessRate { get; set; }

        /// <summary>
        /// Gets or sets the count of errors encountered.
        /// </summary>
        public int ErrorCount { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the health status was last checked.
        /// </summary>
        public DateTimeOffset LastChecked { get; set; }
    }

    /// <summary>
    /// Represents metrics for a specific platform in the job scraper.
    /// </summary>
    public class PlatformMetrics
    {
        /// <summary>
        /// Gets or sets the total number of requests made to the platform.
        /// </summary>
        public int TotalRequests { get; set; }

        /// <summary>
        /// Gets or sets the number of successful requests.
        /// </summary>
        public int SuccessfulRequests { get; set; }

        /// <summary>
        /// Gets or sets the number of failed requests.
        /// </summary>
        public int FailedRequests { get; set; }

        /// <summary>
        /// Gets or sets the average latency in milliseconds for requests to this platform.
        /// </summary>
        public double AverageLatencyMs { get; set; }

        /// <summary>
        /// Gets or sets a dictionary of error categories and their counts.
        /// </summary>
        public Dictionary<string, int> ErrorCategories { get; set; } = new();
    }

    /// <summary>
    /// Represents aggregated metrics for the job scraper across all platforms.
    /// </summary>
    public class JobScraperMetrics
    {
        /// <summary>
        /// Gets or sets the per-platform metrics.
        /// </summary>
        public Dictionary<string, PlatformMetrics> PerPlatformMetrics { get; set; } = new();

        /// <summary>
        /// Gets or sets the timestamp when these metrics were collected.
        /// </summary>
        public DateTimeOffset Timestamp { get; set; }
    }
