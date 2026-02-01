# Ghost Job Search - Full Test Script

## Overview

The `test_all_providers.sh` script automates comprehensive testing of all Ghost job search platforms. It starts the Ghost.WebAPI, tests each platform individually with formatted JQ output, runs an aggregated search, and then cleanly shuts down the API server.

## Features

- **Automatic API Management**: Starts and stops Ghost.WebAPI automatically
- **Health Checks**: Verifies API health and platform status before testing
- **Multi-Platform Testing**: Tests LinkedIn, Indeed, Google, Glassdoor individually
- **Aggregated Search**: Searches all platforms simultaneously
- **JQ Formatting**: Beautiful JSON output with structured job listings
- **Error Handling**: Graceful handling of HTTP errors, JSON parsing, and platform failures
- **Colored Output**: Color-coded success/error/warning messages
- **Cleanup**: Automatic shutdown of API on exit (even on Ctrl+C)

## Usage

### Basic Usage

```bash
# Navigate to script directory
cd examples/scripts/job-search/

# Make executable (first time only)
chmod +x test_all_providers.sh

# Run the script
./test_all_providers.sh
```

### Custom Configuration

```bash
# Set custom API URL
export API_URL="http://localhost:5001"

# Set custom query and location
export QUERY="Python Developer"
export LOCATION="Remote"

# Run with custom configuration
./test_all_providers.sh
```

### Configuration Options (Environment Variables)

| Variable | Default | Description |
|----------|---------|-------------|
| `API_URL` | `http://localhost:5000` | Ghost.WebAPI URL |
| `QUERY` | "Ingeniero de Software" | Job search query (Spanish) |
| `LOCATION` | "Madrid" | Job location (configured for Spain) |
| `MAX_RESULTS` | `5` | Maximum results per platform |

**Regional Configuration**: The Ghost instance is configured for Spain (ES) region across all platforms:
- **LinkedIn**: `GHOST__EXTENSIONS__LINKEDIN__COUNTRY=ES`, `LOCALE=es-ES`, `TIMEZONE=Europe/Madrid`
- **Indeed**: `GHOST__EXTENSIONS__INDEED__COUNTRY=ES`
- **InfoJobs**: `GHOST__EXTENSIONS__INFOJOBS__COUNTRY=ES` (Spanish-only platform)
- **Google Jobs**: `GHOST__EXTENSIONS__GOOGLE__JOBS__COUNTRY=ES`
- **Glassdoor**: `GHOST__EXTENSIONS__GLASSDOOR__COUNTRY=ES`

To test with US locations, update the `.env` file with US configuration and change the script's `QUERY` and `LOCATION` variables.

## Output Format

### Platform Testing

Each platform test shows:
- Platform name
- HTTP status code
- Number of jobs found
- Job listings with: title, company, location, salary, source, URL

 Example output:
```
========================================
Testing Platform: LinkedIn
========================================

Request:
  URL: http://localhost:5000/api/jobs/search
  Query: Ingeniero de Software
  Location: Madrid
  MaxResults: 5
  Sources: [LinkedIn]

HTTP Status: 200

Jobs Found: 5

✅ Successfully retrieved 5 jobs

Job Listings:
Ingeniero de Software Senior...
  Company: TechCorp España
  Location: Madrid, España
  Salary: €45.000 - €55.000
  Source: LinkedIn
  URL: https://linkedin.com/jobs/view/...
```

### Health Check Output

```
========================================
Health Check
========================================

Checking health endpoint: http://localhost:5000/health

{
  "status": "healthy",
  "timestamp": "2026-02-01T06:59:17Z"
}

Checking jobs health endpoint: http://localhost:5000/api/jobs/health

{
  "LinkedIn": {
    "status": "healthy",
    "lastSuccessfulSearch": "2026-02-01T06:59:17Z"
  },
  "Indeed": {
    "status": "healthy",
    "lastSuccessfulSearch": "2026-02-01T06:59:17Z"
  },
  "Google": {
    "status": "degraded",
    "lastSuccessfulSearch": null
  },
  "Glassdoor": {
    "status": "degraded",
    "lastSuccessfulSearch": null
  }
}
```

### Aggregated Search

```
========================================
Aggregated Search (All Platforms)
========================================

Results by Platform:

Platform: LinkedIn
  Count: 5
  First result: Senior Software Engineer @ TechCorp

Platform: Indeed
  Count: 3
  First result: Python Developer @ StartupXYZ

Platform: Google
  Count: 0
  First result: N/A

All Results:
[LinkedIn] Senior Software Engineer @ TechCorp - San Francisco, CA ($150,000 - $200,000)
[Indeed] Python Developer @ StartupXYZ - Remote (Remote)
[Google] No results found
```

