# Ghost WebAPI - Bruno OpenCollection

This is a Bruno OpenCollection (YAML format) for testing the Ghost WebAPI endpoints.

## Structure

```
Ghost-WebAPI-OpenCollection/
├── opencollection.yml          # Collection configuration
├── environments/
│   └── local.yml              # Environment variables
├── Health/                    # Health check endpoints
│   ├── folder.yml
│   ├── Basic Health Check.yml
│   ├── Jobs Health Check.yml
│   ├── Detailed Health Report.yml
│   ├── Platform Health.yml
│   ├── Proxy Health.yml
│   ├── Metrics Snapshot.yml
│   ├── Circuit Breakers Status.yml
│   └── Circuit Breaker by Platform.yml
├── Metrics/                   # Prometheus-style metrics
│   ├── folder.yml
│   ├── Get Metrics (JSON).yml
│   └── Get Metrics (Prometheus).yml
├── Jobs/                      # Job search endpoints
│   ├── folder.yml
│   ├── Search Jobs.yml
│   └── Search Jobs with Errors.yml
├── LinkedIn/                  # LinkedIn-specific endpoints
│   ├── folder.yml
│   ├── Get Job by ID.yml
│   ├── Search LinkedIn Jobs.yml
│   ├── Get Social Profile.yml
│   └── Search News.yml
└── Admin/                     # Administrative endpoints
    ├── folder.yml
    ├── Get DLQ Jobs.yml
    ├── Get DLQ Stats.yml
    └── Clear DLQ.yml
```

## Environment Variables

| Variable | Default Value | Description |
|----------|---------------|-------------|
| `baseUrl` | `http://localhost:8080` | Base URL for the API |
| `platform` | `LinkedIn` | Platform name for circuit breaker checks |
| `jobId` | `linkedin-12345` | Job ID for LinkedIn job lookup |
| `profileId` | `john-doe` | Profile ID for LinkedIn social profile lookup |
| `strategy` | `""` | Optional scraping strategy (GuestApi, BrowserPage, Hybrid) |
| `count` | `10` | Number of items to retrieve |

## Usage with Bruno CLI

### List all requests
```bash
cd Ghost-WebAPI-OpenCollection
bru list
```

### Run a single request
```bash
cd Ghost-WebAPI-OpenCollection
bru run "Health/Basic Health Check.yml" --env local
```

### Run all requests in a folder
```bash
cd Ghost-WebAPI-OpenCollection
bru run Health/ --env local
```

### Run entire collection
```bash
cd Ghost-WebAPI-OpenCollection
bru run --env local
```

### Run with output file
```bash
cd Ghost-WebAPI-OpenCollection
bru run --env local --output results.json
```

## API Endpoints

### Health (8 endpoints)
- Basic Health Check - `GET /health`
- Jobs Health Check - `GET /api/jobs/health`
- Detailed Health Report - `GET /api/health/detailed`
- Platform Health - `GET /api/health/platforms`
- Proxy Health - `GET /api/health/proxies`
- Metrics Snapshot - `GET /api/health/metrics`
- Circuit Breakers Status - `GET /api/health/circuit-breakers`
- Circuit Breaker by Platform - `GET /api/health/circuit-breakers/{platform}`

### Metrics (2 endpoints)
- Get Metrics (JSON) - `GET /api/metrics`
- Get Metrics (Prometheus) - `GET /api/metrics/prometheus`

### Jobs (2 endpoints)
- Search Jobs - `POST /api/jobs/search`
- Search Jobs with Errors - `POST /api/jobs/search-with-errors`

### LinkedIn (4 endpoints)
- Get Job by ID - `GET /api/linkedin/jobs/{jobId}`
- Search LinkedIn Jobs - `POST /api/linkedin/jobs/search`
- Get Social Profile - `GET /api/linkedin/social/profile/{profileId}`
- Search News - `POST /api/linkedin/news/search`

### Admin (3 endpoints)
- Get DLQ Jobs - `GET /api/admin/dlq`
- Get DLQ Stats - `GET /api/admin/dlq/stats`
- Clear DLQ - `POST /api/admin/dlq/clear`

## Format

This collection uses the **OpenCollection YAML** format, which is:
- Human-readable YAML files
- Git-friendly (easy diffing)
- Simple folder structure
- No proprietary binary formats

For more information about OpenCollection YAML, see: https://docs.usebruno.com/opencollection-yaml/overview
