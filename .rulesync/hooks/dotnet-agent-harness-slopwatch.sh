#!/usr/bin/env bash
set -uo pipefail

INPUT=""
if [[ ! -t 0 ]]; then
  INPUT="$(cat)"
fi

FILE_PATH=""
if [[ -n "${INPUT}" ]] && command -v jq >/dev/null 2>&1; then
  FILE_PATH="$(printf '%s' "${INPUT}" | jq -r '.tool_input.file_path // .file_path // .filePath // empty' 2>/dev/null || true)"
fi

if [[ -n "${FILE_PATH}" ]]; then
  case "${FILE_PATH}" in
    *.cs|*.csproj|*.props|*.targets|*.sln|*.slnx) ;;
    *) exit 0 ;;
  esac
fi

emit_message() {
  if command -v jq >/dev/null 2>&1; then
    jq -n --arg msg "$1" '{ systemMessage: $msg }'
  else
    printf '%s\n' "$1"
  fi
}

HAS_LOCAL_MANIFEST=false
if [[ -f ".config/dotnet-tools.json" ]] && grep -q '"slopwatch\.cmd"' ".config/dotnet-tools.json" 2>/dev/null; then
  HAS_LOCAL_MANIFEST=true
fi

if command -v slopwatch >/dev/null 2>&1; then
  SLOPWATCH_COMMAND=(slopwatch analyze -d . --hook)
elif [[ "${HAS_LOCAL_MANIFEST}" = true ]] && command -v dotnet >/dev/null 2>&1; then
  SLOPWATCH_COMMAND=(dotnet tool run slopwatch analyze -d . --hook)
else
  exit 0
fi

if [[ ! -f ".slopwatch/baseline.json" ]]; then
  emit_message "Slopwatch pack detected but .slopwatch/baseline.json is missing. Run 'dotnet tool restore && dotnet tool run slopwatch init' when you are ready to enforce the quality gate."
  exit 0
fi

STATUS=0
OUTPUT="$("${SLOPWATCH_COMMAND[@]}" 2>&1)" || STATUS=$?
if [[ "${STATUS}" -eq 0 ]]; then
  exit 0
fi

PREVIEW="$(printf '%s\n' "${OUTPUT}" | sed -n '1,12p')"
emit_message "Slopwatch flagged potential anti-patterns in dirty files. Review the findings and fix the code instead of suppressing them.
${PREVIEW}"
exit 0
