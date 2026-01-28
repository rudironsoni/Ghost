# Plan: Ghost Server (Modular API Gateway)

**Date:** 2026-01-27
**Status:** Planned
**Objective:** Create a sophisticated, production-ready WebAPI (`Ghost.WebApi`) that acts as a configurable gateway for all Ghost capabilities, supporting dynamic feature flags and robust concurrency management.

---

## 1. Architecture: The Modular Monolith

We will replace the "Sample" project with a production-grade **Server** architecture.

-   **Project**: `src/Ghost.WebApi` (New project).
-   **Pattern**: **REPR** (Request-Endpoint-Response) using **FastEndpoints**. This replaces traditional Controllers with "Vertical Slices" (Feature Folders), ensuring better maintainability and testability.
-   **Deployment**: Single Docker container, behavior controlled entirely by Environment Variables.

### The "Universal Host" Concept
The Server references *all* known Platform extensions but only activates them if configured.

**Configuration (`appsettings.json`):**
```json
{
  "Ghost": {
    "Kernel": {
      "Headless": true,
      "MaxConcurrentSessions": 5  // <--- The Pool Limit (Semaphore)
    },
    "Extensions": {
      "LinkedIn": { "Enabled": true },
      "OpenAI": { "Enabled": false },
      "Anthropic": { "Enabled": false }
    }
  }
}
```

---

## 2. Scalability: Kernel-Level Pooling

To address your scalability concern ("pool of browsers"), we will implement **Semaphore-based Concurrency** directly in the Kernel.

**Changes:**
1.  **Update `KernelOptions`**: Add `int MaxConcurrentSessions { get; set; } = 10;`.
2.  **Update `GhostwriterKernel`**:
    *   Add `SemaphoreSlim _sessionLock`.
    *   In `NewSessionAsync()`: Wait for the semaphore (`_sessionLock.WaitAsync()`).
    *   On Session Dispose: Release the semaphore.

This guarantees that even if you get 1000 simultaneous API requests, only `N` browser contexts are active at once. The rest queue up efficiently. This matches the legacy `BrowserContextPool` behavior but is safer (managed by DI scope).

---

## 3. Implementation Steps

### Phase 1: Kernel Concurrency (The Pool)
- [ ] Add `MaxConcurrentSessions` to `KernelOptions`.
- [ ] Implement `SemaphoreSlim` logic in `GhostwriterKernel`.
- [ ] Ensure `DisposeAsync` reliably releases the semaphore.

### Phase 2: Ghost.Server Setup
- [ ] Delete `samples/Ghost.Sample.WebApi`.
- [ ] Create `src/Ghost.WebApi` (ASP.NET Core WebAPI).
- [ ] Add `FastEndpoints` and `FastEndpoints.Swagger` to `Directory.Packages.props`.
- [ ] Implement `Program.cs` with dynamic extension loading logic (iterating through config to find enabled extensions).

### Phase 3: Feature Slices (Endpoints)
- [ ] Create `Features/LinkedIn/SearchJobs/` (Endpoint, Request, Response).
    *   Route: `POST /api/linkedin/jobs/search`
    *   Logic: Injects `IJobClient`, calls `SearchJobsAsync`.
- [ ] Create `Features/LinkedIn/GetJob/` (Endpoint, Request, Response).
    *   Route: `GET /api/linkedin/jobs/{id}`

### Phase 4: Dockerization
- [ ] Create `Dockerfile` optimized for Playwright (using the official MS base image + playwright dependencies).
- [ ] Create `docker-compose.yml` example showing how to configure feature flags.

---

## 4. Usage Examples

**Scenario A: LinkedIn Source Only**
```bash
docker run -e GHOSTWRIGHT__EXTENSIONS__LINKEDIN__ENABLED=true \
           -e GHOSTWRIGHT__EXTENSIONS__OPENAI__ENABLED=false \
           ghostwright-server
```

**Scenario B: Full AI Automation**
```bash
docker run -e GHOSTWRIGHT__EXTENSIONS__LINKEDIN__ENABLED=true \
           -e GHOSTWRIGHT__EXTENSIONS__ANTHROPIC__ENABLED=true \
           ghostwright-server
```
