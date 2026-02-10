#!/bin/bash
# validate-test-traits.sh
# Validates that only canonical test trait values are used across all test files.
# Exits with error if non-canonical values are found.

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TESTS_DIR="$(dirname "$SCRIPT_DIR")"

echo "Validating test traits in: $TESTS_DIR"

# Canonical Category values: Unit, Integration, System, E2E
# Non-canonical values that should fail: UnitTest, unit, integration, e2e (wrong casing)

# Find all Category trait declarations
CATEGORY_TRAITS=$(grep -r '\[Trait("Category"' "$TESTS_DIR" --include="*.cs" || true)

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

# Check for wrong casing in "e2e" (should be "E2E")
if echo "$CATEGORY_TRAITS" | grep -q 'Trait("Category", "e2e")'; then
    echo "ERROR: Found non-canonical category value 'e2e' (should be 'E2E' all caps)"
    echo "$CATEGORY_TRAITS" | grep 'Trait("Category", "e2e")'
    NON_CANONICAL_FOUND=1
fi

# List all unique category values found
echo ""
echo "Category values found in codebase:"
echo "$CATEGORY_TRAITS" | sed -n 's/.*Trait("Category", "\([^"]*\)").*/\1/p' | sort | uniq -c

if [ $NON_CANONICAL_FOUND -eq 1 ]; then
    echo ""
    echo "FAILURE: Non-canonical test category values found!"
    echo "Canonical values are: Unit, Integration, System, E2E"
    exit 1
fi

echo ""
echo "SUCCESS: All test categories use canonical values."
exit 0
