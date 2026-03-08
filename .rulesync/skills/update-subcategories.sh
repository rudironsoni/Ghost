#!/bin/bash
#
# update-subcategories.sh
# Comprehensive script to add subcategories to all skills and remove complexity fields
#
# Usage:
#   ./update-subcategories.sh              # Run normally with backups
#   ./update-subcategories.sh --dry-run    # Preview changes without modifying
#   ./update-subcategories.sh --no-backup  # Run without creating backups
#
# Features:
#   - Reads TAXONOMY.md for valid subcategories
#   - Determines subcategory based on skill name and category
#   - Adds subcategory field to frontmatter
#   - Removes complexity field
#   - Removes complexity values from tags
#   - Creates backups before modification
#   - Processes in batches with progress reporting
#   - Idempotent - safe to run multiple times
#

# set -e  # Disabled to prevent exit on arithmetic operations in while loops

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SKILLS_DIR="${SKILLS_DIR:-$SCRIPT_DIR}"
TAXONOMY_FILE="$SKILLS_DIR/TAXONOMY.md"
BACKUP_DIR="$SKILLS_DIR/.backups/$(date +%Y%m%d_%H%M%S)"
BATCH_SIZE=30
DRY_RUN=false
NO_BACKUP=false

# Counters
TOTAL_SKILLS=0
PROCESSED=0
UPDATED=0
SKIPPED=0
ERRORS=0
BACKUPS_CREATED=0

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Parse arguments
parse_args() {
    while [[ $# -gt 0 ]]; do
        case $1 in
            --dry-run)
                DRY_RUN=true
                echo -e "${BLUE}DRY RUN MODE: No files will be modified${NC}"
                shift
                ;;
            --no-backup)
                NO_BACKUP=true
                echo -e "${YELLOW}NO BACKUP MODE: Backups will not be created${NC}"
                shift
                ;;
            --help|-h)
                echo "Usage: $0 [OPTIONS]"
                echo ""
                echo "Options:"
                echo "  --dry-run      Preview changes without modifying files"
                echo "  --no-backup    Skip creating backups (not recommended)"
                echo "  --help, -h     Show this help message"
                echo ""
                echo "Environment Variables:"
                echo "  SKILLS_DIR     Path to skills directory (default: script directory)"
                exit 0
                ;;
            *)
                echo -e "${RED}Unknown option: $1${NC}"
                echo "Use --help for usage information"
                exit 1
                ;;
        esac
    done
}

# Logging functions
log_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

log_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Get skill category from frontmatter
get_skill_category() {
    local skill_file="$1"
    sed -n '/^---$/,/^---$/p' "$skill_file" | grep "^category:" | head -1 | sed 's/category:[[:space:]]*//' | tr -d '"' | tr -d "'" | xargs 2>/dev/null || echo ""
}

# Get skill name from frontmatter
get_skill_name() {
    local skill_file="$1"
    sed -n '/^---$/,/^---$/p' "$skill_file" | grep "^name:" | head -1 | sed 's/name:[[:space:]]*//' | tr -d '"' | tr -d "'" | xargs 2>/dev/null || basename "$(dirname "$skill_file")"
}

# Get existing subcategory from frontmatter
get_existing_subcategory() {
    local skill_file="$1"
    sed -n '/^---$/,/^---$/p' "$skill_file" | grep "^subcategory:" | head -1 | sed 's/subcategory:[[:space:]]*//' | tr -d '"' | tr -d "'" | xargs 2>/dev/null || echo ""
}

