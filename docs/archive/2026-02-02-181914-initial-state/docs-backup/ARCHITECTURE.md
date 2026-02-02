# Ghost Job Scraper Architecture Documentation

## Overview

The Ghost Job Scraper Reliability Enhancement introduces a robust, production-grade architecture designed to achieve near-100% reliability when scraping job listings from Indeed, Glassdoor, and Google Jobs. This system combines multi-strategy parsing, circuit breaker patterns, and comprehensive monitoring to handle anti-bot measures, parsing failures, and service degradation gracefully.

### Key Components

1. **Multi-Strategy Parsers** - Three-tier fallback system for resilient HTML parsing
2. **Circuit Breaker** - Polly-based resilience patterns for HTTP requests
3. **Monitoring Service** - Real-time metrics collection and health tracking
4. **DotnetSpider Integration** - Entity-based data extraction with XPath/CSS selectors

## Multi-Strategy Parser Architecture

### Three-Tier Strategy System

The parsers implement a cascading fallback mechanism to maximize parsing success rates:

```
┌─────────────────────────────────────────────────────────────┐
│                    ParseHtmlAsync()                         │
└──────────────────────┬──────────────────────────────────────┘
                       │
         ┌─────────────▼──────────────┐
         │  Strategy 1: DotnetSpider  │
         │  (Entity-based parsing)    │
         └─────────────┬──────────────┘
                       │ Success?
           ┌───────────┴───────────┐
           │                       │
          Yes                     No
           │                       │
           ▼                       ▼
    Return Results    ┌─────────────────────────┐
                      │ Strategy 2: JSON Parser │
                      │ (Original logic)        │
                      └────────────┬────────────┘
                                   │ Success?
                         ┌─────────┴─────────┐
                         │                   │
                        Yes                 No
                         │                   │
                         ▼                   ▼
                  Return Results  ┌──────────────────────┐
                                  │ Strategy 3: Regex    │
                                  │ (Heuristic parsing)  │
                                  └──────────┬───────────┘
                                             │
                                             ▼
                                      Return Results
```

### Content Classification

Before parsing, content is classified to optimize strategy selection:

- **JSON Response Format** - Content starts with `{` or `[`, minimal HTML markers
- **HTML Page Format** - Contains `<html>`, `<body>`, `<div>` tags
- **Mixed Content** - Contains both JSON and HTML structures
- **Unknown** - Cannot determine content type

### Platform-Specific Implementations

Each platform has tailored entity models with XPath/CSS selectors:

**IndeedJobEntity**:
```csharp
[EntitySelector(Expression = "//div[contains(@class,'job_seen_beacon')]", Type = SelectorType.XPath)]
public class IndeedJobEntity : EntityBase<IndeedJobEntity>
{
    [ValueSelector(Expression = "./@data-jk", Type = SelectorType.XPath)]
    public string? JobKey { get; set; }
    
    [ValueSelector(Expression = ".//h2[contains(@class,'jobTitle')]//span", Type = SelectorType.XPath)]
    [TrimFormatter]
    public string? Title { get; set; }
    // ... additional fields
}
```

**GlassdoorJobEntity** and **GoogleJobsEntity** follow similar patterns with platform-specific selectors.

## Circuit Breaker Architecture

### State Machine

The circuit breaker implements a three-state pattern using Polly:

```
                    ┌─────────────┐
                    │   Closed    │◄─────────────────────────────┐
                    │  (Normal)   │                              │
                    └──────┬──────┘                              │
                           │                                      │
              Failure      │                                      │ Success
              Threshold    │                                      │ in HalfOpen
              Exceeded     │                                      │
                           ▼                                      │
                    ┌─────────────┐     Duration      ┌───────────┴───┐
                    │    Open     │    Expired        │   HalfOpen    │
                    │  (Blocked)  │──────────────────►│  (Testing)    │
                    └─────────────┘                   └───────────────┘
                                                              │
                                                              │ Failure
                                                              │
                                                              ▼
                                                       ┌─────────────┐
                                                       │    Open     │
                                                       └─────────────┘
```

### Platform-Specific Configurations

Different platforms have different tolerance levels:

