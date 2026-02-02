# Proxy Pool & Rotation Architecture Plan

**Date:** 2026-01-28
**Goal:** Implement a robust, configurable proxy pool system supporting multiple sources (Static/NordVPN, API/Free) with rotation strategies.
**Requirement:** High quality, extensibility, >80% code coverage.

## 1. Architecture

We will separate the **source** of proxies from the **management** (rotation/distribution) of proxies.

### 1.1 Interfaces (`Ghost.Abstractions`)
- **`IProxySource`**: A source that can produce a list of proxies.
  - `Task<IEnumerable<ProxyInfo>> FetchProxiesAsync(CancellationToken ct);`
- **`IProxyProvider`**: (Existing) The service consumer interface.
  - `Task<ProxyInfo?> GetProxyAsync(string countryCode, CancellationToken ct);`
  - *Refactoring*: Will now be implemented by `RotatingProxyProvider` which aggregates sources.

### 1.2 Components (`Ghost.Services`)
- **`RotatingProxyProvider`**: The main implementation of `IProxyProvider`.
  - Injects `IEnumerable<IProxySource>`.
  - Maintains a concurrent collection of proxies.
  - Implements rotation (Round-Robin).
  - Refreshes proxies periodically (or on startup).
- **`StaticProxySource`**:
  - Reads proxies from `appsettings.json` (e.g., NordVPN SOCKS5 URLs).
- **`ApiProxySource`**:
  - Fetches proxies from a configured HTTP endpoint (e.g., ProxyScrape).
  - Replaces the hardcoded `FreeProxyProvider` logic.

### 1.3 Configuration (`appsettings.json`)
Structure:
```json
"Ghost": {
  "Proxy": {
    "Strategy": "RoundRobin",
    "Sources": {
      "Static": {
        "Enabled": true,
        "Items": [ "socks5://user:pass@host:port", "http://host:port" ]
      },
      "Api": {
        "Enabled": true,
        "Url": "https://api.proxyscrape.com/..."
      }
    }
  }
}
```

## 2. Implementation Steps

### 2.1 Core Changes
1.  Define `IProxySource` in `src/Core/Ghost/Abstractions`.
2.  Create `ProxyOptions` class to model the configuration structure.

### 2.2 Service Implementation
3.  Implement `StaticProxySource` (parses config strings to `ProxyInfo`).
4.  Implement `ApiProxySource` (uses `HttpClient` to fetch lists).
5.  Implement `RotatingProxyProvider` (aggregates sources, handles rotation).

### 2.3 Integration
6.  Update `Ghost.WebApi/Program.cs`:
    - Bind `ProxyOptions` from config.
    - Register `RotatingProxyProvider` as `IProxyProvider`.
    - Register enabled sources (`StaticProxySource`, `ApiProxySource`).
7.  Clean up: Remove old `FreeProxyProvider`.

## 3. Testing Strategy
- **Unit Tests (`Ghost.Core.Tests`)**:
  - `StaticProxySourceTests`: Verify parsing of various proxy string formats.
  - `RotatingProxyProviderTests`: Verify round-robin logic and aggregation from multiple sources.
  - `ApiProxySourceTests`: Mock `HttpClient` and verify parsing of API responses.

## 4. Verification
- **Build**: Clean compilation.
- **Run**: Verify `GuestApi` strategy uses proxies from the configured pool (logs will show "Using proxy: ...").
