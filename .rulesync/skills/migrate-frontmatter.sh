#!/usr/bin/env bash
# ═══════════════════════════════════════════════════════════════════════════════
# Skill Frontmatter Migration Script
# Validates and updates SKILL.md frontmatter across all skills
# ═══════════════════════════════════════════════════════════════════════════════

set -euo pipefail

# ═══════════════════════════════════════════════════════════════════════════════
# CONFIGURATION - Early Exit for undefined variables
# ═══════════════════════════════════════════════════════════════════════════════

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SKILLS_DIR="${SCRIPT_DIR}"

# Valid values per taxonomy
readonly VALID_CATEGORIES=(
    "fundamentals"
    "testing"
    "architecture"
    "web"
    "data"
    "performance"
    "security"
    "operations"
    "ui-frameworks"
    "developer-experience"
)

readonly VALID_COMPLEXITY=(
    "beginner"
    "intermediate"
    "advanced"
)

# Category migrations (old -> new)
declare -A CATEGORY_MIGRATIONS=(
    ["devops"]="operations"
    ["platforms"]="ui-frameworks"
    ["tooling"]="developer-experience"
)

# Color codes for output
readonly COLOR_RESET='\033[0m'
readonly COLOR_GREEN='\033[0;32m'
readonly COLOR_RED='\033[0;31m'
readonly COLOR_YELLOW='\033[1;33m'
readonly COLOR_BLUE='\033[0;34m'
readonly COLOR_CYAN='\033[0;36m'
readonly COLOR_MAGENTA='\033[0;35m'

# ═══════════════════════════════════════════════════════════════════════════════
# STATE - Track validation results
# ═══════════════════════════════════════════════════════════════════════════════

# Counters for summary report
declare -i TOTAL_SKILLS=0
declare -i VALID_SKILLS=0
declare -i INVALID_SKILLS=0
declare -i FIXED_SKILLS=0
declare -i SKIPPED_SKILLS=0

# Arrays to store findings
declare -a MISSING_FIELDS=()
declare -a INVALID_VALUES=()
declare -a CATEGORY_MIGRATIONS_NEEDED=()
declare -a FIXED_ITEMS=()

# Command line flags
VERBOSE=false
FIX_MODE=false
SPECIFIC_SKILL=""

# ═══════════════════════════════════════════════════════════════════════════════
# HELPER FUNCTIONS - Atomic Predictability with clear names
# ═══════════════════════════════════════════════════════════════════════════════

print_usage() {
    cat << 'EOF'
Usage: migrate-frontmatter.sh [COMMAND] [OPTIONS]

Commands:
    check           Validate all skill frontmatter (default)
    help            Show this help message

Options:
    --skill NAME    Check only specific skill by name
    --fix           Apply automatic fixes (dry-run by default)
    --verbose       Show detailed output
    --version       Show script version

Examples:
    ./migrate-frontmatter.sh check
    ./migrate-frontmatter.sh check --skill dotnet-testing-xunit
    ./migrate-frontmatter.sh check --fix
    ./migrate-frontmatter.sh check --verbose

EOF
}

log_info() {
    printf "${COLOR_BLUE}[INFO]${COLOR_RESET} %s\n" "$1"
}

log_success() {
    printf "${COLOR_GREEN}[PASS]${COLOR_RESET} %s\n" "$1"
}

log_warning() {
    printf "${COLOR_YELLOW}[WARN]${COLOR_RESET} %s\n" "$1"
}

log_error() {
    printf "${COLOR_RED}[FAIL]${COLOR_RESET} %s\n" "$1"
}

log_fix() {
    printf "${COLOR_CYAN}[FIX]${COLOR_RESET} %s\n" "$1"
}

log_verbose() {
    if [[ "$VERBOSE" == true ]]; then
        printf "${COLOR_MAGENTA}[VERB]${COLOR_RESET} %s\n" "$1"
    fi
}

# ═══════════════════════════════════════════════════════════════════════════════
# YAML PARSING - Parse Don't Validate at boundaries
# ═══════════════════════════════════════════════════════════════════════════════

