# NordVPN Integration & Verification Plan

**Date:** 2026-01-28
**Goal:** Verify the new `StaticProxySource` logic via unit tests and configure the application to use NordVPN servers.

## 1. Unit Testing (`Ghost.Tests`)

Validate the parsing logic implemented in `StaticProxySource` to ensure it correctly handles the new fallback rules (Global Port/User/Pass).

### 1.1 Action
*   Run `dotnet test tests/Core/Ghost.Tests`.
*   **Success Criteria:** All 4 tests in `StaticProxySourceTests.cs` must pass.

## 2. Configuration (`appsettings.json`)

Update the WebApi config to use the NordVPN servers provided by the user.

### 2.1 Action
Update `src/Ghost.WebApi/appsettings.json`:
1.  Set `Proxy:Static:Enabled` to `true`.
2.  Set `Proxy:Static:Port` to `1080` (SOCKS5 default).
3.  Set `Proxy:Static:Username` and `Proxy:Static:Password` to placeholders.
4.  Populate `Proxy:Static:Items` with the list of NordVPN hosts (prefixed with `socks5://`).
5.  Set `Proxy:Api:Enabled` to `false` (to isolate NordVPN testing).

## 3. Verification

### 3.1 Build & Run
*   Build: `dotnet build`.
*   Run: `dotnet run --project src/Ghost.WebApi`.

### 3.2 Functional Test
*   Execute `GuestApi` search via `curl`.
*   **Expected Output:** Logs confirming "Loaded 12 static proxies".
*   **Note:** Connection failures (407/Auth) are expected until real credentials are used, but *proxy usage* is verified if we see rotation attempts.
