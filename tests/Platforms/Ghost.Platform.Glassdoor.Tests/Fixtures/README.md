# Glassdoor Test Fixtures

This directory contains real-world structured test fixtures for validating the Glassdoor job platform integration.

## Fixture Overview

### Search Results
**File:** `glassdoor-search-results.json`

Contains a paginated search response for "software engineer" jobs with 6 job listings from major tech companies. The response includes:
- Job listings array
- Pagination metadata (total results: 1247, has next page)
- Salary ranges
- Job metadata

### Job Details

Individual job detail fixtures with comprehensive information:

#### 1. glassdoor-job-detail-1.json
- **Title:** Senior Software Engineer
- **Company:** Microsoft Corporation
- **Job ID:** 9247891
- **Location:** Redmond, WA
- **Salary Range:** $145,000 - $210,000 USD
- **Type:** Full-time, Hybrid
- **Posted:** 2026-02-01

**Description:** Azure cloud services role focusing on distributed systems and scalable infrastructure.

#### 2. glassdoor-job-detail-2.json
- **Title:** Software Engineer II
- **Company:** Amazon.com, Inc.
- **Job ID:** 9248102
- **Location:** Seattle, WA
- **Salary Range:** $130,000 - $185,000 USD
- **Type:** Full-time, On-site
- **Posted:** 2026-02-02

**Description:** AWS cloud infrastructure role working on next-generation services.

#### 3. glassdoor-job-detail-3.json
- **Title:** Software Development Engineer
- **Company:** Google LLC
- **Job ID:** 9248345
- **Location:** Mountain View, CA
- **Salary Range:** $155,000 - $225,000 USD
- **Type:** Full-time, Hybrid
- **Posted:** 2026-02-03

**Description:** Large-scale systems role working on products impacting billions of users.

#### 4. glassdoor-job-detail-4.json
- **Title:** Full Stack Software Engineer
- **Company:** Meta Platforms, Inc.
- **Job ID:** 9248567
- **Location:** Menlo Park, CA
- **Salary Range:** $150,000 - $215,000 USD
- **Type:** Full-time, Hybrid
- **Posted:** 2026-02-04

**Description:** Social technology and metaverse platform development role.

#### 5. glassdoor-job-detail-5.json
- **Title:** Backend Software Engineer
- **Company:** Netflix, Inc.
- **Job ID:** 9248789
- **Location:** Los Gatos, CA
- **Salary Range:** $140,000 - $200,000 USD
- **Type:** Full-time, Hybrid
- **Posted:** 2026-02-05

**Description:** Streaming platform backend services role serving millions of members.

## Data Structure

All fixtures follow Glassdoor's GraphQL API response format:

### Search Response Schema
```json
{
  "data": {
    "jobSearchResults": {
      "jobs": [ /* array of job objects */ ],
      "totalResults": number,
      "pageInfo": {
        "hasNextPage": boolean,
        "endCursor": string
      }
    }
  }
}
```

### Job Detail Schema
```json
{
  "data": {
    "job": {
      "jobId": string,
      "jobTitleText": string,
      "employerNameFromSearch": string,
      "employerName": string,
      "location": string,
      "locationFullAddress": string,
      "header": {
        "payPeriodAdjustedPay": {
          "p10": number,
          "p90": number,
          "payCurrency": string
        }
      },
      "jobDescription": string (HTML),
      "postedDate": string (ISO 8601),
      "employmentType": string,
      "remoteWorkTypes": [string],
      "applyUrl": string,
      "employer": { /* employer details */ }
    }
  }
}
```

## Usage in Tests

These fixtures can be used to test:
1. **Job Parser** - Verify correct extraction of job fields from JSON
2. **API Client** - Mock Glassdoor API responses
3. **Integration Tests** - End-to-end workflow validation
4. **Error Handling** - Edge cases and data validation

## HTML Fixtures (Web Scraping)

In addition to JSON fixtures, this directory contains HTML fixtures for testing web scraping functionality:

### HTML Search Results
**File:** `glassdoor-search-results.html`

Contains the HTML structure of a Glassdoor search results page with 5 job listings. Includes the following CSS classes and data attributes:
- `JobsList_jobsList__lqjTr` - Main job list container
- `JobCard_jobCard__jkKTq` - Individual job card
- `JobCard_jobTitle__rbjTE` - Job title
- `EmployerProfile_employerName__Xemli` - Company name
- `JobCard_location__N_iYE` - Job location
- `data-job-id` - Unique job identifier

### HTML Job Details

Individual HTML job detail pages matching the structure of actual Glassdoor job listings:

1. **glassdoor-job-detail-1.html** - Senior Software Engineer @ Google LLC (JV_IC1234567890)
2. **glassdoor-job-detail-2.html** - Software Engineer II @ Microsoft Corporation (JV_IC1234567891)
3. **glassdoor-job-detail-3.html** - Full Stack Software Engineer @ Meta (JV_IC1234567892)
4. **glassdoor-job-detail-4.html** - Backend Software Engineer @ Amazon (JV_IC1234567893)
5. **glassdoor-job-detail-5.html** - Software Development Engineer @ Apple Inc. (JV_IC1234567894)

**Key HTML Elements:**
- `data-test="jobTitle"` - Job title
- `data-test="employerName"` - Company name
- `data-test="location"` - Location
- `data-test="jobDescriptionContent"` - Full job description
- `data-test="detailSalary"` - Salary information
- `data-test="jobId"` - Job ID

## Data Source

Fixtures are modeled after real Glassdoor responses (as of February 2026):
- **JSON fixtures** - GraphQL API responses with actual data structure
- **HTML fixtures** - Web page DOM structure matching Glassdoor's React-based UI

The job IDs, descriptions, and salary ranges are representative of typical software engineering positions at these companies.

## Notes

- All job IDs are fictional but follow Glassdoor's ID format
- HTML fixtures use CSS Modules class naming convention (e.g., `ComponentName_className__hash`)
- Salary ranges reflect typical market rates for senior software engineering roles
- Job descriptions include realistic requirements and responsibilities
- Remote work types: ONSITE, HYBRID, REMOTE
- Employment types: FULL_TIME, PART_TIME, CONTRACT
- Currency codes follow ISO 4217 (USD, EUR, GBP, etc.)
