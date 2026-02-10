# Ghost.Testing.Scenarios

Synthetic provider scenario server for browser testing without external dependencies.

## Overview

This library provides a Kestrel-based ASP.NET Core server that serves deterministic web scenarios for testing browser automation, scraping, and UX patterns. All scenarios run locally without requiring internet access.

## Architecture

```
Ghost.Testing.Scenarios/
├── Server/
│   ├── ScenarioServer.cs          # Kestrel server lifecycle
│   ├── ScenarioRegistry.cs        # Route registration
│   └── Middleware/
│       ├── ConsentMiddleware.cs   # Cookie consent state
│       ├── InfiniteScrollMiddleware.cs  # Scroll state management
│       └── PaginationMiddleware.cs      # Pagination context
├── Scenarios/
│   ├── ConsentScenarios.cs        # Consent UX patterns
│   ├── ScrollScenarios.cs         # Infinite scroll patterns
│   ├── PaginationScenarios.cs     # Pagination patterns
│   ├── DedupeScenarios.cs         # URL normalization tests
│   ├── AntiBotScenarios.cs        # JavaScript challenge tests
│   └── TestData.cs                # Deterministic job postings
└── Models/
    └── ScenarioModels.cs          # Domain models
```

## Usage

### Starting the Server

```csharp
// Start on dynamic port
var server = await ScenarioServer.CreateAsync();
Console.WriteLine($"Server running at {server.BaseUrl}");

// Start on specific port
var server = await ScenarioServer.CreateAsync(port: 5555);

// Stop the server
await server.StopAsync();
server.Dispose();
```

### Available Scenarios

#### Consent Scenarios

- **`/scenario/consent/modal-blocking`** - Blocking modal that must be accepted
- **`/scenario/consent/banner-soft`** - Non-blocking bottom banner
- **`/scenario/consent/iframe-cmp`** - Consent Management Platform in iframe

#### Infinite Scroll Scenarios

- **`/scenario/scroll/auto-threshold`** - Auto-loads when scrolling near bottom
- **`/scenario/scroll/button-driven`** - "Load More" button pattern
- **`/scenario/scroll/virtualized`** - Virtualized list with 1000+ items

#### Pagination Scenarios

- **`/scenario/pagination/numbered`** - Traditional numbered pages
- **`/scenario/pagination/cursor`** - Cursor-based pagination
- **`/scenario/pagination/mixed`** - Hybrid numbered + auto-scroll

#### Deduplication Scenarios

- **`/scenario/dedupe/query-reorder`** - Same content, different query order
- **`/scenario/dedupe/tracking-params`** - URLs with tracking parameters

#### Anti-Bot Scenarios

- **`/scenario/antibot/simple-challenge`** - JavaScript computational challenge

## Testing Example

```csharp
[Fact]
public async Task ConsentModal_BlocksContentUntilAccepted()
{
    // Arrange
    using var server = await ScenarioServer.CreateAsync();
    using var browser = await Patchright.CreateAsync();
    var page = await browser.NewPageAsync();

    // Act
    await page.GotoAsync($"{server.BaseUrl}/scenario/consent/modal-blocking");
    
    // Assert - Modal is visible
    var modal = await page.QuerySelectorAsync("#consent-modal");
    Assert.NotNull(modal);
    Assert.True(await modal.IsVisibleAsync());

    // Accept consent
    await page.ClickAsync(".accept-btn");
    await page.WaitForSelectorAsync("#consent-modal", new() { State = WaitForSelectorState.Hidden });

    // Assert - Content is accessible
    var jobs = await page.QuerySelectorAllAsync(".job");
    Assert.True(jobs.Count > 0);
}
```

## Test Data

All scenarios use deterministic job postings from `TestData.cs`:
- **Total Jobs**: 500
- **Stable IDs**: `job-0000` to `job-0499`
- **Deterministic**: Always same data (seed: 42)
- **Realistic**: Titles, companies, locations, descriptions

## Logging

All scenarios emit structured logs with scenario IDs:

```
info: Ghost.Testing.Scenarios.Server.ScenarioServer[0]
      Scenario server started at http://localhost:52341
info: Ghost.Testing.Scenarios.Server.ScenarioRegistry[0]
      Scenario: consent/modal-blocking
```

## Design Decisions

1. **No External Dependencies**: All scenarios run locally, no internet required
2. **Deterministic Data**: Same seed = same job postings every time
3. **Scenario IDs in Logs**: Easy to correlate browser tests with server logs
4. **Dynamic Ports**: No port conflicts in parallel test execution
5. **Middleware Pipeline**: Reusable state management for common patterns

## Future Enhancements

- Session hydration delays
- Progressive rendering simulation
- Rate limiting scenarios
- CAPTCHA simulation
- Multi-page application navigation
- Form submission flows

## License

Internal testing infrastructure for Ghost project.
