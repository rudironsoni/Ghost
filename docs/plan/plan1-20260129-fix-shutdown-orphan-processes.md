# Plan: Fix Ghost WebApi Shutdown and Orphaned Processes

**Date:** 2026-01-29
**Status:** Approved
**Goal:** Ensure `Ghost.WebApi` and child Playwright processes shut down cleanly, releasing port 5000 and freeing resources.

## Root Cause Analysis
- `GhostKernel` is a Singleton but not automatically disposed because the Host doesn't trigger disposal for Singletons unless they are `IHostedService` or explicit shutdown hooks are used.
- `Ghost.WebApi` (PID 1389152) survives `pkill -f "dotnet run"` because it's a child process detached or not signaling up.
- Playwright browsers remain as orphans.

## Implementation Steps

### 1. Robust Disposal in `GhostKernel.cs`
- Add `AppDomain.CurrentDomain.ProcessExit` handler to catch SIGTERM/SIGKILL (where possible).
- Enhance `DisposeAsyncCore` to aggressively close/kill the browser.
- Ensure `_browser.CloseAsync()` is called before `DisposeAsync()`.

### 2. Create `GhostKernelHostedService`
- Location: `src/Hosting/Ghost.Hosting/GhostKernelHostedService.cs`
- Implements `IHostedService`.
- `StartAsync`: No-op (Kernel is lazy or initialized elsewhere, or we can warm it up here).
- `StopAsync`: Calls `GhostKernel.DisposeAsync()`.
- Register `IHostApplicationLifetime.ApplicationStopping` callback as a safety net.

### 3. Register Service in `GhostBuilder.cs`
- Add `services.AddHostedService<GhostKernelHostedService>()` in `Build()`.

### 4. Unit Testing & Coverage (Target: 80%+)
- Create `tests/Ghost.Core.Tests` (xUnit) if not exists.
- Add tests for:
    - `GhostKernel` lifecycle (Create, Dispose).
    - `GhostKernelHostedService` calling Dispose.
    - `GhostBuilder` registration.
- Verify shutdown behavior.

### 5. Verification
- Build solution.
- Run `Ghost.WebApi`.
- Verify port 5000 is open.
- Kill process.
- Verify port 5000 is released and no `playwright` processes remain.

## Detailed Code Changes

### `src/Core/Ghost/Core/GhostKernel.cs`
- Modify `Dispose(bool)` and `DisposeAsyncCore`.
- Add finalizer safety? No, `IDisposable` pattern is enough if called. The issue is *calling* it.

### `src/Hosting/Ghost.Hosting/GhostKernelHostedService.cs`
```csharp
public class GhostKernelHostedService : IHostedService
{
    // ... impl ...
}
```

### `src/Hosting/Ghost.Hosting/GhostBuilder.cs`
- `_services.AddHostedService<GhostKernelHostedService>();`

## Verification Command
```bash
dotnet run --project src/Ghost.WebApi &
PID=$!
sleep 5
kill $PID
lsof -i :5000
```