# Determine subcategory based on skill name and category
determine_subcategory() {
    local skill_name="$1"
    local category="$2"
    local subcategory=""

    case "$category" in
        fundamentals)
            if [[ "$skill_name" == *"coding-standards" ]]; then
                subcategory="coding-standards"
            elif [[ "$skill_name" == *"modern-patterns" ]] || [[ "$skill_name" == *"async-patterns" ]] || [[ "$skill_name" == *"concurrency-patterns" ]]; then
                subcategory="language-patterns"
            elif [[ "$skill_name" == *"solid-principles" ]]; then
                subcategory="design-principles"
            elif [[ "$skill_name" == *"dependency-injection" ]] || [[ "$skill_name" == *"configuration" ]]; then
                subcategory="di-and-services"
            elif [[ "$skill_name" == *"code-smells" ]] || [[ "$skill_name" == *"diagnostics" ]]; then
                subcategory="diagnostics"
            elif [[ "$skill_name" == *"fundamentals" ]]; then
                subcategory="overview"
            else
                subcategory="coding-standards"
            fi
            ;;

        testing)
            if [[ "$skill_name" == *"unit-test-fundamentals" ]] || [[ "$skill_name" == *"test-naming" ]] || [[ "$skill_name" == *"project-setup" ]]; then
                subcategory="fundamentals"
            elif [[ "$skill_name" == *"xunit" && "$skill_name" != *"xunit-upgrade"* ]]; then
                subcategory="frameworks"
            elif [[ "$skill_name" == *"integration-testing" ]] || [[ "$skill_name" == *"testcontainers"* ]] || [[ "$skill_name" == *"aspire-testing" ]] || [[ "$skill_name" == *"webapi-integration" ]] || [[ "$skill_name" == *"aspnet-integration" ]]; then
                subcategory="integration"
            elif [[ "$skill_name" == *"nsubstitute"* ]] || [[ "$skill_name" == *"mocking" ]]; then
                subcategory="mocking"
            elif [[ "$skill_name" == *"autofixture"* ]] || [[ "$skill_name" == *"bogus"* ]] || [[ "$skill_name" == *"test-data-builder" ]]; then
                subcategory="test-data"
            elif [[ "$skill_name" == *"assertions"* ]] || [[ "$skill_name" == *"awesome-assertions"* ]]; then
                subcategory="assertions"
            elif [[ "$skill_name" == *"fluentvalidation" ]]; then
                subcategory="specialized"
            elif [[ "$skill_name" == *"datetime"* ]] || [[ "$skill_name" == *"timeprovider" ]] || [[ "$skill_name" == *"filesystem"* ]] || [[ "$skill_name" == *"private"* ]]; then
                subcategory="specialized"
            elif [[ "$skill_name" == *"code-coverage" ]] || [[ "$skill_name" == *"coverage" ]]; then
                subcategory="coverage"
            elif [[ "$skill_name" == *"complex-object"* ]] || [[ "$skill_name" == *"object-comparison" ]]; then
                subcategory="assertions"
            elif [[ "$skill_name" == *"test-output"* ]] || [[ "$skill_name" == *"logging" ]]; then
                subcategory="fundamentals"
            elif [[ "$skill_name" == *"autodata"* ]]; then
                subcategory="test-data"
            elif [[ "$skill_name" == *"snapshot"* ]]; then
                subcategory="assertions"
            elif [[ "$skill_name" == *"tunit"* ]]; then
                subcategory="frameworks"
            elif [[ "$skill_name" == *"xunit-upgrade" ]]; then
                subcategory="frameworks"
            elif [[ "$skill_name" == *"testing" && "$skill_name" != "dotnet-testing" ]]; then
                subcategory="fundamentals"
            else
                subcategory="fundamentals"
            fi
            ;;

        architecture)
            if [[ "$skill_name" == *"architecture-patterns" ]]; then
                subcategory="patterns"
            elif [[ "$skill_name" == *"domain-modeling"* ]] || [[ "$skill_name" == *"ddd"* ]]; then
                subcategory="domain-modeling"
            elif [[ "$skill_name" == *"messaging-patterns" ]] || [[ "$skill_name" == *"channels" ]]; then
                subcategory="messaging"
            elif [[ "$skill_name" == *"resilience"* ]]; then
                subcategory="resilience"
            elif [[ "$skill_name" == *"architecture" ]] && [[ "$skill_name" != *"patterns"* ]]; then
                subcategory="overview"
            elif [[ "$skill_name" == *"solid-principles" ]]; then
                subcategory="patterns"
            elif [[ "$skill_name" == *"background-services" ]]; then
                subcategory="patterns"
            elif [[ "$skill_name" == *"observability" ]] || [[ "$skill_name" == *"structured-logging" ]]; then
                subcategory="patterns"
            elif [[ "$skill_name" == *"api-versioning" ]]; then
                subcategory="api-design"
            elif [[ "$skill_name" == *"middleware-patterns" ]]; then
                subcategory="api-design"
            elif [[ "$skill_name" == *"service-communication" ]]; then
                subcategory="messaging"
            elif [[ "$skill_name" == *"aspire-patterns" ]]; then
                subcategory="patterns"
            elif [[ "$skill_name" == *"realtime"* ]] || [[ "$skill_name" == *"grpc" ]]; then
                subcategory="messaging"
            else
                subcategory="patterns"
            fi
            ;;

        web)
            if [[ "$skill_name" == *"minimal-api"* ]] || [[ "$skill_name" == *"api-versioning" ]] || [[ "$skill_name" == *"openapi" ]]; then
                subcategory="minimal-apis"
            elif [[ "$skill_name" == *"blazor-patterns" ]] || [[ "$skill_name" == *"blazor-components" ]] || [[ "$skill_name" == *"blazor-testing" ]] || [[ "$skill_name" == *"blazor-auth" ]]; then
                subcategory="blazor"
            elif [[ "$skill_name" == *"middleware-patterns" ]]; then
                subcategory="middleware"
            elif [[ "$skill_name" == *"authentication"* ]] || [[ "$skill_name" == *"api-security" ]]; then
                subcategory="security"
            elif [[ "$skill_name" == *"validation"* ]] || [[ "$skill_name" == *"input-validation" ]]; then
                subcategory="validation"
            elif [[ "$skill_name" == *"api-design" ]]; then
                subcategory="api-design"
            elif [[ "$skill_name" == *"playwright" ]]; then
                subcategory="blazor"
            elif [[ "$skill_name" == *"accessibility" ]]; then
                subcategory="blazor"
            else
                subcategory="minimal-apis"
            fi
            ;;

        data)
            if [[ "$skill_name" == *"efcore"* ]]; then
                subcategory="ef-core"
            elif [[ "$skill_name" == *"data-access"* ]]; then
                subcategory="data-access"
            elif [[ "$skill_name" == *"caching" ]]; then
                subcategory="caching"
            elif [[ "$skill_name" == *"serialization" ]]; then
                subcategory="serialization"
            elif [[ "$skill_name" == *"channels" ]]; then
                subcategory="messaging"
            else
                subcategory="ef-core"
            fi
            ;;

        performance)
            if [[ "$skill_name" == *"gc"* ]] || [[ "$skill_name" == *"memory" ]]; then
                subcategory="memory"
            elif [[ "$skill_name" == *"benchmarkdotnet" ]] || [[ "$skill_name" == *"benchmark"* ]]; then
                subcategory="benchmarking"
            elif [[ "$skill_name" == *"profiling" ]]; then
                subcategory="profiling"
            elif [[ "$skill_name" == *"aot"* ]] || [[ "$skill_name" == *"native"* ]] || [[ "$skill_name" == *"trimming" ]] || [[ "$skill_name" == *"aot-wasm" ]]; then
                subcategory="aot"
            elif [[ "$skill_name" == *"performance-patterns" ]] || [[ "$skill_name" == *"type-design"* ]]; then
                subcategory="patterns"
            elif [[ "$skill_name" == *"performance" ]]; then
                subcategory="overview"
            elif [[ "$skill_name" == *"linq-optimization" ]]; then
                subcategory="patterns"
            elif [[ "$skill_name" == *"io-pipelines" ]]; then
                subcategory="patterns"
            elif [[ "$skill_name" == *"ci-benchmarking" ]]; then
                subcategory="benchmarking"
            else
                subcategory="patterns"
            fi
            ;;

        security)
            if [[ "$skill_name" == *"security-owasp"* ]] || [[ "$skill_name" == *"owasp"* ]]; then
                subcategory="owasp"
            elif [[ "$skill_name" == *"crypto"* ]]; then
                subcategory="crypto"
            elif [[ "$skill_name" == *"auth"* ]] || [[ "$skill_name" == *"blazor-auth" ]] || [[ "$skill_name" == *"api-security" ]]; then
                subcategory="auth"
            elif [[ "$skill_name" == *"secrets"* ]]; then
                subcategory="secrets"
            elif [[ "$skill_name" == *"security" ]]; then
                subcategory="overview"
            else
                subcategory="owasp"
            fi
            ;;

        operations)
            if [[ "$skill_name" == *"gha"* ]] || [[ "$skill_name" == *"ado"* ]] || [[ "$skill_name" == *"add-ci" ]]; then
                subcategory="ci-cd"
            elif [[ "$skill_name" == *"container-deployment" ]]; then
                subcategory="deployment"
            elif [[ "$skill_name" == *"containers" ]]; then
                subcategory="containers"
            elif [[ "$skill_name" == *"deploy"* ]] || [[ "$skill_name" == *"release"* ]] || [[ "$skill_name" == *"github-releases" ]]; then
                subcategory="release"
            elif [[ "$skill_name" == *"gha-deploy" ]]; then
                subcategory="deployment"
            elif [[ "$skill_name" == *"cli-release"* ]]; then
                subcategory="release"
            elif [[ "$skill_name" == *"ado-publish" ]] || [[ "$skill_name" == *"gha-publish" ]]; then
                subcategory="ci-cd"
            elif [[ "$skill_name" == *"cli-packaging" ]]; then
                subcategory="release"
            elif [[ "$skill_name" == *"build-analysis" ]] || [[ "$skill_name" == *"build-optimization" ]]; then
                subcategory="ci-cd"
            else
                subcategory="ci-cd"
            fi
            ;;

        ui-frameworks)
            if [[ "$skill_name" == *"maui"* ]]; then
                subcategory="maui"
            elif [[ "$skill_name" == *"wpf"* ]]; then
                subcategory="wpf"
            elif [[ "$skill_name" == *"winui"* ]]; then
                subcategory="winui"
            elif [[ "$skill_name" == *"blazor"* ]]; then
                subcategory="blazor"
            elif [[ "$skill_name" == *"uno"* ]]; then
                subcategory="uno"
            elif [[ "$skill_name" == *"winforms"* ]]; then
                subcategory="winforms"
            elif [[ "$skill_name" == *"ui-testing"* ]]; then
                subcategory="maui"
            elif [[ "$skill_name" == *"ui-chooser" ]]; then
                subcategory="maui"
            elif [[ "$skill_name" == *"accessibility" ]]; then
                subcategory="maui"
            else
                subcategory="maui"
            fi
            ;;

        developer-experience)
            if [[ "$skill_name" == *"cli"* ]] || [[ "$skill_name" == *"system-commandline" ]] || [[ "$skill_name" == *"spectre"* ]]; then
                subcategory="cli"
            elif [[ "$skill_name" == *"analyzers"* ]] || [[ "$skill_name" == *"roslyn"* ]] || [[ "$skill_name" == *"slopwatch" ]]; then
                subcategory="analyzers"
            elif [[ "$skill_name" == *"tool-management" ]]; then
                subcategory="tools"
            elif [[ "$skill_name" == *"msbuild"* ]] || [[ "$skill_name" == *"msbuild-tasks" ]] || [[ "$skill_name" == *"csproj"* ]]; then
                subcategory="msbuild"
            elif [[ "$skill_name" == *"nuget"* ]]; then
                subcategory="nuget"
            elif [[ "$skill_name" == *"project-structure" ]] || [[ "$skill_name" == *"scaffold"* ]] || [[ "$skill_name" == *"solution-navigation" ]] || [[ "$skill_name" == *"project-analysis" ]]; then
                subcategory="project"
            elif [[ "$skill_name" == *"api-docs" ]] || [[ "$skill_name" == *"documentation-strategy" ]] || [[ "$skill_name" == *"xml-docs" ]] || [[ "$skill_name" == *"mermaid"* ]]; then
                subcategory="docs"
            elif [[ "$skill_name" == *"serena"* ]]; then
                subcategory="serena"
            elif [[ "$skill_name" == *"rulesync"* ]]; then
                subcategory="rulesync"
            elif [[ "$skill_name" == *"github-docs" ]]; then
                subcategory="docs"
            elif [[ "$skill_name" == *"version-detection" ]] || [[ "$skill_name" == *"version-upgrade" ]]; then
                subcategory="project"
            elif [[ "$skill_name" == *"editorconfig" ]]; then
                subcategory="analyzers"
            elif [[ "$skill_name" == *"add-analyzers" ]]; then
                subcategory="analyzers"
            elif [[ "$skill_name" == *"localization" ]]; then
                subcategory="project"
            elif [[ "$skill_name" == *"terminal-gui" ]]; then
                subcategory="cli"
            elif [[ "$skill_name" == *"file-io" ]]; then
                subcategory="cli"
            elif [[ "$skill_name" == *"file-based-apps" ]]; then
                subcategory="cli"
            elif [[ "$skill_name" == *"cli-distribution" ]]; then
                subcategory="cli"
            elif [[ "$skill_name" == *"cli-architecture" ]]; then
                subcategory="cli"
            elif [[ "$skill_name" == *"advisor" ]]; then
                subcategory="analyzers"
            elif [[ "$skill_name" == *"modernize" ]]; then
                subcategory="analyzers"
            elif [[ "$skill_name" == *"gotchas" ]]; then
                subcategory="analyzers"
            elif [[ "$skill_name" == *"api-surface-validation" ]]; then
                subcategory="analyzers"
            elif [[ "$skill_name" == *"test-quality" ]]; then
                subcategory="analyzers"
            elif [[ "$skill_name" == *"artifacts-output" ]]; then
                subcategory="msbuild"
            elif [[ "$skill_name" == *"multi-targeting" ]]; then
                subcategory="project"
            else
                subcategory="cli"
            fi
            ;;

        *)
            # Unknown category, try to guess from name
            log_warning "Unknown category '$category' for $skill_name, attempting to guess"
            if [[ "$skill_name" == wiki-* ]]; then
                subcategory="wiki"
            elif [[ "$skill_name" == mcp-* ]]; then
                subcategory="mcp"
            elif [[ "$skill_name" == deep-* ]]; then
                subcategory="deep"
            elif [[ "$skill_name" == github* ]]; then
                subcategory="github"
            elif [[ "$skill_name" == agentic-* ]]; then
                subcategory="ai"
            elif [[ "$skill_name" == ai-* ]]; then
                subcategory="ai"
            elif [[ "$skill_name" == microsoft-* ]]; then
                subcategory="mcp"
            elif [[ "$skill_name" == *"interop"* ]]; then
                subcategory="native"
            elif [[ "$skill_name" == *"windbg"* ]]; then
                subcategory="diagnostics"
            else
                subcategory="general"
            fi
            ;;
    esac

    echo "$subcategory"
}

