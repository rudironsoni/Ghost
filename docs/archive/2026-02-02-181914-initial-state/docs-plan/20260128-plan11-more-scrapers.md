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









---









# ENHANCED PROMPT:

# Role: Senior Python Data Engineer (Scraping Specialist)
# Project: Ghost (Job Scraping Engine)
# Target Audience: A .NET Core Aggregator named "Jobtopus"
# Goal: Build a robust, multi-source job scraper that runs as a microservice.

## Context
"Ghost" is the dedicated scraping microservice for the Jobtopus ecosystem. It is responsible for sourcing job data from the wild and normalizing it into a clean stream for the main application.

## 1. Core Architecture
*   **Language:** Python 3.11+ (Leverage the rich scraping ecosystem).
*   **Framework:** FastAPI (for the REST interface) + Celery/Redis (optional, for async job queues) or simple background threads.
*   **Deployment:** Docker container.

## 2. Scraping Capabilities (The "JobSpy" Standard)
You must implement scraping logic for the following sources, prioritizing quality and "stealth":

*   **LinkedIn:**
    *   Implement guest-mode scraping (no auth required) where possible.
    *   Support authenticated scraping (cookie passing) as a fallback.
*   **Indeed:**
    *   **Challenge:** Cloudflare & aggressive bot detection.
    *   **Solution:** Use `selenium-stealth`, `nodriver`, or `playwright` with stealth plugins. Do not use raw `requests` for Indeed.
*   **Google Jobs:**
    *   Excellent source for aggregation. Less ban-prone.
*   **Glassdoor:**
    *   Good for salary validation.

## 3. The "Ghost" API Contract
Ghost must expose a REST API that Jobtopus will call to trigger scrapes.

### Endpoint: `POST /jobs/search`
**Request Body:**
```json
{
  "titles": ["Backend Engineer", ".NET Developer"],
  "locations": ["Remote", "New York"],
  "sources": ["linkedin", "indeed", "glassdoor"],
  "limit_per_source": 10
}
```

**Response:**
Returns a `task_id` (async) or a stream of results. For simplicity in V1, a synchronous list is acceptable if short, but async is preferred.

### Data Normalization (Crucial)
Output must map to this JSON structure to match Jobtopus:
```json
[
  {
    "title": "Senior Backend Developer",
    "company": "Tech Corp",
    "location": "Remote",
    "url": "https://indeed.com/viewjob?jk=...",
    "source": "Indeed",
    "description": "Full HTML or Markdown description...",
    "salary_raw": "$120k - $150k / year",  // SEND RAW STRINGS. Do not parse numbers.
    "date_posted": "2024-01-28T10:00:00Z"
  }
]
```

## 4. Technical Requirements
*   **Proxy Support:** The scraper must support a `PROXY_URL` env var to rotate IPs.
*   **Rate Limiting:** Implement random delays (jitter) between requests to avoid bans.
*   **Headless Mode:** Browser automation must run in headless mode suitable for Docker (Alpine/Debian).

## 5. Execution Plan
1.  Study the `JobSpy` repo (https://github.com/cullenwatson/JobSpy) for selector logic.
2.  Create the FastAPI wrapper.
3.  Implement the `IndeedScraper` and `LinkedInScraper` classes.
4.  Dockerize the application.