## Platforms Tested

### Primary Platforms (Always Tested)

| Platform | Status | Notes |
|----------|--------|-------|
| **LinkedIn** | ✅ Working | Browser page scraping, returns jobs in Madrid/España (Source: LinkedIn ✅) |
| **Indeed** | ⚠️  Scraper Issue | Configured for ES, but scraper parsing fails (API call succeeds) |
| **InfoJobs** | ⚠️  API Response | Credentials configured, returns 0 jobs from API |
| **Google Jobs** | ⚠️  Scraping Issues | Anti-bot measures, cookie consent pages blocking access |
| **Glassdoor** | ⚠️  Scraping Issues | Timeout issues, browser automation failing |

### Platform-Specific Limitations

#### LinkedIn
- **Status**: ✅ Working (BrowserPage strategy)
- **Configuration**: ES country, es-ES locale, Europe/Madrid timezone
- **Current Test**: Query "Ingeniero de Software", Location "Madrid" → Returns 3 jobs
- **Details**: All job listings now show `Source: LinkedIn` (was previously "N/A"/"Unknown")

#### Indeed
- **Issue**: Scraper parsing failure despite correct ES configuration
- **Logs**: `Scraper Indeed failed` at `IndeedJobParser.ParseJobs`
- **Configuration**: Correctly set to ES (`indeed-co = ES`, `indeed-locale = es-ES`)
- **Root Cause**: GraphQL API response structure may have changed, breaking the parser
- **Current**: API request succeeds (200), but returns 0 jobs due to parsing error
- **Recommended**: Fix `src/Platforms/Ghost.Platform.Indeed/Internal/IndeedJobParser.cs` to handle new Indeed API response format

#### InfoJobs
- **Status**: ⚠️  API Authentication Required
- **Issue**: API returns HTTP 500 with error "103 - Client credentials are invalid"
- **Root Cause**: Missing or invalid `ClientId` and `ClientSecret` in `.env`
- **Fix Applied**:
  - ✅ Fixed API endpoint: `/api/9/offer` → `/api/1/offer`
  - ✅ Fixed JSON field mappings (author.name, salaryMin/salaryMax, updated, requirementMin)
  - ✅ Added enhanced logging to show HTTP status and response body
  - ✅ Added warning log when credentials are missing
- **Required Configuration**:
  ```bash
  # Add to .env:
  GHOST__EXTENSIONS__INFOJOBS__CLIENTID=your_client_id
  GHOST__EXTENSIONS__INFOJOBS__CLIENTSECRET=your_client_secret
  ```
- **How to Get Credentials**:
  1. Visit https://www.infojobs.net/api (InfoJobs Developer Portal)
  2. Register as a developer
  3. Create an application to get ClientId/ClientSecret
  4. Add credentials to `.env` file
- **Without Credentials**: API returns error 103 and 0 jobs

#### Google Jobs
- **Issue**: No public API, web scraping blocked by anti-bot measures
- **Current**: Returns 0 jobs due to scraping failures
- **Recommended**: Use third-party service like SerpApi for production

#### Glassdoor
- **Issue**: API closed since 2020, browser automation experiencing timeouts
- **Current**: Returns 0 jobs with timeout errors
- **Recommended**: Use third-party service like Apify or RapidAPI for production |

## Script Workflow

1. **Pre-flight Checks**
   - Verify jq is installed
   - Navigate to repository root
   - Check for .env configuration

2. **API Startup**
   - Start Ghost.WebAPI in background
   - Wait for health endpoint to respond
   - Record API PID for cleanup

3. **Health Verification**
   - Check /health endpoint
   - Check /api/jobs/health endpoint
   - Display platform status

4. **Platform Testing**
   - Test each platform individually
   - Test with both "Sources" and "platforms" parameters
   - Display formatted results with JQ

5. **Aggregated Search**
   - Search all platforms simultaneously
   - Group results by platform
   - Display all job listings

6. **Cleanup**
   - Stop API server
   - Kill any remaining dotnet processes
   - Display log file location

## Troubleshooting

### Script fails to start with "jq not found"

Install jq:
```bash
# Ubuntu/Debian
sudo apt-get install jq

# macOS
brew install jq

# CentOS/RHEL
sudo yum install jq
```

