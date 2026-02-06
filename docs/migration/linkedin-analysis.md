# LinkedIn Implementation Migration Analysis

**Date:** February 5, 2026  
**Source:** Ghost.Platform.LinkedIn  
**Target:** Ghost.Sdk.Spider  

---

## Executive Summary

This document provides a comprehensive analysis of the current LinkedIn scraping implementation to support migration to the Ghost.Sdk.Spider framework. The current implementation uses a multi-strategy approach combining Guest API and Browser automation with fallback mechanisms.

---

## 1. FIELDS BEING EXTRACTED

### 1.1 JobListing Record Fields (Ghost.Contracts.Jobs)

| Field | Type | Required | Source Priority |
|-------|------|----------|-----------------|
| `Id` | string | Yes | URL extraction / data-entity-urn |
| `Title` | string | Yes | JSON-LD -> DOM selectors |
| `Company` | string | Yes | JSON-LD -> DOM selectors -> Regex fallback |
| `Location` | string? | No | JSON-LD -> DOM selectors -> Regex fallback |
| `Description` | string? | No | JSON-LD -> DOM selectors |
| `Salary` | string? | No | JSON-LD -> DOM selectors |
| `JobType` | JobType | No | Criteria list -> JSON-LD |
| `ExperienceLevel` | ExperienceLevel | No | Criteria list -> JSON-LD |
| `PostedAt` | DateTimeOffset | No | time[datetime] -> Relative text parsing |
| `Remote` | bool | No | (Not actively extracted - default false) |
| `Url` | string? | No | Constructed from base + jobId |
| `Source` | string? | No | Hardcoded "LinkedIn" |
| `IsEasyApply` | bool | No | Button detection via selector |

### 1.2 Supporting Enums

```csharp
// JobType
public enum JobType { Unknown, FullTime, PartTime, Contract, Internship }

// ExperienceLevel  
public enum ExperienceLevel { Unknown, EntryLevel, MidLevel, Senior, Manager }
```

---

## 2. SELECTORS DOCUMENTATION

### 2.1 Job Search Results Page Selectors (List View)

**Container Selectors (for finding job cards):**
```css
.jobs-search-results__list-item
.jobs-search__results-list li
.base-card
```

**Field Selectors (within each job card):**

| Field | Selectors (in priority order) |
|-------|------------------------------|
| **ID** | `[data-id]`, `[data-entity-urn]` (attribute extraction) |
| **Title** | `.job-card-list__title`, `.base-search-card__title` |
| **Company** | `.job-card-container__company-name`, `.base-search-card__subtitle` |
| **Location** | `.job-card-container__metadata-item`, `.job-search-card__location` |
| **Link/URL** | `a.base-card__full-link`, `a.job-card-list__title` (href attribute) |

**ID Extraction Patterns (Regex):**
```regex
# From data-entity-urn attribute
data-entity-urn="urn:li:jobPosting:(?<id>[0-9]+)"

# From URL path
/jobs/(?:view|r)/(?<id>[0-9]+)

# From query parameters
[?&](?:jobId|id)=(?<id>[0-9]+)

# From job URL ending
-(\d{6,})(?:\?|$)
```

### 2.2 Job Details Page Selectors (Deep Fetch)

**Title Selectors:**
```css
.top-card-layout__title
.job-details-jobs-unified-top-card__job-title
h1
.job-card-list__title
.top-card-layout__entity-info h1
```

**Company Selectors:**
```css
.top-card-layout__first-subline .topcard__org-name-link
.job-details-jobs-unified-top-card__company-name
.topcard__org-name-link
.job-card-container__company-name
.top-card-layout__company-url
a[data-tracking-control-name='public_jobs_topcard-org-name']
```

**Location Selectors:**
```css
.top-card-layout__first-subline .topcard__flavor--bullet
.job-details-jobs-unified-top-card__bullet
.topcard__flavor--bullet
.job-search-card__location
.job-card-container__metadata-item
.top-card-layout__first-subline .topcard__flavor:not(.topcard__org-name-link)
```

**Description Selectors:**
```css
.show-more-less-html__markup
#job-details
.description__text
.job-description
.core-section-container__content
```

**Salary Selectors:**
```css
.main-job-card__salary-info
.job-details-jobs-unified-top-card__salary
.job-details-jobs-unified-top-card__salary-info
.description__job-criteria-item--salary
.salary-range
.salary
.job-criteria__item--salary
```

**Job Criteria Selectors (for JobType & ExperienceLevel):**
```css
.description__job-criteria-list .description__job-criteria-item
.description__job-criteria-list li
.job-details-jobs-unified-top-card__job-insight
```

