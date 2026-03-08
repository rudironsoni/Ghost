#!/bin/bash
# Session start hook for dotnet-agent-harness
# Performs .NET project detection and MCP health checks

set -uo pipefail

# Get the directory where this script is located
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCRIPT_NAME="$(basename "$0")"

# Configuration - use script location to find mcp.json, fall back to relative path
if [ -f "${SCRIPT_DIR}/../mcp.json" ]; then
    MCP_CONFIG="${SCRIPT_DIR}/../mcp.json"
else
    MCP_CONFIG="${MCP_CONFIG:-.rulesync/mcp.json}"
fi

# Colors for output (if terminal)
if [ -t 1 ]; then
    RED='\033[0;31m'
    GREEN='\033[0;32m'
    YELLOW='\033[1;33m'
    NC='\033[0m' # No Color
else
    RED=''
    GREEN=''
    YELLOW=''
    NC=''
fi

# Emit JSON message for integration with agent systems
# Pure bash implementation - no jq required
emit_json() {
    local type="$1"
    local message="$2"
    # Escape special characters for JSON
    local escaped_msg
    escaped_msg="$(printf '%s' "$message" | sed 's/\\/\\\\/g; s/"/\\"/g; s/\t/\\t/g; s/\n/\\n/g')"
    printf '{"type":"%s","message":"%s"}\n' "$type" "$escaped_msg"
}

# Detect .NET project context
detect_dotnet_context() {
    local sln_count csproj_count has_global_json tfm
    
    sln_count="$(find . -maxdepth 3 \( -name '*.sln' -o -name '*.slnx' \) 2>/dev/null | wc -l | tr -d ' ')" || sln_count=0
    csproj_count="$(find . -maxdepth 3 -name '*.csproj' 2>/dev/null | wc -l | tr -d ' ')" || csproj_count=0
    has_global_json=false
    [ -f "global.json" ] && has_global_json=true
    
    tfm=""
    local first_csproj
    first_csproj="$(find . -maxdepth 3 -name '*.csproj' -print -quit 2>/dev/null)" || true
    if [ -n "$first_csproj" ]; then
        tfm="$(sed -n 's/.*<TargetFramework[s]\{0,1\}>\([^<;]*\).*/\1/p' "$first_csproj" 2>/dev/null | head -1)" || true
    fi
    
    local context=""
    if [ "$sln_count" -gt 0 ] || [ "$csproj_count" -gt 0 ] || [ "$has_global_json" = true ]; then
        context="Mandatory first action for every task: invoke [skill:dotnet-advisor]. Do not plan, reason, design, or implement until it has been invoked, then follow its routing to load additional skills and apply their standards."
        
        local project_context="This is a .NET project"
        [ -n "$tfm" ] && project_context="This is a .NET project ($tfm)"
        [ "$csproj_count" -gt 0 ] && project_context="$project_context with $csproj_count project(s)"
        [ "$sln_count" -gt 0 ] && project_context="$project_context in $sln_count solution(s)"
        project_context="$project_context."
        context="$context $project_context"
    fi
    
    echo "$context"
}

# Check if a command exists
check_command() {
    command -v "$1" >/dev/null 2>&1
}

# Test HTTP MCP server health
test_http_mcp() {
    local url="$1"
    local timeout=3
    
    if [ -z "$url" ]; then
        return 1
    fi
    
    # Try health endpoint first, then base URL
    if curl -s --connect-timeout "$timeout" -o /dev/null "$url/health" 2>/dev/null; then
        return 0
    elif curl -s --connect-timeout "$timeout" -o /dev/null "$url" 2>/dev/null; then
        return 0
    fi
    
    return 1
}

# Test STDIO MCP server command availability
test_stdio_mcp() {
    local cmd="$1"
    
    if [ -z "$cmd" ]; then
        return 1
    fi
    
    # Special handling for package managers
    case "$cmd" in
        uvx)
            check_command uvx || check_command pipx || return 1
            ;;
        npx)
            check_command npx || return 1
            ;;
        *)
            check_command "$cmd" || return 1
            ;;
    esac
    
    return 0
}

# Parse a simple JSON value from mcp.json using grep/sed
# Fallback to jq if available for complex parsing
parse_mcp_json() {
    local key="$1"
    local field="$2"
    
    if [ -f "$MCP_CONFIG" ]; then
        if command -v jq >/dev/null 2>&1; then
            jq -r ".mcpServers.\"$key\".$field // empty" "$MCP_CONFIG" 2>/dev/null
        else
            # Pure bash fallback - simple pattern matching
            # Handles: "key": { "field": "value" }
            local pattern="\"$key\"[[:space:]]*:[[:space:]]*{[^{}]*\"$field\"[[:space:]]*:[[:space:]]*\"\([^\"]*\)"
            sed -n "s/.*$pattern.*/\1/p" "$MCP_CONFIG" 2>/dev/null | head -1
        fi
    fi
}

# Get MCP server type from config
get_mcp_type() {
    local name="$1"
    parse_mcp_json "$name" "type"
}

# Get MCP server URL (for HTTP type)
get_mcp_url() {
    local name="$1"
    parse_mcp_json "$name" "url"
}

