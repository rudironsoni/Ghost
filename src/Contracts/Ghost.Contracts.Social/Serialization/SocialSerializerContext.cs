using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ghost.Contracts.Social.Serialization;

/// <summary>
/// JSON serializer context for Social domain types.
/// Enables AOT-compatible source-generated serialization.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultBufferSize = 4096,
    WriteIndented = false)]
[JsonSerializable(typeof(SocialProfile))]
[JsonSerializable(typeof(SocialPost))]
[JsonSerializable(typeof(SocialConnection))]
[JsonSerializable(typeof(SocialExperience))]
[JsonSerializable(typeof(SocialEducation))]
[JsonSerializable(typeof(CreatePostRequest))]
[JsonSerializable(typeof(ProfileSearchCriteria))]
[JsonSerializable(typeof(ConnectionsOptions))]
[JsonSerializable(typeof(FeedOptions))]
[JsonSerializable(typeof(List<SocialProfile>))]
[JsonSerializable(typeof(List<SocialPost>))]
[JsonSerializable(typeof(List<SocialConnection>))]
[JsonSerializable(typeof(List<SocialExperience>))]
[JsonSerializable(typeof(List<SocialEducation>))]
[JsonSerializable(typeof(IReadOnlyList<string>))]
public partial class SocialSerializerContext : JsonSerializerContext
{
}