# Check if tags contain complexity values
tags_have_complexity() {
    local file="$1"
    sed -n '/^---$/,/^---$/p' "$file" | grep -E "^  -[[:space:]]*(beginner|intermediate|advanced)" > /dev/null 2>&1
}

# Process a single skill file
process_skill() {
    local skill_file="$1"
    local skill_name
    local category
    local existing_subcategory
    local new_subcategory
    local needs_update=false
    local changes_desc=""

    skill_name=$(get_skill_name "$skill_file")
    category=$(get_skill_category "$skill_file")

    if [[ -z "$category" ]]; then
        log_error "No category found in $skill_file"
        ERRORS=$((ERRORS + 1))
        return 1
    fi

    # Determine the appropriate subcategory
    new_subcategory=$(determine_subcategory "$skill_name" "$category")

    existing_subcategory=$(get_existing_subcategory "$skill_file")

    # Check if file has complexity field
    local has_complexity=false
    if grep -q "^complexity:" "$skill_file" 2>/dev/null; then
        has_complexity=true
    fi

    # Check if tags contain complexity values
    local has_complexity_in_tags=false
    if tags_have_complexity "$skill_file"; then
        has_complexity_in_tags=true
    fi

    # Check if we need to make changes
    if [[ "$existing_subcategory" != "$new_subcategory" ]]; then
        needs_update=true
        if [[ -n "$existing_subcategory" ]]; then
            changes_desc="subcategory: $existing_subcategory → $new_subcategory"
        else
            changes_desc="add subcategory: $new_subcategory"
        fi
    fi

    if [[ "$has_complexity" == true ]]; then
        needs_update=true
        if [[ -n "$changes_desc" ]]; then
            changes_desc="$changes_desc, remove complexity"
        else
            changes_desc="remove complexity"
        fi
    fi

    if [[ "$has_complexity_in_tags" == true ]]; then
        needs_update=true
        if [[ -n "$changes_desc" ]]; then
            changes_desc="$changes_desc, clean tags"
        else
            changes_desc="clean tags"
        fi
    fi

    if [[ "$needs_update" == false ]]; then
        log_info "SKIPPED: $skill_name (no changes needed)"
        SKIPPED=$((SKIPPED + 1))
        return 0
    fi

    if [[ "$DRY_RUN" == true ]]; then
        log_info "WOULD UPDATE: $skill_name - $changes_desc"
        UPDATED=$((UPDATED + 1))
        return 0
    fi

    # Create backup
    if [[ "$NO_BACKUP" == false ]]; then
        local rel_path
        rel_path=$(dirname "$skill_file" | sed "s|$SKILLS_DIR/||")
        local backup_subdir="$BACKUP_DIR/$rel_path"
        mkdir -p "$backup_subdir"
        cp "$skill_file" "$backup_subdir/SKILL.md"
        BACKUPS_CREATED=$((BACKUPS_CREATED + 1))
    fi

    # Create a Python script to do the actual file modification
    # This is more reliable than complex sed/awk for YAML manipulation
    local temp_py
    temp_py=$(mktemp)

    cat > "$temp_py" << 'PYTHON_SCRIPT'
import re
import sys

skill_file = sys.argv[1]
new_subcategory = sys.argv[2]
remove_complexity = sys.argv[3] == "true"
clean_tags = sys.argv[4] == "true"

with open(skill_file, 'r') as f:
    content = f.read()

# Find the frontmatter
frontmatter_match = re.search(r'^---\n(.*?)\n---', content, re.DOTALL)
if not frontmatter_match:
    print("No frontmatter found")
    sys.exit(1)

frontmatter = frontmatter_match.group(1)
lines = frontmatter.split('\n')
new_lines = []
in_tags = False
tags_multiline = False
found_subcategory = False
found_complexity = False

i = 0
while i < len(lines):
    line = lines[i]
    
    # Handle subcategory
    if line.startswith('subcategory:'):
        new_lines.append(f'subcategory: {new_subcategory}')
        found_subcategory = True
        i += 1
        continue
    
    # Skip complexity
    if line.startswith('complexity:'):
        found_complexity = True
        i += 1
        continue
    
    # Handle tags
    if line.startswith('tags:'):
        if line == 'tags:':
            # Multi-line format
            new_lines.append(line)
            in_tags = True
            tags_multiline = True
            i += 1
            continue
        else:
            # Single line format: tags: [item1, item2]
            tags_content = line[5:].strip()  # Remove 'tags:'
            # Parse array
            if tags_content.startswith('[') and tags_content.endswith(']'):
                inner = tags_content[1:-1]
                items = [item.strip().strip('"\'') for item in inner.split(',')]
                # Filter out complexity values
                filtered = [item for item in items if item not in ['beginner', 'intermediate', 'advanced'] and item]
                if filtered:
                    new_lines.append(f'tags: [{", ".join(f"{item}" if " " in item or "," in item else item for item in filtered)}]')
            else:
                new_lines.append(line)
            in_tags = False
            tags_multiline = False
            i += 1
            continue
    
    # Handle multi-line tags
    if in_tags and tags_multiline:
        # Check if this is a tag item
        tag_match = re.match(r'^\s+-\s+(.+)$', line)
        if tag_match:
            tag_value = tag_match.group(1).strip().strip('"\'')
            if tag_value in ['beginner', 'intermediate', 'advanced']:
                # Skip complexity tags
                i += 1
                continue
            else:
                new_lines.append(line)
                i += 1
                continue
        else:
            # End of tags section
            in_tags = False
    
    new_lines.append(line)
    i += 1

# Add subcategory if not found
if not found_subcategory:
    # Find category line and insert after it
    for idx, line in enumerate(new_lines):
        if line.startswith('category:'):
            new_lines.insert(idx + 1, f'subcategory: {new_subcategory}')
            break

new_frontmatter = '\n'.join(new_lines)
new_content = '---\n' + new_frontmatter + '\n---' + content[frontmatter_match.end():]

with open(skill_file, 'w') as f:
    f.write(new_content)

print(f"Updated {skill_file}")
PYTHON_SCRIPT

    python3 "$temp_py" "$skill_file" "$new_subcategory" "$has_complexity" "$has_complexity_in_tags"
    rm "$temp_py"

    log_success "UPDATED: $skill_name - $changes_desc"
    UPDATED=$((UPDATED + 1))
}

