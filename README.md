# Ghost

A sophisticated stealth browser automation framework with a pluggable extension architecture.

## Architecture

Ghost is organized as a monorepo with strict layering:

```
┌─────────────────────────────────────────┐
│              LAYER 4: SDK               │
│         Ghost.Sdk (meta-pkg)            │
└─────────────────────┬───────────────────┘
                      │
┌─────────────────────▼───────────────────┐
│           LAYER 3: HOSTING              │
│     Ghost.Hosting.{*,WebApi}            │
└─────────────────────┬───────────────────┘
                      │
┌─────────────────────▼───────────────────┐
│          LAYER 2: PLATFORMS             │
│  Anthropic │ Google │ LinkedIn │ OpenAI │
└─────────────────────┬───────────────────┘
                      │
┌─────────────────────▼───────────────────┐
│          LAYER 1: CONTRACTS             │
│      Ghost.Contracts.*                  │
└─────────────────────┬───────────────────┘
                      │
╔═════════════════════╧═══════════════════╗
║          LAYER 0: KERNEL                ║
║            Ghost                        ║
║  (Stealth browser - fully isolated)     ║
╚═════════════════════════════════════════╝
```

## Quick Start

```bash
# Install the SDK package (includes everything)
dotnet add package Ghost.Sdk
```

```csharp
using Ghost.Hosting;
using Ghost.Platform.Anthropic;
using Ghost.Platform.LinkedIn;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((ctx, services) =>
    {
        services.AddGhost(ghost =>
        {
            ghost.ConfigureKernel(k => k.Headless = true);
            ghost.UseExtension<AnthropicExtension>();
            ghost.UseExtension<LinkedInExtension>();
        });
    })
    .Build();

// Get services via DI
var inference = host.Services.GetRequiredService<IInferenceClient>();
var jobs = host.Services.GetRequiredService<IJobClient>();
```

## Configuration

All platform extensions are configured under the standardized `Ghost:Extensions` structure:

### appsettings.json

```json
{
  "Ghost": {
    "Extensions": {
      "LinkedIn": { "Enabled": true },
      "Indeed": { "Enabled": true },
      "Glassdoor": { 
        "Enabled": true,
        "Strategy": "BrowserFirst",
        "Timeout": 30000,
        "MaxRetries": 3,
        "DebugMode": false
      },
      "Google": { 
        "Enabled": true,
        "Strategy": "BrowserFirst",
        "Timeout": 30000,
        "MaxRetries": 3,
        "DebugMode": false
      },
      "InfoJobs": {
        "Enabled": true,
        "ClientId": "your_client_id",
        "ClientSecret": "your_client_secret"
      },
      "Tecnoempleo": {
        "Enabled": true,
        "ClientId": "your_client_id",
        "ClientSecret": "your_client_secret"
      }
    }
  }
}
```

### Environment Variables (.env)

```bash
GHOST__EXTENSIONS__LINKEDIN__ENABLED=true
GHOST__EXTENSIONS__INDEED__ENABLED=true
GHOST__EXTENSIONS__GLASSDOOR__ENABLED=true
GHOST__EXTENSIONS__GLASSDOOR__STRATEGY=BrowserFirst
GHOST__EXTENSIONS__GLASSDOOR__TIMEOUT=30000
GHOST__EXTENSIONS__GOOGLE__ENABLED=true
GHOST__EXTENSIONS__GOOGLE__STRATEGY=BrowserFirst
GHOST__EXTENSIONS__GOOGLE__TIMEOUT=30000
GHOST__EXTENSIONS__INFOJOBS__ENABLED=true
GHOST__EXTENSIONS__INFOJOBS__CLIENTID=your_client_id
GHOST__EXTENSIONS__INFOJOBS__CLIENTSECRET=your_client_secret
```

## Job Search Platforms

Ghost provides job search capabilities through multiple platforms, each with different characteristics and requirements.

### Supported Platforms

| Platform | Status | API Availability | Strategy | Reliability |
|----------|--------|------------------|----------|-------------|
| **LinkedIn** | ✅ Stable | Official API | Browser-first | High |
| **Indeed** | ✅ Stable | Official API | HTTP-first | High |
| **InfoJobs** | ✅ Stable | Official API | HTTP-first | High |
| **Google Jobs** | ⚠️ Enhanced | No public API | Browser-first | Medium |
| **Glassdoor** | ⚠️ Enhanced | API closed since 2020 | Browser-first | Medium |

### Google Jobs Platform

