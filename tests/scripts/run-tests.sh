#!/bin/bash
# Main test runner with guaranteed cleanup
cd "$(dirname "$0")/../.."

./tests/scripts/pre-test.sh
dotnet test "$@"
EXIT_CODE=$?
./tests/scripts/post-test.sh

exit $EXIT_CODE