**Posted Date Selectors:**
```css
time[datetime]
time
.posted-time-ago__text
.topcard__flavor--metadata time
.job-details-jobs-unified-top-card__posted-date
span.posted-time-ago__text
```

**Easy Apply Button Detection:**
```css
.jobs-apply-button--top-card button
.jobs-s-apply button
```
(Text content check for "Easy Apply")

### 2.3 Regex Fallback Patterns

```regex
# Company extraction fallback
class="[^"]*topcard__org-name-link[^"]*">\s*([^<]+)\s*<

# Location extraction fallback  
class="[^"]*topcard__flavor--bullet[^"]*">\s*([^<]+)\s*<

# Total results count
results-context-header__job-count"[^>]*>(?<count>[0-9,]+)

# Relative time parsing (e.g., "3 days ago")
(?<n>\d+)\s*(minute|minutes|hour|hours|day|days|week|weeks|month|months|year|years)\s*ago
```

---

## 3. MULTI-STRATEGY APPROACH

### 3.1 Strategy Enumeration

```csharp
public enum JobScrapingStrategy
{
    GuestApi,      // Use LinkedIn's guest API endpoints
    BrowserPage,   // Full browser automation
    Hybrid         // Try GuestApi first, fallback to Browser
}
```

### 3.2 Guest API Strategy

**Endpoints:**
- Search: `{domain}/jobs-guest/jobs/api/seeMoreJobPostings/search?keywords={q}&location={loc}&start={offset}`
- Job Details: `{domain}/jobs-guest/jobs/api/jobPosting/{jobId}`

**Query Parameters:**
- `keywords` - URL-encoded search terms
- `location` - URL-encoded location
- `start` - Pagination offset
- `f_TPR` - Time filter (r86400=24h, r604800=week, r2592000=month)

**Extraction Flow:**
1. Navigate to guest API endpoint
2. Extract job IDs from HTML (data-entity-urn patterns)
3. For each ID, fetch details page
4. Parse JSON-LD structured data
5. Fallback to DOM scraping if JSON-LD incomplete

### 3.3 Browser Strategy

**Navigation:**
- Search URL: `{baseUrl}/jobs/search?keywords={q}&location={loc}`
- Details URL: `{baseUrl}/jobs/view/{jobId}`

**Flow:**
1. Clear cookies for fresh session
2. Navigate to search page
3. Extract job cards from results
4. Deep fetch each job details page
5. Parse using JSON-LD + DOM fallback

### 3.4 Hybrid Strategy

**Execution Order:**
1. Attempt Guest API search
2. If results found -> fetch details for each -> yield results
3. If no results or failure:
   - Log fallback event
   - Add 2-second safety delay
   - Clear browser cookies
   - Execute Browser strategy

---

## 4. DEPENDENCIES

### 4.1 Direct Dependencies

| Dependency | Type | Purpose |
|------------|------|---------|
| `Ghost.IBrowserSession` | Injected | Browser automation abstraction |
| `IOptions<LinkedInOptions>` | Injected | Configuration |
| `ILogger<LinkedInJobClient>` | Injected | Structured logging |
| `IGuestJobSearch` | Injected | Guest API implementation |
| `LinkedInSessionPool` | Injected | Session pooling for Guest API |
| `ICountryDomainProvider` | Injected | Domain resolution per country |

### 4.2 Session Pool (LinkedInSessionPool)

**Purpose:** Manages pooled browser sessions for Guest API strategy

**Features:**
- Acquire/Release pattern
- Proxy rotation support
- Session reuse for multiple requests
- Storage state persistence

### 4.3 Rate Limiting

**Components:**
- `LinkedInRateLimitDetector` - Checks for rate limit pages
- Retry logic with up to 3 attempts
- Session pool rotation on Playwright failures

**Rate Limit Detection:**
- Checks for "429 Too Many Requests" in response
- Monitors page content for throttling indicators

### 4.4 Country Domain Resolution

**Configuration:**
```csharp
public CountryCode Country { get; set; } = CountryCode.US;
```

**Supported Domains:**
- Resolves country-specific LinkedIn domains (e.g., ES -> es.linkedin.com)

### 4.5 LinkedInOptions Configuration

```csharp
public sealed class LinkedInOptions
{
    public string BaseUrl { get; set; } = "https://www.linkedin.com";
    public TimeSpan PageLoadTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public JobScrapingStrategy ScrapingStrategy { get; set; } = JobScrapingStrategy.GuestApi;
    public string? TimezoneId { get; set; }
    public string? Locale { get; set; }
    public bool ProxyEnabled { get; set; } = true;
    public string? StorageStatePath { get; set; }
    public bool WarmUpEnabled { get; set; } = true;
    public CountryCode Country { get; set; } = CountryCode.US;
}
```