# Print summary
print_summary() {
    echo ""
    echo "========================================"
    echo "         PROCESSING COMPLETE"
    echo "========================================"
    echo ""
    echo "Total Skills Scanned:  $TOTAL_SKILLS"
    echo "Skills Processed:      $PROCESSED"
    echo "Skills Updated:        $UPDATED"
    echo "Skills Skipped:        $SKIPPED"
    echo "Errors:                $ERRORS"
    if [[ "$NO_BACKUP" == false && "$DRY_RUN" == false ]]; then
        echo "Backups Created:       $BACKUPS_CREATED"
        echo "Backup Location:       $BACKUP_DIR"
    fi
    echo ""

    if [[ $ERRORS -gt 0 ]]; then
        echo -e "${RED}Completed with $ERRORS error(s)${NC}"
        exit 1
    else
        echo -e "${GREEN}All operations completed successfully${NC}"
        exit 0
    fi
}

# Main function
main() {
    parse_args "$@"

    log_info "Starting skill subcategory update process..."
    log_info "Skills directory: $SKILLS_DIR"

    # Verify taxonomy file exists
    if [[ ! -f "$TAXONOMY_FILE" ]]; then
        log_error "TAXONOMY.md not found at $TAXONOMY_FILE"
        exit 1
    fi

    # Create backup directory
    if [[ "$NO_BACKUP" == false && "$DRY_RUN" == false ]]; then
        mkdir -p "$BACKUP_DIR"
        log_info "Backup directory created: $BACKUP_DIR"
    fi

    # Find all SKILL.md files (excluding backup files and .bak files)
    local skill_files
    skill_files=$(find "$SKILLS_DIR" -maxdepth 2 -name "SKILL.md" -type f ! -path "*/.backups/*" ! -name "*.bak" | sort)

    TOTAL_SKILLS=$(echo "$skill_files" | grep -c '^' || echo "0")
    log_info "Found $TOTAL_SKILLS skill files to process"
    echo ""

    # Process each skill
    local batch_count=0
    while IFS= read -r skill_file; do
        if [[ -n "$skill_file" ]]; then
            PROCESSED=$((PROCESSED + 1))
            
            # Show batch header every BATCH_SIZE skills
            if [[ $((PROCESSED % BATCH_SIZE)) -eq 1 ]]; then
                ((batch_count++))
                echo ""
                log_info "Processing batch $batch_count (skills $PROCESSED-$((PROCESSED + BATCH_SIZE - 1)))..."
            fi

            # Process the skill
            process_skill "$skill_file"
        fi
    done <<< "$skill_files"

    print_summary
}

# Run main
main "$@"
