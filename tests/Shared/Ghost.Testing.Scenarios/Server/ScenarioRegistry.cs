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
        "/scenario/consent/banner-dismiss",
        "/scenario/consent/iframe-cmp",
        "/scenario/consent/iframe-cmp-advanced",
        "/scenario/consent/region-gdpr",
        "/scenario/consent/region-ccpa",
        "/scenario/consent/region-lgpd",
        "/scenario/consent/stateful-persistence",
        "/scenario/consent/reconsent-policy-change",
        "/scenario/scroll/auto-threshold",
        "/scenario/scroll/button-driven",
        "/scenario/scroll/virtualized",
        "/scenario/scroll/duplicate-chunk",
        "/scenario/pagination/numbered",
        "/scenario/pagination/cursor",
        "/scenario/pagination/mixed",
        "/scenario/dedupe/query-reorder",
        "/scenario/dedupe/tracking-params",
        "/scenario/dedupe/redirect-chain",
        "/scenario/dedupe/multiple-aliases",
        "/scenario/dedupe/temporal-changes",
        "/scenario/dedupe/mixed-case-params",
        "/scenario/dedupe/array-params",
        "/scenario/dedupe/session-tracking",
        "/scenario/dedupe/ab-test-variants",
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
        app.MapGet("/scenario/consent/banner-dismiss", ConsentScenarios.BannerDismissHandler);
        app.MapGet("/scenario/consent/iframe-cmp", ConsentScenarios.IframeCmpHandler);
        app.MapGet("/scenario/consent/iframe-cmp-advanced", ConsentScenarios.IframeCmpAdvancedHandler);
        app.MapGet("/scenario/consent/region-gdpr", ConsentScenarios.RegionGdprHandler);
        app.MapGet("/scenario/consent/region-ccpa", ConsentScenarios.RegionCcpaHandler);
        app.MapGet("/scenario/consent/region-lgpd", ConsentScenarios.RegionLgpdHandler);
        app.MapGet("/scenario/consent/stateful-persistence", ConsentScenarios.StatefulPersistenceHandler);
        app.MapGet("/scenario/consent/reconsent-policy-change", ConsentScenarios.ReconsentPolicyChangeHandler);
        app.MapPost("/scenario/consent/accept", ConsentScenarios.AcceptConsentHandler);

        // Scroll scenarios
        app.MapGet("/scenario/scroll/auto-threshold", ScrollScenarios.AutoThresholdHandler);
        app.MapGet("/scenario/scroll/button-driven", ScrollScenarios.ButtonDrivenHandler);
        app.MapGet("/scenario/scroll/virtualized", ScrollScenarios.VirtualizedHandler);
        app.MapGet("/scenario/scroll/duplicate-chunk", ScrollScenarios.DuplicateChunkReplayHandler);
        app.MapGet("/api/scroll/load-more", ScrollScenarios.LoadMoreApiHandler);
        app.MapGet("/api/scroll/load-more-duplicates", ScrollScenarios.LoadMoreDuplicatesApiHandler);

        // Pagination scenarios
        app.MapGet("/scenario/pagination/numbered", PaginationScenarios.NumberedHandler);
        app.MapGet("/scenario/pagination/cursor", PaginationScenarios.CursorHandler);
        app.MapGet("/scenario/pagination/mixed", PaginationScenarios.MixedHandler);

        // Deduplication scenarios
        app.MapGet("/scenario/dedupe/query-reorder", DedupeScenarios.QueryReorderHandler);
        app.MapGet("/scenario/dedupe/tracking-params", DedupeScenarios.TrackingParamsHandler);
        app.MapGet("/scenario/dedupe/redirect-chain", DedupeScenarios.RedirectChainHandler);
        app.MapGet("/scenario/dedupe/multiple-aliases", DedupeScenarios.MultipleAliasesHandler);
        app.MapGet("/scenario/dedupe/temporal-changes", DedupeScenarios.TemporalChangesHandler);
        app.MapGet("/scenario/dedupe/mixed-case-params", DedupeScenarios.MixedCaseParamsHandler);
        app.MapGet("/scenario/dedupe/array-params", DedupeScenarios.ArrayParamsHandler);
        app.MapGet("/scenario/dedupe/session-tracking", DedupeScenarios.SessionTrackingHandler);
        app.MapGet("/scenario/dedupe/ab-test-variants", DedupeScenarios.ABTestVariantsHandler);

        // Anti-bot scenarios
        app.MapGet("/scenario/antibot/simple-challenge", AntiBotScenarios.SimpleChallengeHandler);
        app.MapPost("/scenario/antibot/verify", AntiBotScenarios.VerifyChallengeHandler);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Registered {Count} scenario routes", 31);
        }
    }
}
