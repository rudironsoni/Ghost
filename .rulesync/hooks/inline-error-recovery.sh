#!/usr/bin/env bash
#
# Inline Error Recovery Hook - Called from hooks.json postToolUse
# Extracts analyzer codes from build/test output and outputs system messages
#
set -uo pipefail

INPUT="$(cat)"

# Early exit: No input
[[ -z "$INPUT" ]] && exit 0

# Call the main error recovery script
RESULT="$(echo "$INPUT" | bash "$(dirname "$0")/error-recovery.sh")"

# Extract count
COUNT="$(echo "$RESULT" | jq -r '.count // 0')"

# Early exit: No analyzer codes found
[[ "$COUNT" -eq 0 ]] && exit 0

# Extract top recommendation details
TOP_SKILL="$(echo "$RESULT" | jq -r '.recommendations[0].skill // empty')"
TOP_CODES="$(echo "$RESULT" | jq -r '.recommendations[0].codes | join(", ") // empty')"
TOP_COUNT="$(echo "$RESULT" | jq -r '.recommendations[0].count // 0')"
TOP_TITLE="$(echo "$RESULT" | jq -r '.recommendations[0].title // empty')"

# Output system message
jq -n \
    --arg skill "$TOP_SKILL" \
    --arg codes "$TOP_CODES" \
    --arg count "$TOP_COUNT" \
    --arg title "$TOP_TITLE" \
    '{
        systemMessage: "Analyzer issues detected: \($codes) (\($count) issue(s) - \($title)). Consider invoking [skill:\($skill)] for guidance."
    }'

exit 0
