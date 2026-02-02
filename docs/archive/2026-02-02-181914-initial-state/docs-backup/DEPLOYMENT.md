# Deployment Guide

This guide covers deploying the **Ghost Web API** (`src/Ghost.WebApi`) to a VM or container environment.

> Notes:
> - The Web API targets **.NET 9** (`net9.0`).
> - Browser-first platforms (e.g., Google/Glassdoor/LinkedIn) may require significantly more CPU/RAM than HTTP-only workloads.

---

## 1) Prerequisites

### System requirements

Minimums (small/test):
- **CPU**: 2 vCPU
- **Memory**: 2–4 GB RAM (browser automation benefits from 4+ GB)
- **Disk**: 5+ GB free (plus space for logs and any debug artifacts under `logs/`)

Recommended (production-ish scraping):
- **CPU**: 4+ vCPU
- **Memory**: 8+ GB RAM (multiple concurrent browser sessions)
- **Disk**: 20+ GB free (logs + incident artifacts)

### Software dependencies

- **.NET 9 runtime** (or SDK if building on the host)
  - `global.json` pins the SDK line; the Web API project is `net9.0`.
- If deploying via container:
  - Docker / Docker Compose
  - A base image that includes browser dependencies (the repo Dockerfile uses `mcr.microsoft.com/playwright/dotnet`)
- Tooling for smoke tests:
  - `curl`
  - `jq` (optional but used by repo scripts)

### Network requirements

- **Inbound**: HTTP to the Web API (recommended behind a reverse proxy)
  - Common port choices: `8080` in containers, `5000` when running locally.
- **Outbound**: HTTPS to job platforms and any configured proxy providers.
- **DNS**: Reliable resolution (platforms rely on multiple domains/CDNs).

### Access requirements

- Ability to set **environment variables** / secrets at deploy time.
- Read access to deployment artifacts (published output or container image).
- If using reverse proxy: ability to update Nginx/Traefik config and reload.

---

## 2) Configuration Setup

Ghost uses standard ASP.NET configuration layering:

- `src/Ghost.WebApi/appsettings.json`
- `src/Ghost.WebApi/appsettings.{Environment}.json` (e.g., `appsettings.Development.json`)
- Environment variables (highest priority)

Additionally, the Web API loads a `.env` file early at startup using **DotNetEnv**:

```csharp
DotNetEnv.Env.TraversePath().Load();
```

That means a `.env` can be discovered in the working directory or parent directories.

### Required configuration files

Choose one approach:

1) **Environment variables only** (recommended for production)
2) **appsettings** files (good for static config)
3) **.env** (convenient locally; avoid storing secrets on disk in production unless you have an ops policy for it)

### Core environment variables

Common variables you will set:

```bash
# Environment
ASPNETCORE_ENVIRONMENT=Production

# Binding (recommended in containers)
ASPNETCORE_URLS=http://+:8080

# Kernel settings
Ghost__Kernel__Headless=true
Ghost__Kernel__MaxConcurrentSessions=5

# Enable/disable extensions
Ghost__Extensions__LinkedIn__Enabled=true
Ghost__Extensions__Indeed__Enabled=true
Ghost__Extensions__Glassdoor__Enabled=true
Ghost__Extensions__Google__Enabled=false
Ghost__Extensions__InfoJobs__Enabled=false
```

Platform-specific settings live under `Ghost:Extensions:*` (e.g., `Ghost:Extensions:Indeed:Country`).

### Proxy configuration

Proxy configuration is under `Ghost:Proxy`.

Example (appsettings-style):

```json
{
  "Ghost": {
    "Proxy": {
      "Strategy": "RoundRobin",
      "NordVPN": {
        "Enabled": true,
        "Type": "Socks5",
        "Username": "<set via secret store>",
        "Password": "<set via secret store>",
        "Hosts": [
          "socks5://<host>:1080"
        ]
      }
    }
  }
}
```

Important:
- Prefer injecting proxy credentials via a secret manager rather than committing them to config files.
- Validate proxies before production rollout (unstable proxies look like platform failures).

### Platform-specific settings

Examples:

```bash
# Country targeting examples
Ghost__Extensions__Indeed__Country=ES
Ghost__Extensions__Glassdoor__Country=ES

# LinkedIn strategy example
Ghost__Extensions__LinkedIn__ScrapingStrategy=Hybrid
```

### Reverse proxy (recommended)

If you deploy behind Nginx, ensure these are supported:
- Request timeouts large enough for browser-first operations (e.g., 60–120s)
- Forwarded headers (`X-Forwarded-For`, `X-Forwarded-Proto`)
- Response buffering rules appropriate for streaming/large payloads (if enabled later)

Minimal Nginx location (example):

```nginx
location / {
  proxy_pass http://127.0.0.1:8080;
  proxy_set_header Host $host;
  proxy_set_header X-Forwarded-Proto $scheme;
  proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
  proxy_read_timeout 120s;
}
```

---

## 3) Deployment Steps

### Option A: Docker Compose (fastest)

The repo includes `docker-compose.yml` wiring the Web API build.

1. Build and start:

```bash
docker compose up --build -d
```

2. Ensure the service is reachable:

```bash
curl -i http://localhost:8080/health
```

If your container is not listening on `8080`, set:

```bash
ASPNETCORE_URLS=http://+:8080
```

