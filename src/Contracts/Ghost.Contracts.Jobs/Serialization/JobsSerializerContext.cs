using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ghost.Contracts.Jobs.Serialization;

/// <summary>
/// JSON serializer context for Jobs domain types.
/// Enables AOT-compatible source-generated serialization.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultBufferSize = 4096,
    WriteIndented = false)]
[JsonSerializable(typeof(JobListing))]
[JsonSerializable(typeof(JobSearchCriteria))]
[JsonSerializable(typeof(JobSearchResult))]
[JsonSerializable(typeof(JobApplication))]
[JsonSerializable(typeof(ApplicationDetails))]
[JsonSerializable(typeof(ApplicationsFilter))]
[JsonSerializable(typeof(PlatformError))]
[JsonSerializable(typeof(SearchMetadata))]
[JsonSerializable(typeof(JobType))]
[JsonSerializable(typeof(ExperienceLevel))]
[JsonSerializable(typeof(TimePosted))]
[JsonSerializable(typeof(List<JobListing>))]
[JsonSerializable(typeof(IReadOnlyList<JobListing>))]
[JsonSerializable(typeof(IReadOnlyList<PlatformError>))]
public partial class JobsSerializerContext : JsonSerializerContext
{
}
