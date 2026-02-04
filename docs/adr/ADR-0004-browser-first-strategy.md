# ADR-0004: Browser-First Strategy

## Status
Accepted (2026-01-28)

## Context
LinkedIn and similar platforms have sophisticated anti-bot detection. HTTP-first approaches often fail with 403/Captcha responses.

## Decision
Default to BrowserFirst strategy for platforms with strong anti-bot protection:
- LinkedIn: BrowserFirst with Puppeteer/Playwright
- Indeed: HTTP-first with browser fallback
- Glassdoor: BrowserFirst for JavaScript-heavy pages
- Use session pooling to amortize browser startup cost

## Alternatives Considered
1. HTTP-first for all platforms - Rejected: fails on LinkedIn
2. Browser-only - Rejected: too slow and resource-intensive
3. API-first - Rejected: limited platform support

## Consequences
- Positive: Higher success rate on protected platforms
- Positive: Can execute JavaScript for dynamic content
- Negative: Higher resource usage (memory, CPU)
- Negative: Slower than HTTP requests
- Negative: More complex infrastructure

## Evidence
- **Documents:**
  - docs/archive/2026/01/28/docs_plan/plan8-linkedin-platform-upgrade.md
  - docs/archive/2026/01/27/docs_plan/plan2-linkedin-world-class-scraper.md
- **Commits:**
  - 0cb2ed1 - feat: Implement timezone/locale spoofing, human interaction, LinkedIn enhancements