### 4.6 JSON-LD Parsing

**Parser:** `JsonLdParser` - Extracts structured data from script tags

**Schema Types Parsed:**
- `LinkedInJobPostingLd` - Main job posting schema
- `HiringOrganizationLd` - Company info
- `JobLocationLd` / `AddressLd` - Location info
- `BaseSalaryLd` - Salary information

**Salary Value Handling:**
- Supports simple number values
- Supports QuantitativeValue objects with min/max
- Currency extraction from multiple locations

---

## 5. MAPPING TO GHOST.SDK.SPIDER

### 5.1 Entity Mapping

**Current:** `JobListing` record with manual extraction

**Target:** `LinkedInJobEntity` extending `EntityBase<LinkedInJobEntity>`

```csharp
// Proposed LinkedInJobEntity for Ghost.Sdk.Spider
public class LinkedInJobEntity : EntityBase<LinkedInJobEntity>
{
    [ValueSelector("//script[@type='application/ld+json']", SelectorType.XPath)]
    [Formatter(typeof(JsonLdJobExtractor))]
    public string? JsonLdData { get; set; }
    
    [ValueSelector(".top-card-layout__title, .job-details-jobs-unified-top-card__job-title, h1", SelectorType.CSS)]
    public string Title { get; set; } = string.Empty;
    
    [ValueSelector(".top-card-layout__first-subline .topcard__org-name-link, .job-details-jobs-unified-top-card__company-name", SelectorType.CSS)]
    public string Company { get; set; } = string.Empty;
    
    [ValueSelector(".top-card-layout__first-subline .topcard__flavor--bullet, .job-details-jobs-unified-top-card__bullet", SelectorType.CSS)]
    public string? Location { get; set; }
    
    [ValueSelector(".show-more-less-html__markup, #job-details, .description__text", SelectorType.CSS)]
    public string? Description { get; set; }
    
    [ValueSelector("time[datetime]", SelectorType.CSS, Attribute = "datetime")]
    [Formatter(typeof(DateTimeFormatter), Format = "ISO8601")]
    public DateTimeOffset PostedAt { get; set; }
    
    // Additional fields...
}
```

### 5.2 Configuration-Based Extraction Mapping

```yaml
# Proposed Spider Configuration for LinkedIn
entities:
  - name: LinkedInJob
    container:
      type: CSS
      expression: ".jobs-search-results__list-item, .jobs-search__results-list li, .base-card"
    isList: true
    fields:
      - name: Id
        type: String
        required: true
        selector:
          type: Regex
          expression: 'data-entity-urn="urn:li:jobPosting:(?<id>[0-9]+)"'
          attribute: data-entity-urn
        fallbackSelectors:
          - type: Regex
            expression: '/jobs/(?:view|r)/(?<id>[0-9]+)'
      
      - name: Title
        type: String
        required: true
        selector:
          type: CSS
          expression: ".job-card-list__title, .base-search-card__title"
      
      - name: Company
        type: String
        required: true
        selector:
          type: CSS
          expression: ".job-card-container__company-name, .base-search-card__subtitle"
      
      - name: Location
        type: String
        selector:
          type: CSS
          expression: ".job-card-container__metadata-item, .job-search-card__location"
      
      - name: Url
        type: Url
        selector:
          type: CSS
          expression: "a.base-card__full-link, a.job-card-list__title"
          attribute: href
```

### 5.3 Strategy Mapping

| Current Strategy | Spider Equivalent |
|-----------------|-------------------|
| `GuestApi` | Custom adapter (`LinkedInGuestAdapter`) |
| `BrowserPage` | `StaticHtmlAdapter` with Playwright engine |
| `Hybrid` | `StrategyChain` with fallback conditions |

**Strategy Chain Configuration:**
```csharp
strategies:
  - name: GuestApi
    priority: 1
    type: LinkedInGuest
    stopOnSuccess: true
    maxRetries: 3
    
  - name: BrowserFallback
    priority: 2
    type: Browser
    fallbackConditions:
      - strategy: GuestApi
        condition: NoResults
    delayBefore: "00:00:02"
```

### 5.4 Middleware Mapping

