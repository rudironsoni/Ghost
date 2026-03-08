#!/usr/bin/env bash
#
# Error Recovery Hook for dotnet-agent-harness
# Extracts analyzer codes from build/test output and suggests relevant skills
#
# Usage: <build-output> | bash .rulesync/hooks/dotnet-agent-harness-error-recovery.sh
#   Or:  echo '<build-output>' | bash .rulesync/hooks/dotnet-agent-harness-error-recovery.sh
#

set -uo pipefail

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
RULESYNC_DIR="$(dirname "$SCRIPT_DIR")"
ANALYZER_MAP="$RULESYNC_DIR/analyzer-to-skill.json"

# Analyzer code patterns (CS####, CA####, NU####, MSB####, RCS####)
ANALYZER_PATTERN='(CS[0-9]{4}|CA[0-9]{4}|NU[0-9]{4}|MSB[0-9]{4}|RCS[0-9]{4})'

# Read input from stdin
INPUT="$(cat)"

# Early exit: No input provided
if [[ -z "$INPUT" ]]; then
    jq -n '{errors: [], recommendations: [], count: 0, message: "No input provided for analysis"}' 2>/dev/null || \
        echo '{"errors":[],"recommendations":[],"count":0,"message":"No input provided for analysis"}'
    exit 0
fi

# Early exit: Analyzer mapping file not found
if [[ ! -f "$ANALYZER_MAP" ]]; then
    jq -n --arg path "$ANALYZER_MAP" \
        '{errors: [], recommendations: [], count: 0, message: "Analyzer mapping not found: \($path)"}' 2>/dev/null || \
        echo '{"errors":[],"recommendations":[],"count":0,"message":"Analyzer mapping not found"}'
    exit 0
fi

# Extract unique analyzer codes from input
extract_analyzer_codes() {
    local input="$1"
    echo "$input" | grep -oE "$ANALYZER_PATTERN" | sort -u
}

# Lookup skill for a given analyzer code
lookup_skill() {
    local code="$1"

    if command -v jq >/dev/null 2>&1; then
        # Use jq for efficient JSON lookup
        jq -r --arg code "$code" '.mappings[$code] // empty' "$ANALYZER_MAP" 2>/dev/null
    else
        # Fallback: grep-based extraction (less precise but functional)
        grep -A5 "\"$code\":" "$ANALYZER_MAP" | grep -v '^--$' | head -6
    fi
}

# Build recommendation object for a single analyzer code
build_recommendation() {
    local code="$1"
    local skill_info

    skill_info=$(lookup_skill "$code")

    # Parse skill info based on available tools
    if command -v jq >/dev/null 2>&1; then
        if [[ -n "$skill_info" && "$skill_info" != "null" ]]; then
            local skill title message severity
            skill=$(echo "$skill_info" | jq -r '.skill // empty')
            title=$(echo "$skill_info" | jq -r '.title // empty')
            message=$(echo "$skill_info" | jq -r '.message // empty')
            severity=$(echo "$skill_info" | jq -r '.severity // "warning"')

            if [[ -n "$skill" ]]; then
                jq -n \
                    --arg code "$code" \
                    --arg skill "$skill" \
                    --arg title "$title" \
                    --arg message "$message" \
                    --arg severity "$severity" \
                    '{
                        code: $code,
                        skill: $skill,
                        title: $title,
                        message: $message,
                        severity: $severity,
                        action: "Invoke [skill:\($skill)] to resolve this issue"
                    }'
            fi
        fi
    else
        # Fallback: manual parsing with grep/sed
        if [[ -n "$skill_info" ]]; then
            local skill title message severity
            skill=$(echo "$skill_info" | grep '"skill"' | sed 's/.*"skill".*"\([^"]*\)".*/\1/')
            title=$(echo "$skill_info" | grep '"title"' | sed 's/.*"title".*"\([^"]*\)".*/\1/')
            message=$(echo "$skill_info" | grep '"message"' | sed 's/.*"message".*"\([^"]*\)".*/\1/')
            severity=$(echo "$skill_info" | grep '"severity"' | sed 's/.*"severity".*"\([^"]*\)".*/\1/')

            if [[ -n "$skill" ]]; then
                echo "{\"code\":\"$code\",\"skill\":\"$skill\",\"title\":\"$title\",\"message\":\"$message\",\"severity\":\"$severity\",\"action\":\"Invoke [skill:$skill] to resolve this issue\"}"
            fi
        fi
    fi
}

