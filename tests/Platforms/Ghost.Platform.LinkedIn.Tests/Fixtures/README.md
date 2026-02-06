# LinkedIn Test Fixtures

This directory contains real LinkedIn HTML data captured for the Ghost.Sdk.Spider migration project.

## Captured Data

### Search Results
- **File**: `linkedin-search-results.html` (29,859 bytes)
- **Query**: "software engineer"
- **Jobs Found**: 10 unique job IDs
- **Sample Job IDs**: 4334367155, 4334158613, 4294691515, 4294691514, 4328991043

### Job Detail Pages

| File | Job ID | Title | Company |
|------|--------|-------|---------|
| linkedin-job-detail-1.html | 4294691514 | Software Engineer, New Grad | Stripe |
| linkedin-job-detail-2.html | 4294691515 | Software Engineer, New Grad | Stripe |
| linkedin-job-detail-3.html | 4325252246 | Software Engineer I | Amazon |
| linkedin-job-detail-4.html | 4328991042 | Software Engineer, Backend | Google |
| linkedin-job-detail-5.html | 4328991043 | Software Engineer, Backend | Google |

## HTML Structure

### Search Results HTML Contains:
- Job cards with `data-entity-urn="urn:li:jobPosting:JOB_ID"`
- Job titles in `.base-search-card__title`
- Company names in `.hidden-nested-link`
- Locations in `.job-search-card__location`
- Posting dates in `time[datetime]` elements
- Company logos and job links

### Job Detail HTML Contains:
- Job title in `.top-card-layout__title`
- Company name in `.topcard__org-name-link`
- Location in `.topcard__flavor--bullet`
- Job description in `.show-more-less-html__markup`
- Employment criteria in `.description__job-criteria-list`
- Posted date info
- Company logo and metadata

## Usage in Tests

These fixtures can be used to:
1. Test HTML parsing logic for job extraction
2. Validate selector strategies
3. Mock LinkedIn responses without hitting the live site
4. Test edge cases with real-world HTML structures

## Captured
Date: 2026-02-05
Method: Direct HTTP requests via curl (Guest API endpoints)
