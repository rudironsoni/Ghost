# Plan 4: Stealth Core Integration & Repository Professionalization

**Date:** 2026-01-28
**Status:** Implemented
**Goal:** Transform Ghost into a "Stealth-First" automation kernel and professionalize the repository with robust CI/CD and community standards.

## Phase 1: Deep Cleanup ("Ghostwriter" Removal)
Eliminate all remaining "Ghostwriter" references to ensure a consistent "Ghost" naming convention across the codebase.

- [x] **File Renames**:
    - `src/Core/Ghost/Core/GhostwriterKernel.cs` -> `GhostKernel.cs`
    - `src/Hosting/Ghost.Hosting/GhostwriterBuilder.cs` -> `GhostBuilder.cs`
- [x] **Content Refactoring**:
    - Replace `GhostwriterKernel` -> `GhostKernel`
    - Replace `GhostwriterBuilder` -> `GhostBuilder`
    - Replace `GhostwriterVersion` -> `GhostVersion` (Directory.Build.props)
    - Replace `GhostwriterLayer` -> `GhostLayer`
    - Generic cleanup of "Ghostwriter" in comments/strings.

## Phase 2: Stealth Core Engine
Port and adapt advanced evasion techniques from `soenneker.playwrights.extensions.stealth` directly into `Ghost.Core`.

### 1. Fingerprinting Engine (`src/Core/Ghost/Stealth`)
- [x] **`FingerprintProfile.cs`**:
    - Expand record to include: `Cores`, `Memory`, `Platform`, `ScreenW/H`, `ChromeVersion`, `Seed`, `Latitude`, `Longitude`, `TimeZone`, `VideoCardVendor`, `VideoCardRenderer`, `BatteryLevel`.
- [x] **`FingerprintGenerator.cs`**:
    - Implement `Generate()`:
        - Consistent RNG based on seed.
        - Coherent OS/GPU mapping (e.g., macOS -> Apple Silicon/Intel Iris).
        - Realistic Lat/Lon generation (NYC/SF bounding boxes).
        - Benchmarks (RTT, Downlink) randomization.
- [x] **`StealthScripts.cs`**:
    - Port JavaScript injections to C# Raw String Literals.
    - **Features**:
        - `navigator.webdriver` removal.
        - `navigator.hardwareConcurrency/deviceMemory` spoofing.
        - `navigator.userAgentData` (High-Entropy Client Hints).
        - WebGL Parameter spoofing (Vendor/Renderer).
        - Canvas readout noise.
        - WebRTC ICE candidate stripping.
        - Battery API mocking.
        - Permissions API latency simulation.

### 2. Kernel Integration (`src/Core/Ghost/Core`)
- [x] **`KernelOptions.cs`**:
    - Add `bool EnableStealth { get; set; } = true;`
    - Add `string[]? DisableStealthArgs { get; set; }` (for debugging).
- [x] **`GhostKernel.cs`**:
    - **Launch**: Automatically append stealth args (`--disable-blink-features=AutomationControlled`, `--enable-quic`, etc.).
    - **Session**:
        - In `NewSessionAsync`, generate `FingerprintProfile`.
        - Configure `BrowserNewContextOptions` (Viewport, UA, Timezone, Locale).
        - **Inject**: Call `context.AddInitScriptAsync(StealthScripts.GetScript(profile))`.

## Phase 3: Professional Workflows
Establish enterprise-grade CI/CD.

- [x] **GitHub Workflows**:
    - `build-and-test.yml`: PR validation, Restore/Build/Test (80% coverage check)/Pack.
    - `publish-package.yml`: Push to Main, Versioning, Publish to NuGet/GitHub.
- [x] **Configuration**:
    - Update `Directory.Build.props` for dynamic versioning.
    - Add `Directory.Packages.props` updates if needed.
- [x] **Community Files**:
    - `CODE_OF_CONDUCT.md`, `SECURITY.md`, `CONTRIBUTING.md`.
    - Issue Templates (`bug.yml`, `feature.yml`, etc.).

## Phase 4: Verification & Testing
- [x] **Unit Tests**:
    - `FingerprintGeneratorTests`: Verify coherence and randomness.
    - `StealthScriptTests`: Verify script generation syntax.
- [x] **Integration Tests**:
    - `GhostKernelTests`: Verify `AddInitScriptAsync` is called (via NSubstitute or actual browser check).
- [x] **Live Check**: Run a session against a detection site (optional/manual) or assert JS evaluation of spoofed properties.
- [x] **Coverage**: Ensure >80% code coverage.
