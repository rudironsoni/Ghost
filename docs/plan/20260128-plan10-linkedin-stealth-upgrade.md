# LinkedIn Stealth & Anti-Blocking Upgrade Plan

**Date:** 2026-01-28
**Goal:** Implement advanced anti-blocking techniques (stealth, persistence, warm-up) for the LinkedIn scraper.
**Requirement:** High quality, no shortcuts, 80% code coverage.

## 1. Core Enhancements (`Ghost.Core`)

Enable the core engine to support persistent browser sessions and state management.

### 1.1 Session Configuration
- **File:** `src/Core/Ghost/Core/SessionOptions.cs`
- **Change:** Add `public string? StorageStatePath { get; set; }` property.

### 1.2 Kernel Update
- **File:** `src/Core/Ghost/Core/GhostKernel.cs`
- **Change:** In `NewSessionAsync`, map `SessionOptions.StorageStatePath` to Playwright's `BrowserNewContextOptions.StorageStatePath`. This enables loading cookies/state on startup.

### 1.3 Session Interface Update
- **File:** `src/Core/Ghost/Abstractions/IBrowserSession.cs`
- **Change:** Add method `Task SaveStorageStateAsync(string path);`.
- **File:** `src/Core/Ghost/Internal/BrowserSessionWrapper.cs`
- **Change:** Implement `SaveStorageStateAsync` using `_context.StorageStateAsync(new() { Path = path })`.

## 2. Platform Upgrades (`Ghost.Platform.LinkedIn`)

Implement specific logic to mimic human behavior and detect blocks.

### 2.1 Options Update
- **File:** `src/Platforms/Ghost.Platform.LinkedIn/LinkedInOptions.cs`
- **Change:**
  - Add `public bool WarmUpEnabled { get; set; } = true;`
  - Add `public string? StorageStatePath { get; set; }`

### 2.2 Rate Limit Detection (New Service)
- **File:** `src/Platforms/Ghost.Platform.LinkedIn/Internal/LinkedInRateLimitDetector.cs`
- **Responsibility:** Analyze page content for block indicators.
- **Logic:** Check for phrases like "security check", "challenge", "rate limit" and specific URLs (`/check/challenge`).
- **Output:** Throw `LinkedInRateLimitException` if detected.

### 2.3 Authenticator Refactoring
- **File:** `src/Platforms/Ghost.Platform.LinkedIn/Internal/LinkedInAuthenticator.cs`
- **Change:** Refactor `WarmUpAsync`.
- **Logic:**
  - Accept `IPage` (so it can be used on any session).
  - Visit random safe sites (Google, GitHub, Bing).
  - Implement random delays (2-5s) and scrolling behavior.

### 2.4 GuestJobSearch Integration
- **File:** `src/Platforms/Ghost.Platform.LinkedIn/Internal/GuestJobSearch.cs`
- **Change:**
  - Inject `IOptions<LinkedInOptions>` and `LinkedInAuthenticator`.
  - In `SearchAsync`:
    - Configure `SessionOptions.StorageStatePath` from `LinkedInOptions`.
    - Create isolated proxied session.
    - If `WarmUpEnabled`: Call `LinkedInAuthenticator.WarmUpAsync(page)`.
    - Perform search.
    - Check results with `RateLimitDetector`.
    - On success: Call `session.SaveStorageStateAsync(path)` to persist cookies.

## 3. Unit Testing Strategy

Achieve >80% code coverage.

### 3.1 Core Tests (`Ghost.Core.Tests`)
- Test `SessionOptions` mapping in `GhostKernel` (mock Playwright).
- Test `BrowserSessionWrapper` delegates calls correctly.

### 3.2 Platform Tests (`Ghost.Platform.LinkedIn.Tests`)
- **RateLimitDetectorTests**: Feed various HTML samples (blocked vs clean) and assert exceptions.
- **AuthenticatorTests**: Verify `WarmUpAsync` makes navigation calls (using Mock<IPage>).
- **GuestJobSearchTests**: Mock Kernel/Proxy/Page and verify flow (WarmUp called? Storage saved? Retry logic?).

## 4. Verification

1.  **Build**: Ensure clean compilation.
2.  **Test**: Run `dotnet test` and check coverage reports.
3.  **Run**: Execute `dotnet run` with `GuestApi` strategy and verify behavior via logs.
