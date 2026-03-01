# Component Directory

Lightweight component inventory based on documentation and implementation.

## Core Platform

### Ghost.Core
**What:** Core abstractions, session management, proxy configuration
**Key Paths:**
- src/Core/Ghost/
- src/Core/Ghost/Abstractions/
- src/Core/Ghost/ProxyConfiguration/
**Key ADRs:**
- ADR-0001: Proxy Configuration System
**Key Dates:**
- 2026-01-28: Proxy pool system implementation
- See [CHANGELOG_BY_DATE.md](CHANGELOG_BY_DATE.md#2026-01-28)

### Ghost.Scraper.DotnetSpider
**What:** Multi-strategy web scraping with DotnetSpider integration
**Key Paths:**
- src/Core/Ghost.Scraper.DotnetSpider/
- src/Core/Ghost.Scraper.DotnetSpider/Resilience/
- src/Core/Ghost.Scraper.DotnetSpider/Monitoring/
**Key ADRs:**
- ADR-0003: DotnetSpider Integration
- ADR-0005: Multi-Strategy Parsing
**Key Dates:**
- 2026-02-01: CSRF token extraction for Glassdoor
- See [CHANGELOG_BY_DATE.md](CHANGELOG_BY_DATE.md#2026-02-01)

## Platform Integrations

### Ghost.Platform.LinkedIn
**What:** LinkedIn job scraping with browser-first strategy
**Key Paths:**
- src/Platforms/Ghost.Platform.LinkedIn/
**Key ADRs:**
- ADR-0004: Browser-First Strategy
**Key Dates:**
- 2026-01-27: Initial scraper plan
- 2026-01-28: Platform upgrade with timezone/locale spoofing
- See [CHANGELOG_BY_DATE.md](CHANGELOG_BY_DATE.md#2026-01-28)

### Ghost.Platform.Indeed
**What:** Indeed job search integration
**Key Paths:**
- src/Platforms/Ghost.Platform.Indeed/
**Key Dates:**
- 2026-01-31: Salary parsing improvements
- See [CHANGELOG_BY_DATE.md](CHANGELOG_BY_DATE.md#2026-01-31)

### Ghost.Platform.Glassdoor
**What:** Glassdoor job search with CSRF handling
**Key Paths:**
- src/Platforms/Ghost.Platform.Glassdoor/
**Key Dates:**
- 2026-02-01: CSRF token extraction
- See [CHANGELOG_BY_DATE.md](CHANGELOG_BY_DATE.md#2026-02-01)

### Ghost.Platform.Google
**What:** Google Jobs integration
**Key Paths:**
- src/Platforms/Ghost.Platform.Google/
**Key Dates:**
- 2026-02-01: Maintenance guide created
- See [CHANGELOG_BY_DATE.md](CHANGELOG_BY_DATE.md#2026-02-01)

### Ghost.Platform.X (Twitter)
**What:** X/Twitter provider (planned)
**Key Paths:**
- Planned: src/Platforms/Ghost.Platform.X/
**Key Dates:**
- 2026-02-03: Simulation plan created
- See [CHANGELOG_BY_DATE.md](CHANGELOG_BY_DATE.md#2026-02-03)

## Infrastructure

### Terraform Modules
**What:** Infrastructure as Code for AWS/GCP
**Key Paths:**
- infrastructure/terraform/
**Key Dates:**
- 2025-02-03: Ultra-miser infrastructure plan
- See [CHANGELOG_BY_DATE.md](CHANGELOG_BY_DATE.md#2025-02-03)

### Docker Compose
**What:** Local development orchestration
**Key Paths:**
- docker-compose.yml
- infrastructure/docker/
**Key Dates:**
- 2025-02-03: Initial infrastructure

## Documentation Categories

### Plans
**Location:** docs/archive/**/plans/
**Content:** Implementation plans, architecture designs
**Key Files:**
- complete-enhanced-scraper-plan.md
- job-scraper-reliability-enhancement.md
- x-provider-simulation-plan.md

### Notepads
**Location:** docs/archive/**/notepads/
**Content:** Working notes, decisions, learnings, issues
**Structure:**
- decisions.md - Architectural decisions
- learnings.md - Implementation learnings
- issues.md - Known issues and workarounds

### Maintenance Guides
**Location:** docs/archive/2026/02/02/docs_archive_backup/
**Content:** Operational documentation
**Files:**
- GLASSDOOR_MAINTENANCE.md
- GOOGLE_JOBS_MAINTENANCE.md
- RUNBOOK.md

## Cross-Component Relationships

```
Ghost.Core
├── Proxy Configuration ──▶ All Platforms
├── Session Management ──▶ All Platforms
└── Resilience ──▶ Scraper.DotnetSpider

Ghost.Scraper.DotnetSpider
├── Multi-strategy Parsing ──▶ Platform.Indeed
├── Circuit Breaker ──▶ All Platforms
└── Monitoring ──▶ Observability

Platforms
├── LinkedIn ──▶ Browser-First Strategy
├── Indeed ──▶ HTTP-First with Fallback
├── Glassdoor ──▶ CSRF Handling
└── Google ──▶ Async Bootstrap
```

## Document to Component Mapping

| Component | Primary Documents |
|-----------|------------------|
| Ghost.Core | 20260128-plan2-proxy-pool.md, ARCHITECTURE.md |
| Ghost.Scraper.DotnetSpider | job-scraper-reliability-enhancement.md |
| Platform.LinkedIn | 20260127-plan2-linkedin-world-class-scraper.md |
| Platform.Glassdoor | GLASSDOOR_MAINTENANCE.md |
| Platform.Google | GOOGLE_JOBS_MAINTENANCE.md |
