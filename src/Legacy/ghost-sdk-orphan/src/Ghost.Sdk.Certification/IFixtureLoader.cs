namespace Ghost.Sdk.Certification;

/// <summary>
/// Loads fixtures for plugin certification.
/// Fixtures are recorded inputs (HTML, API responses) used for offline testing.
/// </summary>
public interface IFixtureLoader
{
    /// <summary>
    /// Loads fixtures for a specific plugin and spider.
    /// </summary>
    /// <param name="pluginId">Plugin identifier</param>
    /// <param name="spiderId">Spider identifier</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of fixtures for the spider</returns>
    Task<IReadOnlyList<Fixture>> LoadAsync(
        Ghost.Sdk.Contracts.PluginId pluginId,
        Ghost.Sdk.Contracts.SpiderId spiderId,
        CancellationToken ct = default);
}

/// <summary>
/// A fixture containing input data and expected outputs for certification.
/// </summary>
public sealed record Fixture(
    string FixtureId,
    System.Text.Json.JsonDocument Input,
    IReadOnlyList<Ghost.Sdk.Contracts.EngineEvent>? PrerecordedEvents = null);