| Current Feature | Spider Middleware |
|-----------------|-------------------|
| Session Pool | `ProxyRotationMiddleware` + custom `LinkedInSessionPool` |
| Rate Limit Detection | `RateLimitMiddleware` (custom detector) |
| Warm-up requests | `StealthMiddleware` with pre-warm script |
| Retry logic (3 attempts) | `RetryMiddleware` with configured maxRetries |
| Cookie clearing | Custom middleware or adapter option |

### 5.5 Adapter Mapping

**Guest API Adapter Requirements:**
- Extend `IContentAdapter` or use `JavaScriptAdapter`
- Implement endpoint URL building
- Handle JSON-LD extraction
- Support time filter parameters (f_TPR)

**Browser Adapter Configuration:**
```csharp
adapters:
  browser:
    type: Playwright
    options:
      headless: true
      viewport: { width: 1920, height: 1080 }
      timezoneId: "Europe/Madrid"
      locale: "es-ES"
      extraHTTPHeaders:
        Accept-Language: "es-ES,es;q=0.9"
```

---

## 6. MIGRATION CHECKLIST

### 6.1 Entity Creation
- [ ] Create `LinkedInJobEntity` class extending `EntityBase<T>`
- [ ] Define all properties with `ValueSelector` attributes
- [ ] Create formatters for JobType/ExperienceLevel parsing
- [ ] Create formatter for relative date parsing ("3 days ago")

### 6.2 Configuration Files
- [ ] Create `linkedin.spider.json` or `linkedin.spider.yaml`
- [ ] Define entity extraction configuration
- [ ] Define strategy chain (GuestApi -> Browser fallback)
- [ ] Define middleware pipeline

### 6.3 Custom Components
- [ ] `LinkedInGuestAdapter` - Guest API adapter
- [ ] `JsonLdExtractor` - Structured data extraction (exists in Ghost.Utilities)
- [ ] `LinkedInRateLimitMiddleware` - Rate limit handling
- [ ] `LinkedInSessionMiddleware` - Session pool integration

### 6.4 Selectors to Migrate
- [ ] All CSS selectors documented in Section 2
- [ ] Regex patterns for ID extraction
- [ ] XPath equivalents for complex selections

### 6.5 Testing Requirements
- [ ] Guest API strategy tests
- [ ] Browser fallback tests
- [ ] Hybrid strategy tests
- [ ] Rate limit handling tests
- [ ] Field extraction accuracy tests

---

## 7. KNOWN CHALLENGES

### 7.1 LinkedIn Anti-Scraping Measures
- Aggressive rate limiting
- Dynamic DOM structures (A/B testing)
- JavaScript-rendered content
- Cookie/session validation

### 7.2 Data Quality Issues
- Inconsistent JSON-LD schema
- Missing salary information (common)
- Relative date formats ("2 weeks ago")
- Location format variations

### 7.3 Spider SDK Gaps
- No built-in relative date parser
- No built-in JSON-LD extraction attribute
- Custom adapter needed for Guest API
- Session pooling not built-in

---

## 8. RECOMMENDATIONS

1. **Preserve Multi-Strategy Approach:** The Hybrid strategy is essential for LinkedIn reliability
2. **Maintain Session Pool:** Continue using `LinkedInSessionPool` for Guest API efficiency
3. **Custom Middleware:** Create LinkedIn-specific middleware for rate limits and session management
4. **Fallback Selectors:** Use Spider's `FallbackSelectors` configuration for selector variations
5. **Formatter Pipeline:** Leverage Spider's formatter system for type conversion

---

## Appendix A: File Locations

**Source Files Analyzed:**
- `/home/rrj/src/github/rudironsoni/Ghost/src/Platforms/Ghost.Platform.LinkedIn/LinkedInJobClient.cs`
- `/home/rrj/src/github/rudironsoni/Ghost/src/Platforms/Ghost.Platform.LinkedIn/Internal/GuestJobSearch.cs`
- `/home/rrj/src/github/rudironsoni/Ghost/src/Platforms/Ghost.Platform.LinkedIn/Internal/JsonLdParser.cs`
- `/home/rrj/src/github/rudironsoni/Ghost/src/Platforms/Ghost.Platform.LinkedIn/LinkedInOptions.cs`

**Target SDK:**
- `/home/rrj/src/github/rudironsoni/Ghost/src/SDK/Ghost.Sdk.Spider/`

**Contracts:**
- `/home/rrj/src/github/rudironsoni/Ghost/src/Contracts/Ghost.Contracts.Jobs/DTOs/JobListing.cs`
- `/home/rrj/src/github/rudironsoni/Ghost/src/Contracts/Ghost.Contracts.Jobs/DTOs/Enums.cs`
