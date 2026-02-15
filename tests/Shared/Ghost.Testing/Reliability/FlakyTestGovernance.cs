using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Xunit;

namespace Ghost.Testing.Reliability;

/// <summary>
/// Attribute to mark a test as quarantined due to flakiness.
/// Quarantined tests are skipped but tracked for resolution.
/// </summary>
/// <remarks>
/// Quarantine requirements:
/// - Owner assignment (required)
/// - Expiry date (max 30 days, required)
/// - Linked issue with RCA (required)
/// - RCA template completed (required)
///
/// Tests auto-expire to fail-closed after expiry date.
/// </remarks>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class QuarantineAttribute : Attribute
{
    /// <summary>
    /// Gets the owner responsible for resolving the flaky test.
    /// </summary>
    public string Owner { get; }

    /// <summary>
    /// Gets the expiry date for the quarantine.
    /// Tests will fail after this date if not resolved.
    /// </summary>
    public DateTime ExpiryDate { get; }

    /// <summary>
    /// Gets the linked issue ID containing the RCA.
    /// </summary>
    public string LinkedIssue { get; }

    /// <summary>
    /// Gets the reason for the quarantine.
    /// </summary>
    public string Reason { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="QuarantineAttribute"/> class.
    /// </summary>
    /// <param name="owner">The owner responsible for resolving the flaky test.</param>
    /// <param name="expiryDate">The expiry date for the quarantine (max 30 days from now).</param>
    /// <param name="linkedIssue">The linked issue ID containing the RCA.</param>
    /// <param name="reason">The reason for the quarantine.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when owner is null or empty, expiry date is beyond 30 days, or linked issue is null or empty.
    /// </exception>
    public QuarantineAttribute(string owner, string expiryDate, string linkedIssue, string reason)
    {
        if (string.IsNullOrWhiteSpace(owner))
        {
            throw new ArgumentException("Owner is required for quarantined tests.", nameof(owner));
        }

        if (!DateTime.TryParse(expiryDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedExpiry))
        {
            throw new ArgumentException($"Invalid expiry date format: {expiryDate}", nameof(expiryDate));
        }

        DateTime maxExpiry = DateTime.UtcNow.AddDays(30);
        if (parsedExpiry > maxExpiry)
        {
            throw new ArgumentException(
                $"Quarantine expiry date cannot exceed 30 days from now. Max allowed: {maxExpiry:yyyy-MM-dd}",
                nameof(expiryDate));
        }

        if (parsedExpiry < DateTime.UtcNow)
        {
            throw new ArgumentException(
                $"Quarantine expiry date cannot be in the past: {expiryDate}",
                nameof(expiryDate));
        }

        if (string.IsNullOrWhiteSpace(linkedIssue))
        {
            throw new ArgumentException("Linked issue is required for quarantined tests.", nameof(linkedIssue));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Reason is required for quarantined tests.", nameof(reason));
        }

        Owner = owner;
        ExpiryDate = parsedExpiry;
        LinkedIssue = linkedIssue;
        Reason = reason;
    }
}

/// <summary>
/// Exception thrown when a quarantined test has expired.
/// </summary>
public sealed class QuarantineExpiredException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QuarantineExpiredException"/> class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    public QuarantineExpiredException(string message) : base(message)
    {
    }
}

/// <summary>
/// Tracks test stability metrics for flaky test detection.
/// </summary>
public sealed class FlakyTestTracker
{
    private static readonly ConcurrentDictionary<string, TestStabilityMetrics> _metrics = new();
    private static readonly string _metricsFilePath = Path.Combine(
        Path.GetTempPath(),
        "Ghost.Testing",
        "flaky-test-metrics.json");

