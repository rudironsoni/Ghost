# Test Fix Strategy - E2E and Smoke Tests

## Problem
Tests are failing because they require real external services (LinkedIn, Indeed, etc.)

## Solution
1. Create FakeBrowserFixture that uses FakeBrowserSession + stubbed HTML responses
2. Modify GhostWebApiFixture to register stubbed job clients that return test data
3. Update tests to use the fake fixtures

## Key Components
- FakeBrowserSession (exists in tests/Shared/Ghost.Testing/Fakes/)
- FakePage (exists)
- Need to create: FakeBrowserFixture, StubJobClient, StubGhostKernel

## Files to Modify
- Create: tests/Shared/Ghost.Testing/Fixtures/FakeBrowserFixture.cs
- Modify: tests/Shared/Ghost.Testing/Fakes/FakePage.cs (add ability to return stubbed content)
- Modify: tests/Kernel/Ghost.Kernel.SmokeTests/Smoke/GhostWebApiFixture.cs (use stubs)
- Update: All E2E tests to use FakeBrowserFixture instead of RealBrowserFixture