| Platform | Failure Threshold | Open Duration | Anti-Bot Sensitivity |
|----------|------------------|---------------|---------------------|
| Indeed   | 5 failures       | 30 seconds    | Lenient (official API) |
| Glassdoor| 3 failures       | 60 seconds    | Strict (anti-bot sensitive) |
| Google   | 4 failures       | 45 seconds    | Moderate |

### Integration Points

The circuit breaker wraps HTTP requests and parsing operations:

```csharp
public async Task<HttpResponseMessage> ExecuteHttpRequestAsync(
    string platformName,
    Func<Task<HttpResponseMessage>> requestFactory,
    CancellationToken cancellationToken = default)
{
    var policy = GetOrCreatePolicy(platformName);
    return await policy.ExecuteAsync(requestFactory);
}
```

## Monitoring Architecture

### Metrics Collection Pipeline

```
┌─────────────────┐     ┌──────────────────┐     ┌─────────────────┐
│  Request Made   │────►│ RecordRequest()  │────►│  Update Metrics │
└─────────────────┘     └──────────────────┘     └────────┬────────┘
                                                          │
                              ┌───────────────────────────┼───────────┐
                              │                           │           │
                              ▼                           ▼           ▼
                    ┌─────────────────┐        ┌─────────────────┐  ┌─────────────────┐
                    │ Success Count   │        │  Failure Count  │  │  Latency (ms)   │
                    └─────────────────┘        └─────────────────┘  └─────────────────┘
```

### Health Status Calculation

Health is calculated based on success rate over a rolling window:

- **Healthy**: ≥ 90% success rate
- **Degraded**: 70-90% success rate  
- **Unhealthy**: < 70% success rate

```csharp
private HealthStatus DetermineHealthStatus(double successRate)
{
    return successRate switch
    {
        >= 0.90 => HealthStatus.Healthy,
        >= 0.70 => HealthStatus.Degraded,
        _ => HealthStatus.Unhealthy
    };
}
```

### Alert Thresholds

Alerts trigger based on health status transitions:

- **Critical**: Success rate drops below 70%
- **Warning**: Success rate drops below 90% but above 70%
- **Info**: Circuit breaker state changes, proxy rotations

## Component Interactions

### Data Flow Diagram

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           Job Search Request                                │
└─────────────────────────────────┬───────────────────────────────────────────┘
                                  │
                                  ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                        SessionFactory 2.0                                   │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐        │
│  │  Hot Pool   │  │  Warm Pool  │  │  Cold Pool  │  │   Proxy     │        │
│  │  (<500ms)   │  │  (<1.5s)    │  │  (on-demand)│  │  Router     │        │
│  └─────────────┘  └─────────────┘  └─────────────┘  └─────────────┘        │
└─────────────────────────────────┬───────────────────────────────────────────┘
                                  │
                                  ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                     Circuit Breaker (Polly)                                 │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │  State: Closed │ Open │ HalfOpen                                    │   │
│  │  Metrics: Success/Failure/Rejection Counts                          │   │
│  └─────────────────────────────────┬───────────────────────────────────┘   │
└────────────────────────────────────┼────────────────────────────────────────┘
                                     │
                                     ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                        HTTP Request / Parsing                               │
└─────────────────────────────────┬───────────────────────────────────────────┘
                                  │
                    ┌─────────────┼─────────────┐
                    │             │             │
                    ▼             ▼             ▼
          ┌──────────────┐ ┌──────────┐ ┌──────────────┐
          │ DotnetSpider │ │   JSON   │ │    Regex     │
          │   (Primary)  │ │(Fallback)│ │ (Emergency)  │
          └──────────────┘ └──────────┘ └──────────────┘
                    │             │             │
                    └─────────────┴─────────────┘
                                  │
                                  ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                      Monitoring Service                                     │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐        │
│  │   Metrics   │  │    Health   │  │   Alerts    │  │  Dashboard  │        │
│  │  Collector  │  │   Checker   │  │   Trigger   │  │    Data     │        │
│  └─────────────┘  └─────────────┘  └─────────────┘  └─────────────┘        │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Integration Flow

1. **Request Initiation**: Job search request received
2. **Session Allocation**: SessionFactory assigns browser/proxy from tiered pool
3. **Circuit Protection**: Circuit breaker checks state before executing
4. **Execution**: HTTP request made or parsing performed
5. **Strategy Selection**: Multi-strategy parser selects optimal approach
6. **Metrics Recording**: Monitoring service records outcome
7. **Health Update**: Health status recalculated based on recent metrics
8. **Alert Evaluation**: Alert triggered if thresholds crossed

