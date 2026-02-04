# Architectural Decision Records (ADRs)

Records of significant architectural decisions in the Ghost platform.

## Index

- [ADR-0001: Proxy Configuration System](./ADR-0001-proxy-configuration-system.md) - Flexible proxy management with health checking and rotation
- [ADR-0002: Retry Policy with Exponential Backoff](./ADR-0002-retry-policy.md) - Resilient retry strategies for platform APIs
- [ADR-0003: DotnetSpider Integration](./ADR-0003-dotnetspider-integration.md) - Structured HTML parsing with fallback strategies
- [ADR-0004: Browser-First Strategy](./ADR-0004-browser-first-strategy.md) - Browser-based scraping for anti-bot protected platforms
- [ADR-0005: Multi-Strategy Parsing](./ADR-0005-multi-strategy-parsing.md) - Three-tier fallback parsing approach
- [ADR-0006: Session Pooling with Tiered Browsers](./ADR-0006-session-pooling.md) - Browser pool management for performance
- [ADR-0007: Anti-Detection with Timezone/Locale Spoofing](./ADR-0007-anti-detection.md) - Comprehensive anti-detection measures

## Format

Each ADR follows the standard format:
- **Status**: Accepted/Rejected/Superseded
- **Context**: Problem statement and background
- **Decision**: What was decided and key components
- **Alternatives Considered**: Other options evaluated
- **Consequences**: Positive and negative impacts
- **Evidence**: Supporting documents, commits, and implementation references
