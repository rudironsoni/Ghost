# Plan: SOCKS5 Auth Bridge & Ultimate Stealth

**Date:** 2026-01-28
**Goal:** Enable Authenticated SOCKS5 (NordVPN) on Chromium while maximizing stealth and eliminating leaks.

## 1. The Stealth Challenge
*   **Problem:** Chromium cannot natively authenticate SOCKS5 proxies (`socks5://user:pass@host`).
*   **Stealth Risk:** Using a local proxy bridge can inadvertently leak local network info via WebRTC if not configured correctly.
*   **Requirement:** "Completely undetectable."

## 2. Solution Architecture

### 2.1 Component: `Socks5Bridge` (Sidecar)
A lightweight, transparent TCP proxy implemented in `Ghost.Net`.
*   **Listening:** `127.0.0.1:{RandomPort}`.
*   **Protocol:** SOCKS5 (No Auth on local side).
*   **Upstream:** Connects to NordVPN/Target Proxy (User/Pass Auth).
*   **Behavior:** Pipes raw bytes. Does not modify headers or traffic (preserving TLS fingerprints).

### 2.2 Integration: `GhostKernel`
*   **Logic:**
    *   Detects `Chromium` + `Socks5` + `Auth`.
    *   Spins up `Socks5Bridge`.
    *   Configures Playwright to use `socks5://127.0.0.1:{BridgePort}`.
    *   Manages Bridge lifecycle (Dispose with session).

## 3. Stealth Hardening (`GhostKernel`)

To satisfy the "uttermost stealth" requirement, we must tighten the browser fingerprint, specifically around proxy usage.

### 3.1 WebRTC Leak Prevention
Add Chromium arguments to force WebRTC traffic through the proxy or disable non-proxied UDP.
*   `--webrtc-ip-handling-policy=disable_non_proxied_udp`
*   `--force-webrtc-ip-handling-policy=disable_non_proxied_udp`
*   `--enforce-webrtc-ip-permission-check`

### 3.2 Fingerprint Noise
Ensure `enableStealth` adds noise to:
*   Canvas / WebGL (Already in `StealthScripts`, verify integration).
*   AudioContext.

## 4. Implementation Steps

### 4.1 `Socks5Bridge.cs`
Implement using `System.Net.Sockets`.
*   `StartAsync()`: Bind listener.
*   `HandleClientAsync()`:
    1.  Handshake Client (No Auth).
    2.  Connect Upstream.
    3.  Handshake Upstream (User/Pass).
    4.  `Task.WhenAll(CopyToAsync(client, remote), CopyToAsync(remote, client))`.

### 4.2 `GhostKernel.cs` Updates
*   Add WebRTC flags to `launchArgs` if `opts.EnableStealth`.
*   Inject Bridge logic in `NewSessionAsync`.

### 4.3 `BrowserSessionWrapper.cs`
*   Add `IAsyncDisposable _bridge` field to ensure cleanup.

## 5. Verification
1.  **Connectivity:** Run `GuestApi` search with NordVPN config. Success = Jobs returned (or at least no "Browser not supported" error).
2.  **Stealth:** Check logs/behavior.
