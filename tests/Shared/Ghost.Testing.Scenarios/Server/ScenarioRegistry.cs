using Ghost.Testing.Scenarios.Scenarios;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Ghost.Testing.Scenarios.Server;

/// <summary>
/// Registry for all available scenarios and their routing.
/// </summary>
public sealed class ScenarioRegistry
{
    private readonly ILogger<ScenarioRegistry> _logger;

    private static readonly string[] AvailableScenarios =
    [
        "/scenario/consent/modal-blocking",
        "/scenario/consent/banner-soft",
        "/scenario/consent/iframe-cmp",
        "/scenario/scroll/auto-threshold",
        "/scenario/scroll/button-driven",
        "/scenario/scroll/virtualized",
        "/scenario/pagination/numbered",
        "/scenario/pagination/cursor",
        "/scenario/pagination/mixed",
        "/scenario/dedupe/query-reorder",
        "/scenario/dedupe/tracking-params",
        "/scenario/antibot/simple-challenge"
    ];

    public ScenarioRegistry(ILogger<ScenarioRegistry> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Registers all scenario routes.
    /// </summary>
    public void RegisterRoutes(WebApplication app)
    {
        // Root endpoint
        app.MapGet("/", () => Results.Ok(new
        {
            message = "Ghost Synthetic Scenario Server",
            version = "1.0.0",
            scenarios = AvailableScenarios
        }));

        // Consent scenarios
        app.MapGet("/scenario/consent/modal-blocking", ConsentScenarios.ModalBlockingHandler);
        app.MapGet("/scenario/consent/banner-soft", ConsentScenarios.BannerSoftHandler);
        app.MapGet("/scenario/consent/iframe-cmp", ConsentScenarios.IframeCmpHandler);
        app.MapPost("/scenario/consent/accept", ConsentScenarios.AcceptConsentHandler);

        // Scroll scenarios
        app.MapGet("/scenario/scroll/auto-threshold", ScrollScenarios.AutoThresholdHandler);
        app.MapGet("/scenario/scroll/button-driven", ScrollScenarios.ButtonDrivenHandler);
        app.MapGet("/scenario/scroll/virtualized", ScrollScenarios.VirtualizedHandler);
        app.MapGet("/api/scroll/load-more", ScrollScenarios.LoadMoreApiHandler);

        // Pagination scenarios
        app.MapGet("/scenario/pagination/numbered", PaginationScenarios.NumberedHandler);
        app.MapGet("/scenario/pagination/cursor", PaginationScenarios.CursorHandler);
        app.MapGet("/scenario/pagination/mixed", PaginationScenarios.MixedHandler);

        // Deduplication scenarios
        app.MapGet("/scenario/dedupe/query-reorder", DedupeScenarios.QueryReorderHandler);
        app.MapGet("/scenario/dedupe/tracking-params", DedupeScenarios.TrackingParamsHandler);

        // Anti-bot scenarios
        app.MapGet("/scenario/antibot/simple-challenge", AntiBotScenarios.SimpleChallengeHandler);
        app.MapPost("/scenario/antibot/verify", AntiBotScenarios.VerifyChallengeHandler);

        _logger.LogInformation("Registered {Count} scenario routes", 16);
    }
}
