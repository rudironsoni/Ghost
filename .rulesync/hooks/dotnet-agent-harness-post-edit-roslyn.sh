#!/usr/bin/env bash
#
# Post-Edit Roslyn Analyzer Hook
# Runs Roslyn analyzers on modified .cs files and routes to skills
#
# Usage: echo '{"file_path":"/path/to/file.cs","project_root":"/path/to/project"}' | ./post-edit-roslyn.sh
#
# Configuration:
#   DOTNET_AGENT_HARNESS_SKIP_ANALYZERS=false - Skip analysis (default: false, set to true to disable)
#   DOTNET_AGENT_HARNESS_ANALYZER_TIMEOUT=60 - Timeout in seconds (default: 60)
#   DOTNET_AGENT_HARNESS_ANALYZER_SEVERITY=Warning - Minimum severity (Error/Warning/Info)
#   DOTNET_AGENT_HARNESS_ANALYZER_MODE=async - Execution mode (sync/async)
#
# Outputs JSON with violations and skill recommendations

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="${PROJECT_ROOT:-$(pwd)}"
WORKING_DIRECTORY="$(pwd)"

# Configuration with defaults
SKIP_ANALYZERS="${DOTNET_AGENT_HARNESS_SKIP_ANALYZERS:-false}"
TIMEOUT="${DOTNET_AGENT_HARNESS_ANALYZER_TIMEOUT:-60}"
MIN_SEVERITY="${DOTNET_AGENT_HARNESS_ANALYZER_SEVERITY:-Warning}"
MODE="${DOTNET_AGENT_HARNESS_ANALYZER_MODE:-async}"

# Colors for output (if terminal)
RED='\033[0;31m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Check if we should skip
if [[ "${SKIP_ANALYZERS}" == "true" ]]; then
    echo '{"status": "skipped", "reason": "DOTNET_AGENT_HARNESS_SKIP_ANALYZERS=true"}'
    exit 0
fi

# Read input JSON from stdin
INPUT_JSON=""
if [[ ! -t 0 ]]; then
    INPUT_JSON="$(cat)"
fi

if [[ -z "${INPUT_JSON}" ]]; then
    echo '{"status": "skipped", "reason": "No hook payload provided"}'
    exit 0
fi

if ! command -v jq >/dev/null 2>&1; then
    echo '{"status": "skipped", "reason": "jq not available"}'
    exit 0
fi

# Parse input
FILE_PATH=$(printf '%s' "${INPUT_JSON}" | jq -r '.file_path // .tool_input.file_path // .filePath // empty')
FILE_ROOT=$(printf '%s' "${INPUT_JSON}" | jq -r '.project_root // .cwd // .working_directory // empty')

if [[ -z "${FILE_PATH}" ]]; then
    echo '{"status": "skipped", "reason": "No file_path provided"}'
    exit 0
fi

