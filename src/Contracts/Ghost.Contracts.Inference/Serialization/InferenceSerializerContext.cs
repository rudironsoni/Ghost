using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ghost.Contracts.Inference.Serialization;

/// <summary>
/// JSON serializer context for Inference domain types.
/// Enables AOT-compatible source-generated serialization.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultBufferSize = 4096,
    WriteIndented = false)]
[JsonSerializable(typeof(InferenceRequest))]
[JsonSerializable(typeof(InferenceResponse))]
[JsonSerializable(typeof(InferenceMessage))]
[JsonSerializable(typeof(InferenceChunk))]
[JsonSerializable(typeof(TokenUsage))]
[JsonSerializable(typeof(InferenceRole))]
[JsonSerializable(typeof(List<InferenceMessage>))]
[JsonSerializable(typeof(IReadOnlyList<InferenceMessage>))]
[JsonSerializable(typeof(IReadOnlyList<string>))]
public partial class InferenceSerializerContext : JsonSerializerContext
{
}
