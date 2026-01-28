# Role: Senior DotNet C# Data Engineer (Scraping Specialist)
# Project: Ghost (specially the Job Scraping Engine)
# Goal: Build a robust, multi-source job scraper that feeds the "Jobtopus" aggregator.

## Context
"Ghost" is the scraping engine for a larger ecosystem. It currently handles LinkedIn (maybe), but we need to upgrade it to be a "Universal Job Hunter" similar to the open-source library "JobSpy". Here are architectural requirements to build a world-class scraper engine.

## Core Requirements
1.  **Multi-Site Support:**
    *   **LinkedIn:** Continue supporting (or improve) guest/auth scraping.
    *   **Indeed:** Critical. Needs robust anti-bot handling (Cloudflare/Captcha avoidance).
    *   **Glassdoor:** Good for salary data.
    *   **Google Jobs:** Excellent aggregator, lower ban rate.
    *   *Bonus:* ZipRecruiter, RemoteOK.

2.  **Technology Stack:**
    *   **Python:** Preferred for this specific microservice (richer ecosystem for scraping than C#).
    *   **Playwright / Selenium:** Use Playwright (stealth plugin) for dynamic sites (Indeed/Glassdoor).
    *   **Requests/TLS:** Use `tls_client` or `curl_impersonate` for lighter API-based scraping where possible (e.g., specific LinkedIn endpoints).

3.  **Features (The "JobSpy" Standard):**
    *   **Proxy Rotation:** Support for rotating proxies to avoid IP bans.
    *   **User-Agent Rotation:** Randomize UAs to mimic real traffic.
    *   **Rate Limiting:** Intelligent delays (jitter) to behave like a human.
    *   **Standardized Output:** Regardless of the source (Indeed vs. LinkedIn), the output MUST be a standardized JSON object matching the `Jobtopus` schema (details below).

4.  **API Contract (Ghostwright Mode):**
    *   The service must expose a simple REST API (FastAPI/Flask) that `Jobtopus` can call.
    *   **Endpoint:** `GET /jobs/search?q={title}&l={location}&limit={n}`
    *   **Response:** JSON List of Job Objects.

## Data Schema (Crucial for Integration)
Ensure the scraper normalizes data into this structure:
```json
{
  "title": "Backend Engineer",
  "company": "Tech Corp",
  "company_url": "https://...",
  "location": "Remote, US",
  "salary_raw": "$120,000 - $160,000 a year",  // Do not parse, send raw string
  "description": "Full HTML or Markdown description...",
  "source": "Indeed",  // or "LinkedIn", "Glassdoor"
  "url": "https://indeed.com/viewjob?jk=...",
  "date_posted": "2024-01-28T10:00:00Z"
}
```

## Anti-Hallucination & Quality Control
*   **Deduping:** Generate a deterministic ID based on (Title + Company) to avoid sending duplicates in the same batch.
*   **Validation:** If a job is missing a Title or URL, discard it.

## Execution Plan for You
1.  Explore the `JobSpy` repo (https://github.com/cullenwatson/JobSpy) for logic on payload construction and specific site selectors.
2.  Implement a `ScraperFactory` pattern where `LinkedInScraper`, `IndeedScraper`, etc., all implement a common `IScraper` interface.
3.  Expose the aggregator via a FastAPI endpoint.
