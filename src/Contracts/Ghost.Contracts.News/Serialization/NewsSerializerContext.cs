using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ghost.Contracts.News.Serialization;

/// <summary>
/// JSON serializer context for News domain types.
/// Enables AOT-compatible source-generated serialization.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultBufferSize = 4096,
    WriteIndented = false)]
[JsonSerializable(typeof(NewsArticle))]
[JsonSerializable(typeof(NewsFilter))]
[JsonSerializable(typeof(NewsSearchOptions))]
[JsonSerializable(typeof(NewsCategory))]
[JsonSerializable(typeof(List<NewsArticle>))]
[JsonSerializable(typeof(IReadOnlyList<NewsArticle>))]
public partial class NewsSerializerContext : JsonSerializerContext
{
}
