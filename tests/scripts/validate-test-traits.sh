#!/bin/bash
# validate-test-traits.sh
# Validates that only canonical test trait values are used across all test files.
# Exits with error if non-canonical values are found.

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TESTS_DIR="$(dirname "$SCRIPT_DIR")"

echo "Validating test traits in: $TESTS_DIR"

# Canonical Category values: Unit, Integration, System, End2End
# Non-canonical values that should fail: UnitTest, unit, integration, end2end (wrong casing)

# Find all Category trait declarations in active tests (exclude legacy inventory)
CATEGORY_TRAITS=$(grep -r '\[Trait("Category"' "$TESTS_DIR" --include="*.cs" | grep -v '/Legacy/' || true)

# Check for non-canonical category values
NON_CANONICAL_FOUND=0

# Check for "UnitTest" (should be "Unit")
if echo "$CATEGORY_TRAITS" | grep -q 'Trait("Category", "UnitTest")'; then
    echo "ERROR: Found non-canonical category value 'UnitTest' (should be 'Unit')"
    echo "$CATEGORY_TRAITS" | grep 'Trait("Category", "UnitTest")'
    NON_CANONICAL_FOUND=1
fi

# Check for wrong casing in "unit" (should be "Unit")
if echo "$CATEGORY_TRAITS" | grep -q 'Trait("Category", "unit")'; then
    echo "ERROR: Found non-canonical category value 'unit' (should be 'Unit' with capital U)"
    echo "$CATEGORY_TRAITS" | grep 'Trait("Category", "unit")'
    NON_CANONICAL_FOUND=1
fi

# Check for wrong casing in "integration" (should be "Integration")
if echo "$CATEGORY_TRAITS" | grep -q 'Trait("Category", "integration")'; then
    echo "ERROR: Found non-canonical category value 'integration' (should be 'Integration' with capital I)"
    echo "$CATEGORY_TRAITS" | grep 'Trait("Category", "integration")'
    NON_CANONICAL_FOUND=1
fi

# Check for wrong casing in "system" (should be "System")
if echo "$CATEGORY_TRAITS" | grep -q 'Trait("Category", "system")'; then
    echo "ERROR: Found non-canonical category value 'system' (should be 'System' with capital S)"
    echo "$CATEGORY_TRAITS" | grep 'Trait("Category", "system")'
    NON_CANONICAL_FOUND=1
fi

# Check for wrong casing in "end2end" (should be "End2End")
if echo "$CATEGORY_TRAITS" | grep -q 'Trait("Category", "end2end")'; then
    echo "ERROR: Found non-canonical category value 'end2end' (should be 'End2End' in PascalCase)"
    echo "$CATEGORY_TRAITS" | grep 'Trait("Category", "end2end")'
    NON_CANONICAL_FOUND=1
fi

# Check for deprecated category value "E2E" (must be "End2End")
if echo "$CATEGORY_TRAITS" | grep -q 'Trait("Category", "E2E")'; then
    echo "ERROR: Found deprecated category value 'E2E' (must be 'End2End')"
    echo "$CATEGORY_TRAITS" | grep 'Trait("Category", "E2E")'
    NON_CANONICAL_FOUND=1
fi

# End2End lane ownership: every End2End test file must declare a lane capability trait.
END2END_FILES=$(grep -rl '\[Trait("Category", "End2End")' "$TESTS_DIR" --include="*.cs" | grep -v '/Legacy/' || true)
if [ -n "$END2END_FILES" ]; then
    while IFS= read -r file; do
        if [ -z "$file" ]; then
            continue
        fi

        if ! grep -Eq '\[Trait\("Capability", "(RequiresProviderLive|RequiresSyntheticServer)"\)\]' "$file"; then
            echo "ERROR: End2End test file missing required lane capability trait:"
            echo "  $file"
            echo "  Required: [Trait(\"Capability\", \"RequiresProviderLive\")] or [Trait(\"Capability\", \"RequiresSyntheticServer\")]"
            NON_CANONICAL_FOUND=1
        fi
    done <<< "$END2END_FILES"
fi

# List all unique category values found
echo ""
echo "Category values found in codebase:"
echo "$CATEGORY_TRAITS" | sed -n 's/.*Trait("Category", "\([^"]*\)").*/\1/p' | sort | uniq -c

CAPABILITY_TRAITS=$(grep -r '\[Trait("Capability"' "$TESTS_DIR" --include="*.cs" | grep -v '/Legacy/' || true)
echo ""
echo "Capability values found in codebase:"
if [ -n "$CAPABILITY_TRAITS" ]; then
    echo "$CAPABILITY_TRAITS" | sed -n 's/.*Trait("Capability", "\([^"]*\)").*/\1/p' | sort | uniq -c
else
    echo "  (none)"
fi

if [ $NON_CANONICAL_FOUND -eq 1 ]; then
    echo ""
    echo "FAILURE: Non-canonical test category values found!"
    echo "Canonical values are: Unit, Integration, System, End2End"
    echo "End2End files must include lane capability: RequiresProviderLive or RequiresSyntheticServer"
    exit 1
fi

echo ""
echo "SUCCESS: All test categories use canonical values."
exit 0