Google Jobs aggregates job listings from multiple sources but **does not provide a public API**. The official Google Jobs API was discontinued in 2021, leaving web scraping as the only viable approach.

#### Features
- **Browser-first strategy** with HTTP fallback for resilience
- **Retry logic with exponential backoff** for handling temporary failures
- **Dynamic parser** with multiple fallback strategies for HTML structure changes
- **Health check endpoint** at `/api/jobs/health` for monitoring
- **Structured error reporting** with actionable suggestions

#### Configuration Options

```json
{
  "Google": {
    "Enabled": true,
    "Strategy": "BrowserFirst",  // BrowserFirst, HttpFirst, BrowserOnly, HttpOnly
    "Timeout": 30000,            // Request timeout in milliseconds
    "MaxRetries": 3,             // Maximum retry attempts
    "DebugMode": false           // Save HTML responses to logs/
  }
}
```

#### Known Limitations
- **No official API**: Relies on web scraping which may break when Google changes their HTML structure
- **Anti-bot measures**: Google actively blocks automated requests
- **Rate limiting**: Conservative rate limiting to avoid IP bans
- **Consent pages**: May encounter cookie consent pages that require browser automation
- **Widget dependency**: Parser depends on Google's widget structure which can change

### Glassdoor Platform

Glassdoor provides job listings and company reviews but **closed their API to new partners in February 2020**. Only existing partners can access the official API.

#### Features
- **Browser-first strategy** with HTTP fallback for better resilience
- **Dynamic location resolution** for accurate geographic searches
- **Enhanced CSRF token extraction** with multiple fallback patterns
- **Retry logic with exponential backoff** for handling temporary failures
- **Health check endpoint** at `/api/jobs/health` for monitoring
- **Structured error reporting** with detailed diagnostics

#### Configuration Options

```json
{
  "Glassdoor": {
    "Enabled": true,
    "Strategy": "BrowserFirst",  // BrowserFirst, HttpFirst, BrowserOnly, HttpOnly
    "Timeout": 30000,            // Request timeout in milliseconds
    "MaxRetries": 3,             // Maximum retry attempts
    "DebugMode": false           // Save HTML/JSON responses to logs/
  }
}
```

#### Known Limitations
- **No public API**: API closed to new partners since February 2020
- **CSRF token dependency**: Requires valid CSRF tokens that change frequently
- **Location handling**: Complex location resolution that may not cover all geographic areas
- **GraphQL schema changes**: Backend API structure can change without notice
- **Anti-bot measures**: Glassdoor implements various measures to prevent automated access

## Troubleshooting

### Common Issues and Solutions

#### Google Jobs Issues

**Issue: Returns empty results**
- **Cause**: HTML structure changed or anti-bot detection triggered
- **Solution**: 
  1. Enable debug mode: `"DebugMode": true`
  2. Check logs for HTML structure analysis
  3. Try browser-only strategy: `"Strategy": "BrowserOnly"`
  4. Verify no rate limiting issues

**Issue: Consent page blocking**
- **Cause**: Google showing cookie consent page
- **Solution**: 
  1. Use browser-first strategy (handles consent automatically)
  2. Clear browser cookies between attempts
  3. Check if IP address is blocked

**Issue: Parser failures**
- **Cause**: Widget key or JSON structure changed
- **Solution**:
  1. Enable debug mode to see actual HTML structure
  2. Check logs/google_jobs_search.html for structure analysis
  3. Consider using third-party APIs like SerpApi

#### Glassdoor Issues

**Issue: CSRF token extraction fails**
- **Cause**: Token patterns changed in HTML
- **Solution**:
  1. Enable debug mode: `"DebugMode": true`
  2. Check logs/glassdoor_search_*.json for token extraction
  3. Try browser-first strategy for better token handling

**Issue: Location not respected**
- **Cause**: Location resolution failing or using hardcoded values
- **Solution**:
  1. Use common location names ("Remote", "Spain", "New York")
  2. Check logs for location resolution attempts
  3. Verify location parameters in GraphQL requests

**Issue: GraphQL errors**
- **Cause**: Query structure changed or authentication issues
- **Solution**:
  1. Enable debug mode to see actual GraphQL responses
  2. Check for error details in structured error reporting
  3. Try browser-first strategy as fallback

### Debug Mode and Logging

Enable debug mode to get detailed diagnostic information:

```json
{
  "Google": { "DebugMode": true },
  "Glassdoor": { "DebugMode": true }
}
```