## Configuration Options

### Circuit Breaker Configuration

```json
{
  "CircuitBreaker": {
    "Platforms": {
      "Indeed": {
        "FailureThreshold": 5,
        "OpenDurationSeconds": 30,
        "TreatAntiBotAsFailure": false
      },
      "Glassdoor": {
        "FailureThreshold": 3,
        "OpenDurationSeconds": 60,
        "TreatAntiBotAsFailure": true
      },
      "Google": {
        "FailureThreshold": 4,
        "OpenDurationSeconds": 45,
        "TreatAntiBotAsFailure": true
      }
    }
  }
}
```

### Monitoring Configuration

```json
{
  "Monitoring": {
    "AlertThresholds": {
      "Healthy": 0.90,
      "Degraded": 0.70
    },
    "MetricsRetentionHours": 24,
    "HealthCheckIntervalSeconds": 30
  }
}
```

### Environment Variables

```bash
# Circuit Breaker
GHOST__CIRCUITBREAKER__INDEED__FAILURETHRESHOLD=5
GHOST__CIRCUITBREAKER__GLASSDOOR__FAILURETHRESHOLD=3

# Monitoring
GHOST__MONITORING__ALERTTHRESHOLDS__HEALTHY=0.90
GHOST__MONITORING__ALERTTHRESHOLDS__DEGRADED=0.70
```

## Performance Characteristics

### Target Metrics

| Component | Target | Measurement |
|-----------|--------|-------------|
| Hot Pool Acquisition | < 500ms | Time to acquire pre-warmed browser |
| Warm Pool Activation | < 1.5s | Time to activate pre-configured browser |
| Parser Strategy 1 | < 100ms | DotnetSpider entity parsing |
| Parser Strategy 2 | < 50ms | JSON parsing fallback |
| Parser Strategy 3 | < 200ms | Regex parsing fallback |
| Circuit Breaker Overhead | < 5ms | Policy wrapper cost |
| Monitoring Recording | < 1ms | Metrics update latency |

### Resource Usage

- **Memory**: ~50MB per browser instance in Hot pool
- **CPU**: Minimal overhead from circuit breaker (< 1%)
- **Network**: Connection pooling via SessionFactory sessions
- **Storage**: Metrics retained for 24 hours (configurable)

## Error Handling

### Graceful Degradation

When components fail, the system degrades gracefully:

1. **DotnetSpider Fails** → Falls back to JSON parser
2. **JSON Parser Fails** → Falls back to Regex parser
3. **Circuit Breaker Opens** → Returns cached data or empty results
4. **Session Unhealthy** → Automatically rotates to new session
5. **Proxy Fails** → Falls back to next proxy in chain

### Logging Strategy

All components use structured logging with correlation IDs:

```csharp
private static readonly Action<ILogger, string, Exception?> LogStrategyAttempt =
    LoggerMessage.Define<string>(
        LogLevel.Debug, 
        new EventId(2, "StrategyAttempt"), 
        "Attempting parsing strategy: {Strategy}");
```

## Deployment Considerations

### Prerequisites

- .NET 9.0 runtime
- Polly 8.0+ library
- DotnetSpider (included in solution)
- Sufficient memory for browser pools (Hot: 10-20 browsers, Warm: 5-10 browsers)

### Health Check Endpoints

```
GET /health - Overall system health
GET /health/platforms - Per-platform health status
GET /metrics - Prometheus-compatible metrics
GET /circuit-breakers - Circuit breaker states
```

### Monitoring Integration

The monitoring service exposes metrics compatible with:
- Prometheus (metrics endpoint)
- Grafana (dashboard templates)
- Application Insights (structured logging)
- Custom dashboards (JSON API)

## Summary

This architecture provides:
- **Resilience**: Multi-layer fallback strategies
- **Observability**: Comprehensive metrics and health tracking
- **Performance**: Tiered pools and optimized parsing
- **Maintainability**: Clear separation of concerns and configuration
- **Scalability**: Async/await throughout with minimal blocking

The system is designed to achieve >95% success rate while maintaining sub-second response times for the majority of requests.