    private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true
    };

    /// <summary>
    /// Gets the flake budget target (0.5% over 14 days).
    /// </summary>
    public const double FlakeBudgetTarget = 0.005; // 0.5%

    /// <summary>
    /// Gets the flake budget window in days.
    /// </summary>
    public const int FlakeBudgetWindowDays = 14;

    /// <summary>
    /// Records a test execution result.
    /// </summary>
    /// <param name="testName">The fully qualified test name.</param>
    /// <param name="passed">Whether the test passed.</param>
    /// <param name="executionTimeMs">The test execution time in milliseconds.</param>
    public static void RecordExecution(string testName, bool passed, long executionTimeMs)
    {
        TestStabilityMetrics metrics = _metrics.GetOrAdd(testName, _ => new TestStabilityMetrics { TestName = testName });

        lock (metrics)
        {
            metrics.TotalExecutions++;
            metrics.LastExecutionTime = DateTime.UtcNow;

            if (passed)
            {
                metrics.PassedExecutions++;
            }
            else
            {
                metrics.FailedExecutions++;
                metrics.LastFailureTime = DateTime.UtcNow;
            }

            metrics.ExecutionTimes.Add(executionTimeMs);
            if (metrics.ExecutionTimes.Count > 100)
            {
                metrics.ExecutionTimes.RemoveAt(0);
            }

            // Track recent executions for flake detection
            metrics.RecentExecutions.Enqueue((Timestamp: DateTime.UtcNow, Passed: passed));
            while (metrics.RecentExecutions.Count > 50 &&
                   metrics.RecentExecutions.TryPeek(out (DateTime Timestamp, bool Passed) oldest) &&
                   (DateTime.UtcNow - oldest.Timestamp).TotalDays > 7)
            {
                metrics.RecentExecutions.TryDequeue(out _);
            }
        }

        SaveMetrics();
    }

    /// <summary>
    /// Gets the stability metrics for a test.
    /// </summary>
    /// <param name="testName">The fully qualified test name.</param>
    /// <returns>The stability metrics, or null if not found.</returns>
    public static TestStabilityMetrics? GetMetrics(string testName)
    {
        return _metrics.TryGetValue(testName, out TestStabilityMetrics? metrics) ? metrics : null;
    }

    /// <summary>
    /// Gets all metrics.
    /// </summary>
    /// <returns>All test stability metrics.</returns>
    public static IReadOnlyDictionary<string, TestStabilityMetrics> GetAllMetrics()
    {
        return _metrics.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    /// <summary>
    /// Detects potentially flaky tests based on stability metrics.
    /// </summary>
    /// <param name="minExecutions">Minimum executions required for analysis (default: 10).</param>
    /// <param name="flakeThreshold">Flake threshold (default: 0.1 for 10% failure rate).</param>
    /// <returns>List of potentially flaky tests.</returns>
    public static List<FlakyTestCandidate> DetectFlakyTests(int minExecutions = 10, double flakeThreshold = 0.1)
    {
        var candidates = new List<FlakyTestCandidate>();

        foreach (KeyValuePair<string, TestStabilityMetrics> kvp in _metrics)
        {
            TestStabilityMetrics metrics = kvp.Value;

            lock (metrics)
            {
                if (metrics.TotalExecutions < minExecutions)
                {
                    continue;
                }

                double failureRate = (double)metrics.FailedExecutions / metrics.TotalExecutions;
                if (failureRate >= flakeThreshold)
                {
                    // Check for flake pattern (intermittent failures)
                    int recentFailures = metrics.RecentExecutions.Count(e => !e.Passed);
                    int recentTotal = metrics.RecentExecutions.Count;
                    double recentFailureRate = recentTotal > 0 ? (double)recentFailures / recentTotal : 0;

                    candidates.Add(new FlakyTestCandidate
                    {
                        TestName = metrics.TestName,
                        TotalExecutions = metrics.TotalExecutions,
                        PassedExecutions = metrics.PassedExecutions,
                        FailedExecutions = metrics.FailedExecutions,
                        FailureRate = failureRate,
                        RecentFailureRate = recentFailureRate,
                        LastFailureTime = metrics.LastFailureTime,
                        AverageExecutionTimeMs = metrics.ExecutionTimes.Count > 0
                            ? (long)metrics.ExecutionTimes.Average()
                            : 0
                    });
                }
            }
        }

        return candidates.OrderByDescending(c => c.FailureRate).ToList();
    }

    /// <summary>
    /// Calculates the overall flake rate over the specified window.
    /// </summary>
    /// <param name="windowDays">The window in days (default: 14).</param>
    /// <returns>The flake rate as a percentage.</returns>
    public static double CalculateFlakeRate(int windowDays = FlakeBudgetWindowDays)
    {
        DateTime cutoffDate = DateTime.UtcNow.AddDays(-windowDays);
        int totalExecutions = 0;
        int failedExecutions = 0;

        foreach (KeyValuePair<string, TestStabilityMetrics> kvp in _metrics)
        {
            TestStabilityMetrics metrics = kvp.Value;

            lock (metrics)
            {
                var recentExecutions = metrics.RecentExecutions
                    .Where(e => e.Timestamp >= cutoffDate)
                    .ToList();

                totalExecutions += recentExecutions.Count;
                failedExecutions += recentExecutions.Count(e => !e.Passed);
            }
        }

        return totalExecutions > 0 ? (double)failedExecutions / totalExecutions : 0;
    }

    /// <summary>
    /// Checks if the flake budget has been exceeded.
    /// </summary>
    /// <returns>True if the flake budget is exceeded, false otherwise.</returns>
    public static bool IsFlakeBudgetExceeded()
    {
        double flakeRate = CalculateFlakeRate();
        return flakeRate > FlakeBudgetTarget;
    }

    /// <summary>
    /// Generates a flake report.
    /// </summary>
    /// <returns>The flake report.</returns>
    public static FlakeReport GenerateReport()
    {
        List<FlakyTestCandidate> flakyTests = DetectFlakyTests();
        double flakeRate = CalculateFlakeRate();
        bool budgetExceeded = IsFlakeBudgetExceeded();

        return new FlakeReport
        {
            GeneratedAt = DateTime.UtcNow,
            FlakeRate = flakeRate,
            FlakeBudgetTarget = FlakeBudgetTarget,
            BudgetExceeded = budgetExceeded,
            WindowDays = FlakeBudgetWindowDays,
            FlakyTests = flakyTests,
            TotalTrackedTests = _metrics.Count
        };
    }

    /// <summary>
    /// Loads metrics from disk.
    /// </summary>
    public static void LoadMetrics()
    {
        try
        {
            if (File.Exists(_metricsFilePath))
            {
                string json = File.ReadAllText(_metricsFilePath);
                Dictionary<string, TestStabilityMetrics>? loadedMetrics = JsonSerializer.Deserialize<Dictionary<string, TestStabilityMetrics>>(json, _jsonOptions);

                if (loadedMetrics != null)
                {
                    foreach (KeyValuePair<string, TestStabilityMetrics> kvp in loadedMetrics)
                    {
                        _metrics.TryAdd(kvp.Key, kvp.Value);
                    }
                }
            }
        }
        catch
        {
            // Best-effort load
        }
    }

    /// <summary>
    /// Saves metrics to disk.
    /// </summary>
    private static void SaveMetrics()
    {
        try
        {
            string? directory = Path.GetDirectoryName(_metricsFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonSerializer.Serialize(_metrics.ToDictionary(kvp => kvp.Key, kvp => kvp.Value), _jsonOptions);
            File.WriteAllText(_metricsFilePath, json);
        }
        catch
        {
            // Best-effort save
        }
    }

    /// <summary>
    /// Clears all metrics.
    /// </summary>
    public static void ClearMetrics()
    {
        _metrics.Clear();
        SaveMetrics();
    }
}

/// <summary>
/// Represents test stability metrics.
/// </summary>
public sealed class TestStabilityMetrics
{
    /// <summary>
    /// Gets or sets the fully qualified test name.
    /// </summary>
    public string TestName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the total number of executions.
    /// </summary>
    public int TotalExecutions { get; set; }

    /// <summary>
    /// Gets or sets the number of passed executions.
    /// </summary>
    public int PassedExecutions { get; set; }

    /// <summary>
    /// Gets or sets the number of failed executions.
    /// </summary>
    public int FailedExecutions { get; set; }

    /// <summary>
    /// Gets or sets the last execution time.
    /// </summary>
    public DateTime LastExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets the last failure time.
    /// </summary>
    public DateTime LastFailureTime { get; set; }

    /// <summary>
    /// Gets or sets the execution times in milliseconds.
    /// </summary>
    public List<long> ExecutionTimes { get; set; } = new();

    /// <summary>
    /// Gets or sets the recent executions (timestamp, passed).
    /// </summary>
    public Queue<(DateTime Timestamp, bool Passed)> RecentExecutions { get; set; } = new();
}

/// <summary>
/// Represents a flaky test candidate.
/// </summary>
public sealed class FlakyTestCandidate
{
    /// <summary>
    /// Gets or sets the fully qualified test name.
    /// </summary>
    public string TestName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the total number of executions.
    /// </summary>
    public int TotalExecutions { get; set; }

    /// <summary>
    /// Gets or sets the number of passed executions.
    /// </summary>
    public int PassedExecutions { get; set; }

    /// <summary>
    /// Gets or sets the number of failed executions.
    /// </summary>
    public int FailedExecutions { get; set; }

    /// <summary>
    /// Gets or sets the overall failure rate.
    /// </summary>
    public double FailureRate { get; set; }

    /// <summary>
    /// Gets or sets the recent failure rate (last 7 days).
    /// </summary>
    public double RecentFailureRate { get; set; }

    /// <summary>
    /// Gets or sets the last failure time.
    /// </summary>
    public DateTime LastFailureTime { get; set; }

    /// <summary>
    /// Gets or sets the average execution time in milliseconds.
    /// </summary>
    public long AverageExecutionTimeMs { get; set; }
}

/// <summary>
/// Represents a flake report.
/// </summary>
public sealed class FlakeReport
{
    /// <summary>
    /// Gets or sets the report generation time.
    /// </summary>
    public DateTime GeneratedAt { get; set; }

    /// <summary>
    /// Gets or sets the current flake rate.
    /// </summary>
    public double FlakeRate { get; set; }

    /// <summary>
    /// Gets or sets the flake budget target.
    /// </summary>
    public double FlakeBudgetTarget { get; set; }

    /// <summary>
    /// Gets or sets whether the budget is exceeded.
    /// </summary>
    public bool BudgetExceeded { get; set; }

    /// <summary>
    /// Gets or sets the window days for the flake rate calculation.
    /// </summary>
    public int WindowDays { get; set; }

    /// <summary>
    /// Gets or sets the list of flaky tests.
    /// </summary>
    public List<FlakyTestCandidate> FlakyTests { get; set; } = new();

    /// <summary>
    /// Gets or sets the total number of tracked tests.
    /// </summary>
    public int TotalTrackedTests { get; set; }
}
