#!/usr/bin/env bash
set -euo pipefail
API_URL="${API_URL:-http://localhost:5000}"
SEARCH_URL="$API_URL/api/jobs/search"
QUERIES=("software engineer" "product manager" "data scientist" "nurse" "teacher")
LOCATIONS=("San Francisco" "New York" "Remote" "London" "Madrid")
TOTAL=20
COUNT=0
DELAY=3
LOGFILE="logs/pilot_test_glassdoor.md"

printf "Pilot Glassdoor test\nStart: %s\nAPI_URL: %s\n\n" "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$API_URL" > "$LOGFILE"

while [ $COUNT -lt $TOTAL ]; do
  Q=${QUERIES[$((COUNT % ${#QUERIES[@]}))]}
  L=${LOCATIONS[$((COUNT % ${#LOCATIONS[@]}))]}
  PAYLOAD=$(printf '{"Query":"%s","Location":"%s","MaxResults":5,"Sources":["Glassdoor"]}' "$Q" "$L")
  START=$(date +%s%3N)
  HTTP_RESPONSE=$(curl -s -w '\n%{http_code}' -X POST "$SEARCH_URL" -H "Content-Type: application/json" -d "$PAYLOAD" || true)
  END=$(date +%s%3N)
  ELAPSED_MS=$((END-START))
  BODY=$(printf "%s" "$HTTP_RESPONSE" | sed -n '1,${p;q}')
  STATUS=$(printf "%s" "$HTTP_RESPONSE" | tail -n1)

  ERROR_TYPE="none"
  if printf "%s" "$BODY" | grep -q "errors"; then
    MSG=$(printf "%s" "$BODY" | sed -n 's/.*"message"[[:space:]]*:[[:space:]]*"\([^"\n]*\)".*/\1/p' || true)
    if printf "%s" "$MSG" | grep -qi "token\|csrf"; then
      ERROR_TYPE="token_expired"
    elif printf "%s" "$MSG" | grep -qi "server"; then
      ERROR_TYPE="graphql_error"
    elif printf "%s" "$MSG" | grep -qi "blocked\|captcha\|consent"; then
      ERROR_TYPE="blocked"
    else
      ERROR_TYPE="graphql_error"
    fi
  fi

  SUCCESS=false
  if printf "%s" "$BODY" | grep -q '^\[' && printf "%s" "$BODY" | grep -q '"id"\|"Id"' ; then
    SUCCESS=true
  fi

  printf "#%02d: Query=\"%s\" Location=\"%s\" HTTP=%s Time=%dms Success=%s Error=%s\n" \
    $((COUNT+1)) "$Q" "$L" "$STATUS" "$ELAPSED_MS" "$SUCCESS" "$ERROR_TYPE" >> "$LOGFILE"

  echo "Response (truncated 1000 chars):" >> "$LOGFILE"
  printf "%s" "$BODY" | head -c 1000 >> "$LOGFILE"
  echo "\n---\n" >> "$LOGFILE"

  COUNT=$((COUNT+1))
  sleep $DELAY
done

printf "End: %s\n\n" "$(date -u +%Y-%m-%dT%H:%M:%SZ)" >> "$LOGFILE"

TOTAL_RUN=$TOTAL
SUCCESSES=$(grep -c "Success=true" "$LOGFILE" || true)
GQL_ERRORS=$(grep -c "graphql_error" "$LOGFILE" || true)
TOKEN_ERRORS=$(grep -c "token_expired" "$LOGFILE" || true)
BLOCKED=$(grep -c "blocked" "$LOGFILE" || true)

{
  echo "SUMMARY:";
  echo "Total queries: $TOTAL_RUN";
  echo "Successes: $SUCCESSES";
  echo "GraphQL errors: $GQL_ERRORS";
  echo "Token errors: $TOKEN_ERRORS";
  echo "Blocked: $BLOCKED";
  awk "BEGIN{printf \"Success rate: %.2f%%\n\", ($SUCCESSES/$TOTAL_RUN)*100}";
} >> "$LOGFILE"

exit 0