extract_frontmatter() {
    local file_path="$1"
    
    # Read file and extract content between --- markers
    local frontmatter=""
    local in_frontmatter=false
    local first_marker=true
    
    while IFS= read -r line || [[ -n "$line" ]]; do
        # Early exit: skip non-frontmatter content
        if [[ "$line" == "---" ]]; then
            if [[ "$first_marker" == true ]]; then
                in_frontmatter=true
                first_marker=false
                continue
            else
                # Second --- marks end of frontmatter
                break
            fi
        fi
        
        if [[ "$in_frontmatter" == true ]]; then
            frontmatter+="$line"
            frontmatter+=$'\n'
        fi
    done < "$file_path"
    
    printf '%s' "$frontmatter"
}

parse_yaml_value() {
    local frontmatter="$1"
    local key="$2"
    
    # Extract value after key, handling various YAML formats
    local value
    value=$(printf '%s' "$frontmatter" | grep -E "^${key}:" | head -1)
    
    # Fail Fast: no value found
    if [[ -z "$value" ]]; then
        return 1
    fi
    
    # Strip key and leading whitespace
    value="${value#*:}"
    value="${value# }"
    
    printf '%s' "$value"
}

parse_yaml_multiline() {
    local frontmatter="$1"
    local key="$2"
    
    local value
    value=$(printf '%s' "$frontmatter" | awk -v k="$key" '
        $0 ~ "^"k":" {
            if (match($0, "^"k": *")) {
                line = substr($0, RLENGTH + 1)
                if (line ~ /^[>|]/) {
                    getline
                    while (NF > 0 && $0 !~ /^[a-zA-Z]/) {
                        print
                        getline
                    }
                } else {
                    print line
                }
            }
            exit
        }
    ')
    
    printf '%s' "$value"
}

# ═══════════════════════════════════════════════════════════════════════════════
# VALIDATION FUNCTIONS - Pure functions for Atomic Predictability
# ═══════════════════════════════════════════════════════════════════════════════

is_valid_kebab_case() {
    local name="$1"
    [[ "$name" =~ ^[a-z0-9]+(-[a-z0-9]+)*$ ]]
}

is_valid_category() {
    local category="$1"
    local cat
    for cat in "${VALID_CATEGORIES[@]}"; do
        if [[ "$cat" == "$category" ]]; then
            return 0
        fi
    done
    return 1
}

is_valid_complexity() {
    local complexity="$1"
    local comp
    for comp in "${VALID_COMPLEXITY[@]}"; do
        if [[ "$comp" == "$complexity" ]]; then
            return 0
        fi
    done
    return 1
}

get_category_migration() {
    local category="$1"
    local old_cat
    for old_cat in "${!CATEGORY_MIGRATIONS[@]}"; do
        if [[ "$old_cat" == "$category" ]]; then
            printf '%s' "${CATEGORY_MIGRATIONS[$old_cat]}"
            return 0
        fi
    done
    return 1
}

# ═══════════════════════════════════════════════════════════════════════════════
# SKILL VALIDATION - Main validation logic
# ═══════════════════════════════════════════════════════════════════════════════

validate_skill() {
    local skill_file="$1"
    local skill_name
    skill_name=$(basename "$(dirname "$skill_file")")
    
    log_verbose "Validating: $skill_name"
    
    # Fail Fast: Check file exists
    if [[ ! -f "$skill_file" ]]; then
        log_error "SKILL.md not found for $skill_name"
        MISSING_FIELDS+=("$skill_name: Missing SKILL.md file")
        ((INVALID_SKILLS++))
        return 1
    fi
    
    # Parse frontmatter at boundary
    local frontmatter
    frontmatter=$(extract_frontmatter "$skill_file")
    
    if [[ -z "$frontmatter" ]]; then
        log_error "$skill_name: No frontmatter found (missing --- markers)"
        MISSING_FIELDS+=("$skill_name: Missing YAML frontmatter")
        ((INVALID_SKILLS++))
        return 1
    fi
    
    local has_errors=false
    local skill_issues=()
    
    # Validate required fields
    local name_value
    name_value=$(parse_yaml_value "$frontmatter" "name" || true)
    
    # Check name field
    if [[ -z "$name_value" ]]; then
        skill_issues+=("Missing required field: name")
    elif ! is_valid_kebab_case "$name_value"; then
        skill_issues+=("Invalid name format '$name_value': must be lowercase-kebab")
    elif [[ "$name_value" != "$skill_name" ]]; then
        skill_issues+=("Name mismatch: frontmatter '$name_value' != directory '$skill_name'")
    fi
    
    # Check description field
    local desc_value
    desc_value=$(parse_yaml_value "$frontmatter" "description" || true)
    if [[ -z "$desc_value" ]]; then
        skill_issues+=("Missing required field: description")
    fi
    
    # Check category field
    local category_value
    category_value=$(parse_yaml_value "$frontmatter" "category" || true)
    if [[ -z "$category_value" ]]; then
        skill_issues+=("Missing required field: category")
    else
        if ! is_valid_category "$category_value"; then
            # Check for old category name
            local migrated_category
            if migrated_category=$(get_category_migration "$category_value" 2>/dev/null); then
                skill_issues+=("Old category name '$category_value' should be '$migrated_category'")
                CATEGORY_MIGRATIONS_NEEDED+=("$skill_name: $category_value -> $migrated_category")
            else
                skill_issues+=("Invalid category '$category_value': not in taxonomy")
            fi
        fi
    fi
    
    # Check subcategory field
    local subcategory_value
    subcategory_value=$(parse_yaml_value "$frontmatter" "subcategory" || true)
    if [[ -z "$subcategory_value" ]]; then
        skill_issues+=("Missing field: subcategory (recommended)")
    fi
    
    # Check complexity field (OPTIONAL - user requested NOT to include complexity)
    # We still check if it exists, but don't count it as an issue
    local complexity_value
    complexity_value=$(parse_yaml_value "$frontmatter" "complexity" || true)
    if [[ -n "$complexity_value" ]] && [[ "$FIX_MODE" == "true" ]]; then
        # If complexity exists and we're in fix mode, we should remove it
        log_fix "$skill_name: Will remove complexity field (user preference)"
    fi
    
    # Check targets field
    local targets_value
    targets_value=$(parse_yaml_value "$frontmatter" "targets" || true)
    if [[ -z "$targets_value" ]]; then
        skill_issues+=("Missing recommended field: targets")
    fi
    
    # Report findings
    if [[ ${#skill_issues[@]} -eq 0 ]]; then
        log_success "$skill_name: Valid frontmatter"
        ((VALID_SKILLS++))
        return 0
    else
        log_error "$skill_name: ${#skill_issues[@]} issue(s) found"
        local issue
        for issue in "${skill_issues[@]}"; do
            printf "       - %s\n" "$issue"
            INVALID_VALUES+=("$skill_name: $issue")
        done
        ((INVALID_SKILLS++))
        return 1
    fi
}

# ═══════════════════════════════════════════════════════════════════════════════
# FIX MODE - Apply automatic corrections
# ═══════════════════════════════════════════════════════════════════════════════

apply_fixes() {
    local skill_file="$1"
    local skill_name
    skill_name=$(basename "$(dirname "$skill_file")")
    
    log_verbose "Applying fixes to: $skill_name"
    
    if [[ ! -f "$skill_file" ]]; then
        log_warning "$skill_name: Cannot fix - file not found"
        return 1
    fi
    
    local frontmatter
    frontmatter=$(extract_frontmatter "$skill_file")
    
    if [[ -z "$frontmatter" ]]; then
        log_warning "$skill_name: Cannot fix - no frontmatter found"
        return 1
    fi
    
    local made_changes=false
    local original_frontmatter="$frontmatter"
    local fixed_frontmatter="$frontmatter"
    
    # Fix 1: Update old category names
    local category_value
    category_value=$(parse_yaml_value "$frontmatter" "category" 2>/dev/null || true)
    if [[ -n "$category_value" ]]; then
        local migrated_category
        if migrated_category=$(get_category_migration "$category_value" 2>/dev/null); then
            log_fix "$skill_name: Updating category '$category_value' -> '$migrated_category'"
            fixed_frontmatter=$(printf '%s' "$fixed_frontmatter" | sed "s/^category: *${category_value}$/category: ${migrated_category}/")
            made_changes=true
            FIXED_ITEMS+=("$skill_name: Updated category to '$migrated_category'")
        fi
    fi
    
    # Fix 2: Add missing category if not present (default to 'fundamentals')
    if ! parse_yaml_value "$frontmatter" "category" &>/dev/null; then
        log_fix "$skill_name: Adding missing category (default: fundamentals)"
        fixed_frontmatter=$(printf '%s\ncategory: fundamentals' "$fixed_frontmatter")
        made_changes=true
        FIXED_ITEMS+=("$skill_name: Added default category 'fundamentals'")
    fi
    
    # Fix 3: Add missing complexity if not present (default to 'intermediate')
    if ! parse_yaml_value "$frontmatter" "complexity" &>/dev/null; then
        log_fix "$skill_name: Adding missing complexity (default: intermediate)"
        fixed_frontmatter=$(printf '%s\ncomplexity: intermediate' "$fixed_frontmatter")
        made_changes=true
        FIXED_ITEMS+=("$skill_name: Added default complexity 'intermediate'")
    fi
    
    # Fix 4: Add missing subcategory placeholder
    if ! parse_yaml_value "$frontmatter" "subcategory" &>/dev/null; then
        log_fix "$skill_name: Adding missing subcategory (default: general)"
        fixed_frontmatter=$(printf '%s\nsubcategory: general' "$fixed_frontmatter")
        made_changes=true
        FIXED_ITEMS+=("$skill_name: Added default subcategory 'general'")
    fi
    
    # Fix 5: Add missing targets if not present
    if ! parse_yaml_value "$frontmatter" "targets" &>/dev/null; then
        log_fix "$skill_name: Adding missing targets (default: ['*'])"
        fixed_frontmatter=$(printf "%s\ntargets:\n  - '*'\n" "$fixed_frontmatter")
        made_changes=true
        FIXED_ITEMS+=("$skill_name: Added default targets ['*']")
    fi
    
    # Apply changes to file if any fixes were made
    if [[ "$made_changes" == true ]]; then
        if [[ "$FIX_MODE" == true ]]; then
            # Create backup first
            cp "$skill_file" "$skill_file.bak"
            
            # Extract content after second --- marker
            local content_after_frontmatter
            # Skip first --- to --- block and capture everything after
            content_after_frontmatter=$(awk '/^---$/{if(++count==2){found=1;next}}found' "$skill_file.bak" || true)
            
            # Safety check: ensure we have the original file
            if [[ ! -s "$skill_file.bak" ]]; then
                log_error "$skill_name: Backup file is empty, aborting fix"
                return 1
            fi
            
            # Write new content atomically
            local temp_file
            temp_file=$(mktemp)
            
            {
                printf '%s\n' '---'
                printf '%s\n' "$fixed_frontmatter"
                printf '%s\n' '---'
                if [[ -n "$content_after_frontmatter" ]]; then
                    printf '%s' "$content_after_frontmatter"
                fi
            } > "$temp_file"
            
            # Only replace if temp file is valid
            if [[ -s "$temp_file" ]]; then
                mv "$temp_file" "$skill_file"
                log_success "$skill_name: Applied fixes successfully"
                ((FIXED_SKILLS++))
                rm -f "$skill_file.bak"
            else
                log_error "$skill_name: Generated file is empty, keeping original"
                rm -f "$temp_file"
                mv "$skill_file.bak" "$skill_file"
                return 1
            fi
        else
            log_info "$skill_name: Would apply fixes (use --fix to apply)"
        fi
    else
        log_verbose "$skill_name: No fixes needed"
    fi
    
    return 0
}

# ═══════════════════════════════════════════════════════════════════════════════
# REPORTING - Generate summary report
# ═══════════════════════════════════════════════════════════════════════════════

print_summary_report() {
    echo ""
    echo "╔════════════════════════════════════════════════════════════════════════════╗"
    echo "║                    SKILL FRONTMATTER MIGRATION REPORT                      ║"
    echo "╚════════════════════════════════════════════════════════════════════════════╝"
    echo ""
    
    printf "  Total Skills Scanned:    %d\n" "$TOTAL_SKILLS"
    printf "  ${COLOR_GREEN}Valid Skills:${COLOR_RESET}            %d\n" "$VALID_SKILLS"
    printf "  ${COLOR_RED}Invalid Skills:${COLOR_RESET}          %d\n" "$INVALID_SKILLS"
    
    if [[ "$FIX_MODE" == true ]]; then
        printf "  ${COLOR_CYAN}Skills Fixed:${COLOR_RESET}            %d\n" "$FIXED_SKILLS"
    fi
    
    echo ""
    
    # Show detailed findings if verbose or if there are issues
    if [[ "$VERBOSE" == true ]] || [[ ${#MISSING_FIELDS[@]} -gt 0 ]]; then
        if [[ ${#MISSING_FIELDS[@]} -gt 0 ]]; then
            echo "  Missing Fields:"
            local item
            for item in "${MISSING_FIELDS[@]}"; do
                printf "    ${COLOR_RED}- %s${COLOR_RESET}\n" "$item"
            done
            echo ""
        fi
    fi
    
    if [[ "$VERBOSE" == true ]] || [[ ${#CATEGORY_MIGRATIONS_NEEDED[@]} -gt 0 ]]; then
        if [[ ${#CATEGORY_MIGRATIONS_NEEDED[@]} -gt 0 ]]; then
            echo "  Category Migrations Needed:"
            local item
            for item in "${CATEGORY_MIGRATIONS_NEEDED[@]}"; do
                printf "    ${COLOR_YELLOW}- %s${COLOR_RESET}\n" "$item"
            done
            echo ""
        fi
    fi
    
    if [[ "$FIX_MODE" == true ]] && [[ ${#FIXED_ITEMS[@]} -gt 0 ]]; then
        echo "  Applied Fixes:"
        local item
        for item in "${FIXED_ITEMS[@]}"; do
            printf "    ${COLOR_CYAN}- %s${COLOR_RESET}\n" "$item"
        done
        echo ""
    fi
    
    # Exit status based on findings
    if [[ $INVALID_SKILLS -gt 0 ]]; then
        echo "  Status: ${COLOR_RED}FAILED${COLOR_RESET} - $INVALID_SKILLS skill(s) need attention"
        return 1
    else
        echo "  Status: ${COLOR_GREEN}PASSED${COLOR_RESET} - All skills have valid frontmatter"
        return 0
    fi
}

print_validation_guide() {
    echo ""
    echo "╔════════════════════════════════════════════════════════════════════════════╗"
    echo "║                     VALIDATION RULES REFERENCE                           ║"
    echo "╚════════════════════════════════════════════════════════════════════════════╝"
    echo ""
    echo "  Required Fields:"
    echo "    - name:        lowercase-kebab identifier"
    echo "    - description: Multi-line description with keywords"
    echo "    - category:    One of: ${VALID_CATEGORIES[*]}"
    echo ""
    echo "  Recommended Fields:"
    echo "    - subcategory: Subcategory per taxonomy"
    echo "    - complexity:  One of: ${VALID_COMPLEXITY[*]}"
    echo "    - targets:     Platform compatibility list"
    echo ""
    echo "  Category Migrations (auto-fixable):"
    local old_cat
    for old_cat in "${!CATEGORY_MIGRATIONS[@]}"; do
        printf "    '%s' -> '%s'\n" "$old_cat" "${CATEGORY_MIGRATIONS[$old_cat]}"
    done
    echo ""
}

# ═══════════════════════════════════════════════════════════════════════════════
# MAIN EXECUTION - Entry point with argument parsing
# ═══════════════════════════════════════════════════════════════════════════════

main() {
    # Parse command arguments with Early Exit pattern
    local command=""
    local show_version=false
    
    # Early Exit: No arguments defaults to 'check'
    if [[ $# -eq 0 ]]; then
        command="check"
    else
        # Parse arguments
        while [[ $# -gt 0 ]]; do
            case "$1" in
                check)
                    command="check"
                    shift
                    ;;
                help|--help|-h)
                    print_usage
                    exit 0
                    ;;
                --version|-v)
                    show_version=true
                    shift
                    ;;
                --skill)
                    if [[ -n "${2:-}" ]]; then
                        SPECIFIC_SKILL="$2"
                        shift 2
                    else
                        log_error "--skill requires a skill name"
                        exit 1
                    fi
                    ;;
                --fix)
                    FIX_MODE=true
                    shift
                    ;;
                --verbose)
                    VERBOSE=true
                    shift
                    ;;
                *)
                    log_error "Unknown option: $1"
                    print_usage
                    exit 1
                    ;;
            esac
        done
    fi
    
    # Show version and exit
    if [[ "$show_version" == true ]]; then
        echo "migrate-frontmatter.sh v1.0.0"
        exit 0
    fi
    
    # Default to check command
    if [[ -z "$command" ]]; then
        command="check"
    fi
    
    log_info "Starting frontmatter migration check..."
    log_info "Skills directory: $SKILLS_DIR"
    
    if [[ "$FIX_MODE" == true ]]; then
        log_info "Mode: CHECK + FIX (applying changes)"
    else
        log_info "Mode: CHECK ONLY (dry-run, use --fix to apply)"
    fi
    
    if [[ -n "$SPECIFIC_SKILL" ]]; then
        log_info "Filter: Checking only skill '$SPECIFIC_SKILL'"
    fi
    
    echo ""
    
    # Find all skill directories with SKILL.md files
    local skill_dirs=()
    if [[ -n "$SPECIFIC_SKILL" ]]; then
        # Check specific skill
        if [[ -d "$SKILLS_DIR/$SPECIFIC_SKILL" ]]; then
            skill_dirs=("$SPECIFIC_SKILL")
        else
            log_error "Skill directory not found: $SPECIFIC_SKILL"
            exit 1
        fi
    else
        # Find all skill directories
        while IFS= read -r -d '' dir; do
            local skill_name
            skill_name=$(basename "$dir")
            # Skip non-skill directories (hidden, special files)
            if [[ "$skill_name" != "." ]] && [[ "$skill_name" != ".." ]] && \
               [[ "$skill_name" != ".claude-plugin" ]] && \
               [[ -f "$dir/SKILL.md" ]]; then
                skill_dirs+=("$skill_name")
            fi
        done < <(find "$SKILLS_DIR" -mindepth 1 -maxdepth 1 -type d -print0 2>/dev/null)
    fi
    
    # Fail Fast: No skills found
    if [[ ${#skill_dirs[@]} -eq 0 ]]; then
        log_error "No skill directories found in $SKILLS_DIR"
        exit 1
    fi
    
    TOTAL_SKILLS=${#skill_dirs[@]}
    log_info "Found $TOTAL_SKILLS skill(s) to validate"
    echo ""
    
    # Progress indicator
    local current=0
    local skill_dir
    
    for skill_dir in "${skill_dirs[@]}"; do
        current=$((current + 1))
        local skill_file="$SKILLS_DIR/$skill_dir/SKILL.md"
        
        # Show progress
        if [[ "$VERBOSE" == false ]] && [[ -z "$SPECIFIC_SKILL" ]]; then
            printf "\r  Progress: [%d/%d] %s" "$current" "$TOTAL_SKILLS" "$skill_dir"
        fi
        
        # Validate the skill (ignore return value to continue loop)
        validate_skill "$skill_file" || true
        
        # Apply fixes if in fix mode
        if [[ "$FIX_MODE" == true ]]; then
            apply_fixes "$skill_file"
        elif [[ ${#CATEGORY_MIGRATIONS_NEEDED[@]} -gt 0 ]] && \
              [[ " ${CATEGORY_MIGRATIONS_NEEDED[*]} " =~ " $skill_dir: " ]]; then
            # Show what would be fixed
            apply_fixes "$skill_file"
        fi
    done
    
    # Clear progress line
    if [[ "$VERBOSE" == false ]] && [[ -z "$SPECIFIC_SKILL" ]]; then
        printf '\r%*s\r' 60 ''
    fi
    
    echo ""
    
    # Print summary report
    print_summary_report
    local report_status=$?
    
    # Print validation guide if there were errors
    if [[ $INVALID_SKILLS -gt 0 ]] || [[ ${#CATEGORY_MIGRATIONS_NEEDED[@]} -gt 0 ]]; then
        print_validation_guide
    fi
    
    exit $report_status
}

# ═══════════════════════════════════════════════════════════════════════════════
# SCRIPT ENTRY POINT
# ═══════════════════════════════════════════════════════════════════════════════

main "$@"
