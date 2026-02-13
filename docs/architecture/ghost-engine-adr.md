# ADR: Ghost Engine Component Model and Lifecycle

## Status
Accepted

## Context
Ghost currently composes platform behavior through `IExtension` registrations in `Ghost.Hosting`.
That extension model is useful, but it does not yet define a stable execution ABI for a crawl runtime.

The next stage of work requires a core engine contract that is independent of host composition details and supports plugin portability.

## Decision
Ghost will introduce an explicit engine abstraction layer under `src/Engine/Ghost.Engine.Abstractions`.
The abstractions define execution lifecycle and component boundaries for:

- `IGhostEngine`: orchestrates spider execution.
- `IRequestScheduler`: frontier enqueue/dequeue and queue metrics.
- `IDownloader` and `IDownloaderMiddleware`: request/response execution pipeline.
- `ISpider` and `ISpiderMiddleware`: parse path for response-to-output transformation.
- `IItemPipeline`: post-parse item processing.
- `ISignalBus`: lifecycle/event publication and subscription.
- `IGhostSettings`: settings retrieval for engine/runtime configuration.

Core execution payload types are:

- `GhostRequest`
- `GhostResponse`
- `ItemEnvelope`
- `SpiderOutput`
- `GhostEngineContext`

## Non-negotiable runtime rules

1. Backpressure MUST be first-class in `IGhostEngine` implementations.
2. CancellationToken MUST flow through all async component APIs.
3. External I/O MUST be bounded by explicit timeout controls in implementations.
4. Plugin code MUST avoid depending on `Ghost.Hosting` internals.

## Lifecycle model

1. Host composes engine + components and constructs `GhostEngineContext`.
2. Engine calls `ISpider.StartRequestsAsync(...)` and schedules requests.
3. Engine dequeues frontier requests and invokes downloader middleware chain.
4. Engine obtains `GhostResponse`, invokes spider middleware, then `ISpider.ParseAsync(...)`.
5. Parse output requests are re-enqueued; items flow through `IItemPipeline` chain.
6. `ISignalBus` emits lifecycle and diagnostic events.
7. Engine completes when frontier and in-flight work are drained, or when cancelled.

## Migration plan

Phase PR1 only introduces contracts, ADR, and architecture guardrails.
It does not replace existing host behavior.

Subsequent phases will:

1. Introduce concrete engine runtime implementation.
2. Bridge hosting to engine lifecycle.
3. Migrate LinkedIn first as a compile-time plugin path.

## Consequences

- Positive: Clear, testable boundaries for engine and future plugins.
- Positive: Easier to enforce dependency direction in tests.
- Trade-off: Temporary duplication while legacy extension and new engine contracts coexist.