in compose `environment:`.

### Option B: Host publish + systemd (VM deployment)

1. Build and publish:

```bash
dotnet restore
dotnet publish src/Ghost.WebApi/Ghost.WebApi.csproj -c Release -o /opt/ghost-webapi
```

2. Create a systemd unit (example):

```ini
[Unit]
Description=Ghost Web API
After=network.target

[Service]
WorkingDirectory=/opt/ghost-webapi
ExecStart=/usr/bin/dotnet /opt/ghost-webapi/Ghost.WebApi.dll
Restart=always
RestartSec=5
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://+:8080

# Optional: load env vars from a file you manage securely
# EnvironmentFile=/etc/ghost-webapi/env

[Install]
WantedBy=multi-user.target
```

3. Start:

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now ghost-webapi
sudo systemctl status ghost-webapi
```

### Deployment order (recommended)

1) Secrets/config (env vars, secret store) → 2) Reverse proxy → 3) App deploy → 4) Health checks → 5) Smoke tests.

### Health check verification

```bash
# Basic process readiness
curl -i http://localhost:8080/health

# Functional platform probe (aggregated)
curl -s http://localhost:8080/api/jobs/health | jq .
```

### Smoke tests

Use the repo scripts (they assume an API URL; adjust `API_URL` inside scripts if needed):

```bash
chmod +x examples/scripts/**/**/*.sh

./examples/scripts/health/health-check.sh
./examples/scripts/validate/validate-api.sh
```

If a platform is flaky, temporarily disable it rather than letting it degrade overall behavior:

```bash
Ghost__Extensions__Google__Enabled=false
Ghost__Extensions__Glassdoor__Enabled=false
```

---

## 4) Health Check Endpoints

### `GET /health`

- **Purpose**: ASP.NET Core health checks (process readiness).
- **Expected**: HTTP **200** when healthy.
- **Body**: default health check output (often plain text). Treat **HTTP status** as the primary signal unless you customize the response writer.

Example:

```bash
curl -i http://localhost:8080/health
```

### `GET /api/jobs/health`

- **Purpose**: Functional health check that runs a small test job search per enabled platform.
- **Expected**: HTTP **200** always; interpret the JSON field `overallStatus`.

Example:

```bash
curl -s http://localhost:8080/api/jobs/health | jq .
```

Interpretation:
- `overallStatus: "healthy"` → all platforms returned results
- `overallStatus: "degraded"` → at least one platform returned **no jobs** (responded but empty)
- `overallStatus: "unhealthy"` → one or more platforms threw exceptions

Per-platform fields:
- `platforms.{Name}.status`: `healthy | degraded | unhealthy | unknown`
- `responseTimeMs`, `jobsFound`, `lastSuccessfulSearch`

### Optional: `GET /health/ghost` (if you wire it)

The hosting package provides `MapGhostHealthCheck()` which returns `{"status":"ok"}` at `/health/ghost`.
It is **not mapped by default** in the current Web API startup; wire it if you want a stable JSON readiness endpoint.

---

## 5) Monitoring Setup

### Metrics endpoints

The current Web API project maps health endpoints but does **not** expose a Prometheus `/metrics` endpoint out of the box.

Recommended options:
- **Logs-first monitoring**: ship stdout/stderr (container logs or journald) to your log backend and alert on error rates and health degradation.
- **Add OpenTelemetry** (traces + metrics) and export to your APM.
- If you want Prometheus scraping, add an instrumentation library and map `/metrics` explicitly.

### Dashboard configuration

At minimum, track:
- Request rate and latency for `POST /api/jobs/search`
- `GET /api/jobs/health` overall status counts (healthy/degraded/unhealthy)
- Error logs grouped by platform (Google/Glassdoor/LinkedIn/Indeed)
- Resource usage: CPU, memory, container restarts

### Alert setup (practical defaults)

- **Critical**: `/health` not returning 200 for 2–5 minutes.
- **Critical**: `/api/jobs/health` returns `overallStatus != healthy` for 10+ minutes.
- **Warning**: increased `degraded` responses (empty results) for a single platform.
- **Warning**: memory pressure / OOM kills (browser pools).

---

## 6) Rollback Procedures

### When to rollback

Rollback if any of the following is true after deployment:
- `/health` fails (service not ready)
- `/api/jobs/health` moves from healthy to unhealthy and stays there
- Error rate spikes or you observe platform bans/anti-bot escalation
- Resource usage (CPU/RAM) becomes unstable (restarts/OOM)

### How to rollback

**Docker**:
1. Re-deploy the previous image tag.
2. Restart the service.

```bash
docker compose pull
docker compose up -d
```

**Systemd / host publish**:
1. Keep releases versioned (e.g., `/opt/ghost-webapi/releases/2026-02-02-1/`).
2. Re-point the symlink `/opt/ghost-webapi/current` to the previous release.
3. Restart the service.

```bash
sudo systemctl restart ghost-webapi
sudo systemctl status ghost-webapi
```

### Verification after rollback

1. Confirm readiness:

```bash
curl -i http://localhost:8080/health
```

2. Confirm functional status:

```bash
curl -s http://localhost:8080/api/jobs/health | jq .overallStatus
```

3. Run smoke tests:

```bash
./examples/scripts/health/health-check.sh
./examples/scripts/validate/validate-api.sh
```
