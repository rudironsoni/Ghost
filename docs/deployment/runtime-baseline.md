# Runtime Baseline

## Supported .NET Runtime

- Repository baseline targets `.NET 10` (`net10.0`).
- Container runtime images for first-party services MUST align with .NET 10.

## Container Artifacts

- Web API: `src/Ghost.WebApi/Dockerfile` uses `.NET 10 noble` images.
- Worker: `src/Ghost.Worker/Dockerfile` uses `.NET 10 noble` images.

## Operational Guidance

- Keep runtime image major version aligned with `global.json` and `Directory.Build.props` target framework baseline.
- Validate image tag drift during CI and before release.

## Admin and Metrics Endpoint Policy

- Admin and metrics endpoints are protected by API key policy when enabled.
- Configuration section: `Ghost:Security:AdminApiKey`.
- Environment overrides:
  - `Ghost__Security__AdminApiKey__Enabled`
  - `Ghost__Security__AdminApiKey__HeaderName`
  - `Ghost__Security__AdminApiKey__ApiKey`
  - or process-level fallback `GHOST_ADMIN_API_KEY`.