### API fails to start

1. Check log file:
   ```bash
   cat ghost_api.log | tail -50
   ```

2. Start API manually first:
   ```bash
   dotnet run --project src/Ghost.WebApi
   ```

3. Verify .env configuration:
   ```bash
   cat .env
   ```

### Platform returns 0 jobs

Check platform status:
```bash
curl http://localhost:5000/api/jobs/health | jq .
```

Common issues:
- **LinkedIn/Indeed**: Check platform configuration in .env
- **Google/Glassdoor**: May be blocked by consent pages or require credentials
- **InfoJobs/Tecnoempleo**: Require API credentials in .env

### Source field showing "N/A" or "Unknown"

**Fixed**: The Source field issue in LinkedIn results has been resolved.

**Previous behavior**: LinkedIn jobs were returning `source: null`, which displayed as "N/A" in individual tests and "Unknown" in aggregated search.

**Fix applied**: Updated `LinkedInJobClient.cs` to always set `Source = "LinkedIn"` in all job creation/merge scenarios:
- Shallow job listings in SearchJobsWithStrategyAsync
- Merged job objects in GetJobDetailsBrowserAsync
- Fallback cases when parsing fails
- JSON-LD parser already set Source correctly

**Verification**: Run the script and check:
```
Job Listings:
Software Engineer, Fullstack, Early Career
  Company: Notion
  Location: San Francisco, CA
  Salary: $122,100 - $134,400
  Source: LinkedIn  ✅ Should show "LinkedIn", not "N/A"
```

### API doesn't stop cleanly

Manual cleanup:
```bash
# Find dotnet processes
ps aux | grep "dotnet.*Ghost.WebApi"

# Kill manually
pkill -f "dotnet.*Ghost.WebApi"
```

## Advanced Usage

### Run specific platforms only

Edit the script and modify the `test_all_platforms` function:

```bash
test_all_platforms() {
    print_header "Testing Selected Platforms"

    # Only test working platforms
    test_platform "LinkedIn" "Sources"
    test_platform "Indeed" "Sources"
}
```

### Testing with different queries

```bash
# Test multiple queries in a loop
for query in "Python Developer" "Java Engineer" "Data Scientist"; do
    QUERY="$query" ./test_all_providers.sh
    echo ""
done
```

### Save results to file

```bash
./test_all_providers.sh | tee full_test_output.log
```

## API Reference

### Search Endpoint

**URL**: `POST /api/jobs/search`

**Request Body**:
```json
{
  "Query": "Ingeniero de Software",
  "Location": "Madrid",
  "MaxResults": 5,
  "Sources": ["LinkedIn"],
  "platforms": ["LinkedIn"]
}
```

**Response**:
```json
[
  {
    "id": "4255413340",
    "title": "Junior Back and Front Developers",
    "company": "Plexus Tech",
    "location": "Madrid, Community of Madrid, Spain",
    "description": "Buscamos desarrollador junior...",
    "salary": "$60,000.00 - $150,000.00",
    "jobType": 1,
    "remote": false,
    "url": "https://www.linkedin.com/jobs/view/4255413340",
    "source": "LinkedIn",
    "postedAt": "2026-01-29T10:30:00Z"
  }
]
```

### Health Endpoint

**URL**: `GET /api/jobs/health`

**Response**:
```json
{
  "LinkedIn": {
    "status": "healthy",
    "lastSuccessfulSearch": "2026-02-01T06:59:17Z"
  },
  "Indeed": {
    "status": "healthy",
    "lastSuccessfulSearch": "2026-02-01T06:59:17Z"
  }
}
```

## Exit Codes

| Code | Meaning |
|------|---------|
| `0` | All tests completed successfully |
| `1` | jq not found, API failed to start, or critical error |

## Related Scripts

- `search_all.sh` - Search all platforms via API
- `search_linkedin.sh` - Test LinkedIn only
- `search_indeed.sh` - Test Indeed only
- `search_google.sh` - Test Google only
- `search_glassdoor.sh` - Test Glassdoor only
- `search_working_platforms.sh` - Test working platforms only

## Files Modified

This script generates:
- `ghost_api.log` - API log file (updated on each run)

## Support

For issues or questions:
1. Check the log file for detailed error messages
2. Verify Ghost.WebAPI builds successfully: `dotnet build`
3. Check platform configuration in `.env` or `appsettings.json`
4. Review API documentation in the main Ghost README