# Get MCP server command (for STDIO type)
get_mcp_command() {
    local name="$1"
    parse_mcp_json "$name" "command"
}

# Check MCP server health
check_mcp_health() {
    local name="$1"
    local mcp_type url cmd
    
    mcp_type="$(get_mcp_type "$name")"
    
    case "$mcp_type" in
        http)
            url="$(get_mcp_url "$name")"
            if test_http_mcp "$url"; then
                echo "available"
            else
                echo "unavailable"
            fi
            ;;
        stdio)
            cmd="$(get_mcp_command "$name")"
            if test_stdio_mcp "$cmd"; then
                echo "available"
            else
                echo "unavailable"
            fi
            ;;
        *)
            echo "unknown"
            ;;
    esac
}

# Get fallback recommendation for unavailable MCP
get_fallback_recommendation() {
    local name="$1"
    
    case "$name" in
        serena)
            echo "Use Read + Grep + Edit for code navigation"
            ;;
        microsoftdocs-mcp)
            echo "Use web search with microsoft.com/learn sources"
            ;;
        context7)
            echo "Use web search for third-party library docs"
            ;;
        deepwiki)
            echo "Read markdown files from docs/ or wiki/ directories"
            ;;
        github)
            echo "Use gh CLI or web interface for GitHub operations"
            ;;
        *)
            echo "No fallback available"
            ;;
    esac
}

# Get MCP description
get_mcp_description() {
    local name="$1"
    local desc
    desc="$(parse_mcp_json "$name" "description")"
    if [ -z "$desc" ]; then
        echo "No description"
    else
        echo "$desc"
    fi
}

# Perform full MCP health check
perform_mcp_health_check() {
    local -a mcp_servers=()
    local -A mcp_status
    local -a unavailable_mcps=()
    local -a fallback_recommendations=()
    
    # Check if config exists
    if [ ! -f "$MCP_CONFIG" ]; then
        emit_json "warning" "MCP config not found at $MCP_CONFIG"
        return 0
    fi
    
    # Get list of MCP servers from JSON
    # Try jq first, fall back to grep/sed
    if command -v jq >/dev/null 2>&1; then
        mapfile -t mcp_servers < <(jq -r '.mcpServers | keys[]' "$MCP_CONFIG" 2>/dev/null || true)
    else
        # Pure bash: extract keys from "key": { pattern
        local mcp_keys
        mcp_keys="$(grep -oP '"[^"]+"[[:space:]]*:[[:space:]]*{' "$MCP_CONFIG" 2>/dev/null | grep -oP '"\K[^"]+' || true)"
        if [ -n "$mcp_keys" ]; then
            mapfile -t mcp_servers <<< "$mcp_keys"
        fi
    fi
    
    if [ ${#mcp_servers[@]} -eq 0 ]; then
        emit_json "info" "No MCP servers configured"
        return 0
    fi
    
    # Check each MCP server
    for mcp in "${mcp_servers[@]}"; do
        local status
        status="$(check_mcp_health "$mcp")"
        mcp_status["$mcp"]="$status"
        
        if [ "$status" != "available" ]; then
            unavailable_mcps+=("$mcp")
            fallback_recommendations+=("$mcp: $(get_fallback_recommendation "$mcp")")
        fi
    done
    
    # Build detailed status message
    local status_details=""
    for mcp in "${mcp_servers[@]}"; do
        local desc
        desc="$(get_mcp_description "$mcp")"
        status_details="${status_details}\n  - $mcp: ${mcp_status[$mcp]} ($desc)"
    done
    
    # Output results
    if [ ${#unavailable_mcps[@]} -eq 0 ]; then
        emit_json "info" "All MCP servers healthy:$status_details"
    else
        local recs=""
        for rec in "${fallback_recommendations[@]}"; do
            recs="${recs}\n  - $rec"
        done
        emit_json "warning" "MCP servers unavailable: ${unavailable_mcps[*]}.$status_details\n\nFallback recommendations:$recs"
    fi
}

# Show help
show_help() {
    cat << 'EOF'
Usage: session-start.sh [COMMAND]

Commands:
  dotnet-context    Detect .NET project context and emit routing guidance
  mcp-health        Perform MCP health checks and report status
  help              Show this help message

If no command is specified, runs dotnet-context by default.

Environment Variables:
  MCP_CONFIG        Path to MCP config file (default: .rulesync/mcp.json)

Examples:
  session-start.sh dotnet-context    # Detect .NET project
  session-start.sh mcp-health        # Check MCP servers
EOF
}

# Main function
main() {
    local command="${1:-dotnet-context}"
    
    case "$command" in
        dotnet-context)
            local context
            context="$(detect_dotnet_context)"
            if [ -n "$context" ]; then
                # Pure bash JSON - no jq required
                printf '{"additionalContext":"%s"}\n' "$context"
            fi
            ;;
        mcp-health)
            perform_mcp_health_check
            ;;
        help|--help|-h)
            show_help
            ;;
        *)
            echo "Unknown command: $command" >&2
            show_help >&2
            exit 1
            ;;
    esac
}

main "$@"
