using System.Text.Json;
using System.Text.Json.Serialization;
using Ghost.Contracts.Jobs;
using Ghost.Queue;
using Ghost.Resilience;
using Ghost.Session;

namespace Ghost.Serialization;

/// <summary>
/// JSON serializer context for Kernel domain types.
/// Enables AOT-compatible source-generated serialization.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultBufferSize = 4096,
    WriteIndented = false)]
// Queue types
[JsonSerializable(typeof(Job))]
[JsonSerializable(typeof(JobResult))]
[JsonSerializable(typeof(JobPriority))]
// Resilience types
[JsonSerializable(typeof(FailedScrapeJob))]
// Session types
[JsonSerializable(typeof(BrowserSession))]
[JsonSerializable(typeof(ViewportDimensions))]
// Contracts.Jobs types (for cache serialization)
[JsonSerializable(typeof(JobListing))]
[JsonSerializable(typeof(JobSearchCriteria))]
[JsonSerializable(typeof(List<JobListing>))]
[JsonSerializable(typeof(IReadOnlyList<JobListing>))]
// Generic collections
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(List<string>))]
public partial class KernelSerializerContext : JsonSerializerContext
{
}
