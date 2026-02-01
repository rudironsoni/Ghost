#!/usr/bin/env bash
set -euo pipefail

# weekly maintenance check
# Usage: scripts/maintenance-check.sh [LOG_GLOB]
# Defaults to logs/*.log in repo root. You can pass e.g. "/var/log/myapp/*.log"

LOG_GLOB="${1:-logs/*.log}"
DATA_DIR="${HOME}/.maintenance"
PREV_RATE_FILE="$DATA_DIR/last_rate.txt"
mkdir -p "$DATA_DIR"

success_count=0
failed_count=0

shopt -s nullglob
for f in $LOG_GLOB; do
  [ -f "$f" ] || continue
  s=$(grep -c "SUCCESS" "$f" || true)
  fa=$(grep -c "FAILED" "$f" || true)
  success_count=$((success_count + s))
  failed_count=$((failed_count + fa))
done
shopt -u nullglob

total=$((success_count + failed_count))
if [ "$total" -eq 0 ]; then
  rate=0
else
  # integer percentage
  rate=$(( (success_count * 100) / total ))
fi

printf "Success: %d  Failed: %d  Total: %d\n" "$success_count" "$failed_count" "$total"
printf "Current success rate: %d%%\n" "$rate"

prev_rate=""
if [ -f "$PREV_RATE_FILE" ]; then
  prev_rate=$(cat "$PREV_RATE_FILE" 2>/dev/null || echo "")
fi
if [ -n "$prev_rate" ]; then
  printf "Previous rate: %s%%\n" "$prev_rate"
  if [ "$rate" -gt "$prev_rate" ]; then
    trend="up"
  elif [ "$rate" -lt "$prev_rate" ]; then
    trend="down"
  else
    trend="no change"
  fi
  printf "Trend: %s (now %d%%, was %s%%)\n" "$trend" "$rate" "$prev_rate"
else
  printf "Trend: no previous data\n"
fi

echo
echo "Recent errors (matched lines containing FAILED, ERROR, Exception) — latest up to 200 lines, de-duplicated:"
{
  shopt -s nullglob
  for f in $LOG_GLOB; do
    [ -f "$f" ] || continue
    # print newest lines first per file, then combine
    tail -n 200 "$f" || true
  done
  shopt -u nullglob
} | grep -E "FAILED|ERROR|Exception" -i || true

# Persist current rate for next run
printf "%d" "$rate" > "$PREV_RATE_FILE" 2>/dev/null || true

if [ "$rate" -lt 50 ]; then
  echo
  echo "ALERT: success rate below threshold (50%). Current: ${rate}%"
  # exit code indicates alert state
  exit 2
fi

exit 0