Debug output includes:
- **HTML responses**: Saved to `logs/google_jobs_search.html`
- **JSON responses**: Saved to `logs/glassdoor_search_*.json`
- **CSRF token extraction**: Detailed logs of token finding attempts
- **Parser diagnostics**: Which strategies succeeded/failed
- **Network requests**: Headers, timing, and response codes

### Health Check Endpoint

Monitor platform health using the health check endpoint:

```bash
curl http://localhost:5000/api/jobs/health
```

Response includes:
- **Platform status**: healthy, degraded, or failing
- **Last successful search**: timestamp for each platform
- **Error categories**: Auth, Network, Parse, RateLimit
- **Suggestions**: Actionable recommendations for fixing issues

### Alternative Solutions

If scraping continues to fail, consider these alternatives:

#### Third-Party APIs (Recommended for Production)

**For Google Jobs**:
- **SerpApi** (~$50/month): `https://serpapi.com/google-jobs-api`
- **ScraperAPI** (~$49/month): Handles proxy rotation and CAPTCHAs
- **SearchApi.io** (~$40/month): Google Jobs-specific endpoint

**For Glassdoor**:
- **Apify** ($30/month): Pre-built Glassdoor scraper
- **RapidAPI** (pay-per-use): Real-time Glassdoor data
- **Mantiks**: Job postings API with Glassdoor data

#### Focus on Official API Platforms

Prioritize platforms with official APIs for better reliability:
- **LinkedIn** (already working well)
- **Indeed** (has official API)
- **InfoJobs** (already implemented)
- **ZipRecruiter** (has API)

### Legal and Ethical Considerations

⚠️ **Important Legal Notice**:

- **Google Jobs**: Scraping violates Google's Terms of Service. Google actively blocks automated requests.
- **Glassdoor**: API closed to new partners. Scraping likely violates Terms of Service.
- **Rate Limiting**: Always use conservative rate limits (max 1 request per 3 seconds)
- **Data Usage**: Only extract publicly visible data and link back to original sources
- **Compliance**: Consider using third-party APIs that handle legal compliance

**Recommended Approach**:
1. Use official API platforms when available (LinkedIn, Indeed, InfoJobs)
2. For Google Jobs/Glassdoor, consider third-party APIs for production use
3. Implement respectful rate limiting and error handling
4. Monitor for Terms of Service changes

## Packages

| Package                     | Description                              |
| --------------------------- | ---------------------------------------- |
| `Ghost`                     | Core stealth browser engine              |
| `Ghost.Contracts`           | Core interfaces (IBrowserSession, IPage) |
| `Ghost.Contracts.Inference` | IInferenceClient contract                |
| `Ghost.Contracts.Social`    | ISocialClient contract                   |
| `Ghost.Contracts.Jobs`      | IJobClient contract                      |
| `Ghost.Contracts.News`      | INewsClient contract                     |
| `Ghost.Platform.Anthropic`  | Claude via claude.ai                     |
| `Ghost.Platform.OpenAI`     | ChatGPT via chatgpt.com                  |
| `Ghost.Platform.Google`     | Gemini via gemini.google.com             |
| `Ghost.Platform.LinkedIn`   | LinkedIn automation                      |
| `Ghost.Hosting`             | DI and configuration                     |
| `Ghost.Hosting.WebApi`      | ASP.NET Core integration                 |
| `Ghost.Sdk`                 | Meta-package for quick start             |

## Infrastructure

Ghost Platform includes enterprise-grade infrastructure for production deployments.

### Quick Deploy

```bash
# Development (k3s, $50/month)
cd infrastructure/environments/development
terraform init && terraform apply

# Production (EKS HA, $500-800/month)
cd infrastructure/environments/production
terraform init && terraform apply
```

### Infrastructure Features

- **Kubernetes Platform**: EKS/k3s with auto-scaling, HPA, PDB
- **Security**: HashiCorp Vault, OPA Gatekeeper, Falco, Trivy scanning
- **Observability**: Prometheus, Grafana (6 dashboards), Loki, alerting
- **CI/CD**: GitHub Actions, Azure DevOps, ArgoCD GitOps
- **Multi-Environment**: Development, staging, production

See [infrastructure/README.md](infrastructure/README.md) for complete documentation.

## Building

```bash
# Restore and build
dotnet build Ghost.sln

# Run tests
dotnet test Ghost.sln

# Run tests with coverage
dotnet test Ghost.sln --collect:"XPlat Code Coverage"
```

## License

MIT
