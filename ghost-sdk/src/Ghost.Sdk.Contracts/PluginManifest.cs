using System.Text.Json.Serialization;

namespace Ghost.Sdk.Contracts;

/// <summary>
/// Unique identifier for a plugin.
/// </summary>
public sealed record PluginId(string Value)
{
    public override string ToString() => Value;
}

/// <summary>
/// Unique identifier for a spider within a plugin.
/// </summary>
public sealed record SpiderId(string Value)
{
    public override string ToString() => Value;
}

/// <summary>
/// Descriptor for a spider within a plugin manifest.
/// </summary>
public sealed record SpiderDescriptor(
    SpiderId SpiderId,
    string EntryStepId,
    IReadOnlyDictionary<string, string> Capabilities);

/// <summary>
/// Plugin manifest containing metadata and spider definitions.
/// </summary>
public sealed record PluginManifest(
    PluginId PluginId,
    string Version,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<SpiderDescriptor> Spiders);
