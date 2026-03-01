using System.Text.Json;
using System.Text.Json.Serialization;
using Ghost.Contracts.Social;

namespace Ghost.Contracts.Simulation.Serialization;

/// <summary>
/// JSON serializer context for Simulation domain types.
/// Enables AOT-compatible source-generated serialization.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultBufferSize = 4096,
    WriteIndented = false)]
[JsonSerializable(typeof(SimulationOptions))]
[JsonSerializable(typeof(SimulationResult))]
[JsonSerializable(typeof(SimulationRecord))]
[JsonSerializable(typeof(List<SimulationRecord>))]
[JsonSerializable(typeof(IReadOnlyList<string>))]
[JsonSerializable(typeof(Dictionary<string, object>))]
// Include Social types that are referenced by SimulationRecord
[JsonSerializable(typeof(SocialProfile))]
[JsonSerializable(typeof(CreatePostRequest))]
public partial class SimulationSerializerContext : JsonSerializerContext
{
}
