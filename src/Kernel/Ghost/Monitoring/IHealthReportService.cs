namespace Ghost.Monitoring;

/// <summary>
/// Builds detailed health reports for the system.
/// </summary>
public interface IHealthReportService
{
    /// <summary>
    /// Builds a detailed health report.
    /// </summary>
    public Task<HealthReport> BuildReportAsync(CancellationToken ct);
}