# Group recommendations by skill
group_by_skill() {
    local recs="$1"

    if command -v jq >/dev/null 2>&1; then
        echo "$recs" | jq -s '
            group_by(.skill) |
            map({
                skill: .[0].skill,
                codes: [.[].code],
                count: length,
                title: .[0].title,
                message: .[0].message,
                severity: .[0].severity
            })
        '
    else
        # Without jq, return simple array
        echo "$recs"
    fi
}

# Main processing
CODES=$(extract_analyzer_codes "$INPUT")

# Early exit: No analyzer codes found
if [[ -z "$CODES" ]]; then
    jq -n '{errors: [], recommendations: [], count: 0, message: "No analyzer codes found in output"}' 2>/dev/null || \
        echo '{"errors":[],"recommendations":[],"count":0,"message":"No analyzer codes found in output"}'
    exit 0
fi

# Build recommendations for each code
RECOMMENDATIONS=""
CODE_COUNT=0

while IFS= read -r code; do
    [[ -z "$code" ]] && continue

    rec=$(build_recommendation "$code")

    if [[ -n "$rec" ]]; then
        if [[ -z "$RECOMMENDATIONS" ]]; then
            RECOMMENDATIONS="$rec"
        else
            RECOMMENDATIONS="$RECOMMENDATIONS
$rec"
        fi
        ((CODE_COUNT++))
    fi
done <<< "$CODES"

# Build final output
if command -v jq >/dev/null 2>&1; then
    # Use jq to build proper JSON structure
    GROUPED=$(echo "$RECOMMENDATIONS" | jq -s '
        if . == [] then
            {errors: [], recommendations: [], count: 0}
        else
            group_by(.skill) |
            {
                errors: [.[].[].code] | unique,
                recommendations: map({
                    skill: .[0].skill,
                    codes: [.[].code],
                    count: length,
                    title: .[0].title,
                    message: .[0].message,
                    severity: .[0].severity,
                    action: "Invoke [skill:\(.[0].skill)] to resolve \(length) issue(s)"
                }),
                count: [.[].[]] | length,
                message: "Found \([.[].[]] | length) analyzer issue(s) with skill mappings"
            }
        end
    ')

    echo "$GROUPED"
else
    # Fallback: Build JSON manually
    ERROR_ARRAY="["
    REC_ARRAY="["

    while IFS= read -r line; do
        [[ -z "$line" ]] && continue

        code=$(echo "$line" | grep -o '"code":"[^"]*"' | cut -d'"' -f4)
        skill=$(echo "$line" | grep -o '"skill":"[^"]*"' | cut -d'"' -f4)

        if [[ -n "$code" ]]; then
            if [[ "$ERROR_ARRAY" != "[" ]]; then
                ERROR_ARRAY="$ERROR_ARRAY,"
            fi
            ERROR_ARRAY="$ERROR_ARRAY\"$code\""

            if [[ "$REC_ARRAY" != "[" ]]; then
                REC_ARRAY="$REC_ARRAY,"
            fi
            REC_ARRAY="$REC_ARRAY{\"code\":\"$code\",\"skill\":\"$skill\"}"
        fi
    done <<< "$RECOMMENDATIONS"

    ERROR_ARRAY="$ERROR_ARRAY]"
    REC_ARRAY="$REC_ARRAY]"

    echo "{\"errors\":$ERROR_ARRAY,\"recommendations\":$REC_ARRAY,\"count\":$CODE_COUNT,\"message\":\"Found $CODE_COUNT analyzer issue(s) with skill mappings\"}"
fi

exit 0
