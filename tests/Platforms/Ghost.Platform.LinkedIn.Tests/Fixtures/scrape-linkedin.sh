#!/bin/bash
# LinkedIn HTML Fixture Scraper
# Captures real LinkedIn job search results and job detail pages for test fixtures

set -e

FIXTURES_DIR="$(dirname "$0")"
cd "$FIXTURES_DIR"

USER_AGENT="Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
SEARCH_QUERY="software engineer"
SEARCH_LOCATION=""

echo "=== LinkedIn Fixture Scraper ==="
echo "Search Query: $SEARCH_QUERY"
echo "Output Directory: $(pwd)"
echo ""

# Function to URL encode
url_encode() {
    local string="$1"
    local encoded=""
    local pos c o
    
    for (( pos=0; pos<${#string}; pos++ )); do
        c=${string:$pos:1}
        case "$c" in
            [-_.~a-zA-Z0-9]) encoded+="$c" ;;
            *) printf -v o '%%%02x' "'$c"; encoded+="$o" ;;
        esac
    done
    echo "$encoded"
}

ENCODED_QUERY=$(url_encode "$SEARCH_QUERY")
ENCODED_LOCATION=$(url_encode "$SEARCH_LOCATION")

# Step 1: Scrape search results
echo "[1/6] Scraping LinkedIn job search results..."
SEARCH_URL="https://www.linkedin.com/jobs-guest/jobs/api/seeMoreJobPostings/search?keywords=${ENCODED_QUERY}&location=${ENCODED_LOCATION}&start=0"
echo "  URL: $SEARCH_URL"

curl -s -L \
    -H "User-Agent: $USER_AGENT" \
    -H "Accept: text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8" \
    -H "Accept-Language: en-US,en;q=0.9" \
    -H "Accept-Encoding: gzip, deflate, br" \
    -H "Referer: https://www.linkedin.com/" \
    --compressed \
    --max-time 30 \
    -o "linkedin-search-results.html" \
    "$SEARCH_URL"

if [ -f "linkedin-search-results.html" ]; then
    FILE_SIZE=$(stat -c%s "linkedin-search-results.html" 2>/dev/null || stat -f%z "linkedin-search-results.html" 2>/dev/null || echo "0")
    echo "  Saved: linkedin-search-results.html (${FILE_SIZE} bytes)"
else
    echo "  ERROR: Failed to save search results"
    exit 1
fi

# Extract job IDs from search results
echo "[2/6] Extracting job IDs from search results..."
JOB_IDS=$(grep -oP 'data-entity-urn="urn:li:jobPosting:\K[0-9]+' linkedin-search-results.html | head -20)

if [ -z "$JOB_IDS" ]; then
    # Try alternative pattern
    JOB_IDS=$(grep -oP '/jobs/(?:view|r)/\K[0-9]+' linkedin-search-results.html | head -20)
fi

if [ -z "$JOB_IDS" ]; then
    echo "  WARNING: No job IDs found. LinkedIn may be returning non-standard HTML."
    echo "  Check linkedin-search-results.html for content."
    # Let's check the content
    head -c 2000 linkedin-search-results.html
    exit 1
fi

# Count unique job IDs
UNIQUE_JOB_IDS=$(echo "$JOB_IDS" | sort -u)
JOB_COUNT=$(echo "$UNIQUE_JOB_IDS" | wc -l | tr -d ' ')
echo "  Found $JOB_COUNT unique job IDs"

# Step 3-7: Scrape individual job details
echo "[3/6] Scraping individual job detail pages..."

INDEX=1
for JOB_ID in $UNIQUE_JOB_IDS; do
    if [ $INDEX -gt 5 ]; then
        break
    fi
    
    echo "  Scraping job $INDEX/5 (ID: $JOB_ID)..."
    JOB_URL="https://www.linkedin.com/jobs-guest/jobs/api/jobPosting/${JOB_ID}"
    
    curl -s -L \
        -H "User-Agent: $USER_AGENT" \
        -H "Accept: text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8" \
        -H "Accept-Language: en-US,en;q=0.9" \
        -H "Referer: https://www.linkedin.com/jobs/search/?keywords=${ENCODED_QUERY}" \
        --compressed \
        --max-time 30 \
        -o "linkedin-job-detail-${INDEX}.html" \
        "$JOB_URL"
    
    if [ -f "linkedin-job-detail-${INDEX}.html" ]; then
        FILE_SIZE=$(stat -c%s "linkedin-job-detail-${INDEX}.html" 2>/dev/null || stat -f%z "linkedin-job-detail-${INDEX}.html" 2>/dev/null || echo "0")
        echo "    Saved: linkedin-job-detail-${INDEX}.html (${FILE_SIZE} bytes)"
        
        # Extract basic info
        TITLE=$(grep -oP '<h1[^>]*class="[^"]*top-card-layout__title[^"]*"[^>]*>\K[^<]+' "linkedin-job-detail-${INDEX}.html" | head -1 | tr -d '\n' | sed 's/^[[:space:]]*//;s/[[:space:]]*$//' || echo "N/A")
        if [ "$TITLE" = "N/A" ]; then
            TITLE=$(grep -oP '<h1[^>]*>\K[^<]+' "linkedin-job-detail-${INDEX}.html" | head -1 | tr -d '\n' | sed 's/^[[:space:]]*//;s/[[:space:]]*$//' || echo "N/A")
        fi
        
        echo "    Title: $TITLE"
    else
        echo "    ERROR: Failed to save job details"
    fi
    
    # Be nice to LinkedIn
    sleep 2
    
    INDEX=$((INDEX + 1))
done

echo ""
echo "=== Scraping Complete ==="
echo "Files saved to: $(pwd)"
echo ""
echo "Generated Files:"
ls -la *.html 2>/dev/null || echo "No HTML files found"
