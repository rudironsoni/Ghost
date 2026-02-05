#!/bin/bash

echo "=== Testing Google Jobs DIRECT HTML Scraping (NO APIs) ==="
echo ""
echo "This test will make a direct HTTP request to Google and parse the HTML"
echo "NO SerpAPI, NO external APIs - just pure HTML scraping"
echo ""

# Simple curl test to show the approach
QUERY="software+engineer+jobs+San+Francisco"
URL="https://www.google.com/search?q=${QUERY}&ibp=htl;jobs&udm=8&gl=us&hl=en"

echo "URL: $URL"
echo ""
echo "Making direct HTTP request to Google..."
echo ""

curl -s "$URL" \
  -H "User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36" \
  -H "Accept: text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8" \
  -H "Accept-Language: en-US,en;q=0.9" \
  -H "Referer: https://www.google.com/" \
  -H "Cookie: CONSENT=YES; SOCS=CAESE" \
  --compressed \
  > /tmp/google-jobs-raw.html

echo "Response saved to /tmp/google-jobs-raw.html"
echo "File size: $(wc -c < /tmp/google-jobs-raw.html) bytes"
echo ""

# Check for job-related content
if grep -q "role=\"listitem\"" /tmp/google-jobs-raw.html || \
   grep -q "job-title" /tmp/google-jobs-raw.html || \
   grep -q "JobPosting" /tmp/google-jobs-raw.html; then
    echo "✓ SUCCESS: Found job listings in HTML response!"
    echo ""
    
    # Count potential job listings
    JOB_COUNT=$(grep -o "role=\"listitem\"" /tmp/google-jobs-raw.html | wc -l)
    echo "Potential job listings found: $JOB_COUNT"
    
    # Check for JSON-LD
    if grep -q "application/ld+json" /tmp/google-jobs-raw.html; then
        echo "✓ Found JSON-LD structured data in response"
    fi
    
    # Check for specific job data
    if grep -q "BjJfJf" /tmp/google-jobs-raw.html || \
       grep -q "vNEEBe" /tmp/google-jobs-raw.html; then
        echo "✓ Found Google Jobs widget classes in HTML"
    fi
else
    echo "✗ WARNING: No obvious job listings found in response"
    echo ""
    echo "Checking for consent/captcha pages..."
    
    if grep -q "consent.google.com" /tmp/google-jobs-raw.html; then
        echo "✗ Detected consent page"
    elif grep -q "recaptcha" /tmp/google-jobs-raw.html; then
        echo "✗ Detected CAPTCHA"
    elif grep -q "sorry/index" /tmp/google-jobs-raw.html; then
        echo "✗ Detected Google sorry page"
    fi
    
    echo ""
    echo "First 1000 chars of response:"
    head -c 1000 /tmp/google-jobs-raw.html
fi

echo ""
echo "=== Test Complete ==="
