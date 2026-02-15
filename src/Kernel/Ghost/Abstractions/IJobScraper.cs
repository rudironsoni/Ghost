using Ghost.Contracts.Jobs;

namespace Ghost.Abstractions;

/// <summary>
/// Marker interface for job scrapers used by the aggregator.
/// </summary>
public interface IJobScraper : IJobClient { }