if [[ "${FILE_PATH}" != /* ]]; then
    FILE_PATH="${WORKING_DIRECTORY}/${FILE_PATH#./}"
fi

# Use provided root or detect
if [[ -n "${FILE_ROOT}" ]]; then
    PROJECT_ROOT="${FILE_ROOT}"
else
    # Detect project root by looking for .csproj or .sln
    DIR="$(dirname "${FILE_PATH}")"
    while [[ "${DIR}" != "/" ]]; do
        if ls "${DIR}"/*.csproj 1> /dev/null 2>&1 || ls "${DIR}"/*.sln 1> /dev/null 2>&1; then
            PROJECT_ROOT="${DIR}"
            break
        fi
        DIR="$(dirname "${DIR}")"
    done
fi

# Find the .csproj for this file
CSproj_FILE=""
DIR="$(dirname "${FILE_PATH}")"
while [[ "${DIR}" != "/" && -z "${CSproj_FILE}" ]]; do
    CSproj_FILE=$(find "${DIR}" -maxdepth 1 -name "*.csproj" -print -quit 2>/dev/null || true)
    if [[ -n "${CSproj_FILE}" ]]; then
        break
    fi
    DIR="$(dirname "${DIR}")"
done

if [[ -z "${CSproj_FILE}" ]]; then
    echo '{"status": "skipped", "reason": "No .csproj found for file"}'
    exit 0
fi

# Load analyzer-to-skill mapping
ANALYZER_MAP="${SCRIPT_DIR}/../analyzer-to-skill.json"
if [[ ! -f "${ANALYZER_MAP}" ]]; then
    ANALYZER_MAP="${PROJECT_ROOT}/.rulesync/analyzer-to-skill.json"
fi

# Run analysis with timeout
run_analysis() {
    local csproj="$1"
    local output
    
    # Build with analyzers - capture both stdout and stderr
    # Use binary log for detailed analysis if available
    if command -v dotnet-format &> /dev/null; then
        output=$(dotnet build "${csproj}" --no-incremental \
            -p:RunAnalyzersDuringBuild=true \
            -p:TreatWarningsAsErrors=false \
            -p:WarningLevel=4 \
            -v:q \
            -nologo 2>&1) || true
    else
        output=$(dotnet build "${csproj}" --no-incremental \
            -p:RunAnalyzersDuringBuild=true \
            -p:TreatWarningsAsErrors=false \
            -v:q \
            -nologo 2>&1) || true
    fi
    
    echo "${output}"
}

# Export function for timeout
export -f run_analysis

# Run with timeout
ANALYSIS_OUTPUT=$(timeout "${TIMEOUT}" bash -c "run_analysis \"${CSproj_FILE}\"" 2>&1) || {
    echo "{\"status\": \"timeout\", \"timeout\": $TIMEOUT, \"message\": \"Analysis timed out\"}"
    exit 0
}

# Parse MSBuild output to extract analyzer warnings
parse_violations() {
    local output="$1"
    local violations=()
    
    # Parse format: FilePath(Line,Column): error/warning CODE: Message [Project]
    while IFS= read -r line; do
        # Match analyzer warnings
        if [[ "${line}" =~ ^.*\(([0-9]+),([0-9]+)\):[[:space:]]+(error|warning)[[:space:]]+([A-Z]{2,}[0-9]{4}):[[:space:]]+(.+)\[ ]]; then
            local line_num="${BASH_REMATCH[1]}"
            local col="${BASH_REMATCH[2]}"
            local severity="${BASH_REMATCH[3]}"
            local code="${BASH_REMATCH[4]}"
            local message="${BASH_REMATCH[5]}"
            
            # Map severity to skill
            local skill=""
            if [[ -f "${ANALYZER_MAP}" ]]; then
                skill=$(jq -r ".mappings[\"${code}\"].skill // empty" "${ANALYZER_MAP}" 2>/dev/null || true)
            fi
            
            violations+=("{\"code\": \"${code}\", \"severity\": \"${severity}\", \"line\": $line_num, \"column\": $col, \"message\": \"${message//\"/\\\"}\", \"skill\": \"${skill}\"}")
        fi
    done <<< "${output}"
    
    # Join violations
    local IFS=','
    echo "[${violations[*]}]"
}

VIOLATIONS=$(parse_violations "${ANALYSIS_OUTPUT}")

# Count by severity
ERROR_COUNT=$(echo "${VIOLATIONS}" | jq '[.[] | select(.severity == "error")] | length')
WARNING_COUNT=$(echo "${VIOLATIONS}" | jq '[.[] | select(.severity == "warning")] | length')
SUGGESTION_COUNT=$(echo "${VIOLATIONS}" | jq '[.[] | select(.severity == "suggestion")] | length')

# Determine overall status
STATUS="passed"
if [[ $ERROR_COUNT -gt 0 ]]; then
    STATUS="failed"
elif [[ $WARNING_COUNT -gt 0 && "${MIN_SEVERITY}" == "Warning" ]]; then
    STATUS="failed"
fi

# Build output JSON
cat << EOF
{
  "status": "${STATUS}",
  "file": "${FILE_PATH}",
  "project": "${CSproj_FILE}",
  "summary": {
    "errors": $ERROR_COUNT,
    "warnings": $WARNING_COUNT,
    "suggestions": $SUGGESTION_COUNT
  },
  "violations": $VIOLATIONS,
  "recommendations": $(if [[ $ERROR_COUNT -gt 0 || $WARNING_COUNT -gt 0 ]]; then
    echo "${VIOLATIONS}" | jq '[.[] | select(.skill != "") | {code: .code, skill: .skill}] | unique'
  else
    echo "[]"
  fi),
  "config": {
    "timeout": $TIMEOUT,
    "min_severity": "${MIN_SEVERITY}",
    "mode": "${MODE}"
  }
}
EOF
