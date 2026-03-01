# Plan 8: LinkedIn Platform Upgrade & Stealth Hardening

## Objective
Upgrade the `Ghost.Platform.LinkedIn` implementation to match and exceed the capabilities of the legacy `Ghostwright.LinkedIn` solution. This involves hardening stealth mechanics (geo-sync, canvas noise), enriching the data model (employment type, easy apply), and implementing human-like interaction patterns.

## 1. Core Abstractions & Stealth (`Ghost.Core`, `Ghost.Contracts`)

### 1.1 Data Model Enrichment
*   **File:** `src/Contracts/Ghost.Contracts.Jobs/DTOs/JobListing.cs`
*   **Change:** Add `public bool IsEasyApply { get; init; }`.

### 1.2 Configuration Enhancements
*   **File:** `src/Core/Ghost/Core/SessionOptions.cs`
*   **Change:** Add `TimezoneId` (string?) and `Locale` (string?) to allow session-level overrides (critical for proxy matching).
*   **File:** `src/Core/Ghost/Abstractions/Options/PageOptions.cs`
*   **Change:** Add `TimezoneId` and `Locale` to allow page-level overrides.

### 1.3 Advanced Stealth
*   **File:** `src/Core/Ghost/Stealth/StealthScripts.cs`
*   **Change:** Implement `InjectCanvasNoise` script. This script overrides `HTMLCanvasElement.prototype.toDataURL` and `CanvasRenderingContext2D.prototype.getImageData` to add imperceptible noise, preventing exact canvas fingerprinting.

### 1.4 Human-Like Interaction
*   **File:** `src/Core/Ghost/Extensions/HumanInteractionExtensions.cs` (New)
*   **Change:** Implement `HumanClickAsync` extension method for `IElement`.
    *   **Logic:** Uses a Bezier curve algorithm to simulate natural mouse movement from current position to target element before clicking. Adds micro-delays.

## 2. Platform Implementation (`Ghost.Platform.LinkedIn`)

### 2.1 Parsing Logic
*   **File:** `src/Platforms/Ghost.Platform.LinkedIn/Internal/JsonLdParser.cs`
*   **Change:**
    *   Map `employmentType` JSON-LD field to `JobListing.JobType` enum (FullTime, PartTime, Contract, etc.).
    *   Ensure `Salary` is correctly formatted (already present, but verify mapping).

### 2.2 Client "Brain" Upgrade
*   **File:** `src/Platforms/Ghost.Platform.LinkedIn/LinkedInJobClient.cs`
*   **Change 1 (Geo-Sync):**
    *   In `SearchJobsAsync` and `GetJobDetailsBrowserAsync`, detect if a Proxy is configured.
    *   If Proxy is detected (mock logic or simple heuristic for now, e.g., if proxy string contains specific codes), explicitly set `PageOptions.TimezoneId` and `Locale`.
    *   *Note:* Real-world implementation would look up the IP, but we will implement the infrastructure for it.
*   **Change 2 (Easy Apply):**
    *   Implement selector check for `Easy Apply` badge/icon in the browser scraping path.
    *   Populate `IsEasyApply`.
*   **Change 3 (Interaction):**
    *   Replace `ClickAsync` with `HumanClickAsync` for pagination and navigation.

## 3. Testing Strategy
*   **Target:** >80% Code Coverage.
*   **Unit Tests:**
    *   `JsonLdParserTests`: Verify JSON-LD -> JobListing mapping, including new Enum and Boolean fields.
    *   `StealthScriptsTests`: Verify script generation logic.
*   **Integration/Component Tests:**
    *   `LinkedInJobClientTests`: Mock `IBrowserSession` and `IPage`. Verify that `NewPageAsync` is called with the correct `TimezoneId`/`Locale` when proxy settings are present. Verify `IsEasyApply` extraction logic.

## 4. Verification
*   Run `dotnet test` to ensure all tests pass.
*   Run `dotnet run` (via a CLI entry point or script) to validate end-to-end behavior if applicable.
