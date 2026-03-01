# Flaky Test Governance Policy

## Overview

This policy establishes explicit processes for managing flaky tests in the Ghost project. Flaky tests reduce trust in CI and must be managed with transparency and accountability.

## Policy Requirements

### 1. No Silent Retries in PR Gate

- **MUST**: Fail fast on any test failure in PR gate
- **MUST NOT**: Use automatic retries without explicit documentation
- **MUST NOT**: Suppress flaky test failures in CI

### 2. Quarantine Requirements

When a test is identified as flaky and requires quarantine, the following information MUST be provided:

#### Required Fields

| Field | Description | Validation |
|-------|-------------|------------|
| **Owner** | Person responsible for resolving the flake | Required, non-empty |
| **Expiry Date** | Maximum 30 days from quarantine start | Required, max 30 days |
| **Linked Issue** | Issue ID containing the RCA | Required, non-empty |
| **Reason** | Brief description of the flake behavior | Required, non-empty |

#### Quarantine Behavior

- **Active Quarantine**: Test is skipped with clear message
- **Expired Quarantine**: Test fails with `QuarantineExpiredException`
- **Auto-Expiration**: Tests fail-closed after expiry date

### 3. RCA Template

Every quarantined test MUST have a linked issue with the following Root Cause Analysis (RCA) template completed:

```markdown
## Flaky Test RCA

### Test Information
- **Test Name**: `<Fully qualified test name>`
- **Quarantine Date**: `<YYYY-MM-DD>`
- **Owner**: `<@username>`

### Trigger Condition
Describe the specific condition that triggers the flake:
- What sequence of events causes the failure?
- What timing or race condition is involved?
- What external dependencies are involved?

### Why Not Caught Earlier
Explain why this flake wasn't detected in development:
- Was the test recently added?
- Did a recent change introduce the flake?
- Is the flake environment-specific (CI vs local)?

### Deterministic Reproduction
Provide steps to reproduce the flake deterministically:
1. Step 1
2. Step 2
3. ...

Include any necessary:
- Test data
- Environment configuration
- Timing parameters

### Permanent Fix Proposal
Describe the permanent fix:
- **Approach**: What is the fix strategy?
- **Implementation**: What code changes are needed?
- **Testing**: How will the fix be validated?
- **Risk Assessment**: What are the risks of the fix?

### Owner and SLA
- **Owner**: `<@username>`
- **Target Resolution Date**: `<YYYY-MM-DD>`
- **SLA Status**: On Track / At Risk / Blocked

### Progress Updates
Update this section weekly:
- `<YYYY-MM-DD>`: Update description
- `<YYYY-MM-DD>`: Update description
```

## Flake Budget

### Target Metrics

| Metric | Target | Alert Threshold |
|--------|--------|-----------------|
| **Flake Rate** | < 0.5% | ≥ 0.5% |
| **Window** | 14 days | 14 days |
| **Review Frequency** | Weekly | Weekly |

### Flake Rate Calculation

```
Flake Rate = (Failed Executions in Window) / (Total Executions in Window)
```

### Budget Enforcement

- **Alert**: When flake rate ≥ 0.5%
- **Action Required**: Weekly flake review meeting
- **Escalation**: If flake rate remains ≥ 0.5% for 2 consecutive weeks

## Implementation

### 1. Quarantine Detection

The `FlakyTestTracker` class automatically:

- Tracks test stability metrics
- Records execution results (pass/fail, timing)
- Detects flaky tests based on failure rate threshold
- Maintains 7-day and 14-day rolling windows

**Detection Criteria**:
- Minimum 10 executions
- Failure rate ≥ 10%
- Intermittent failure pattern (not consistent failure)

### 2. Quarantine Attribute

Use the `[Quarantine]` attribute to mark flaky tests:

```csharp
[Quarantine(
    owner: "@username",
    expiryDate: "2026-03-13",
    linkedIssue: "Ghost-xxx",
    reason: "Test fails intermittently due to race condition in async initialization")]
public async Task MyFlakyTest()
{
    // Test implementation
}
```

### 3. Dashboard/Reporting

Generate flake reports using `FlakyTestTracker.GenerateReport()`:

```csharp
var report = FlakyTestTracker.GenerateReport();

Console.WriteLine($"Flake Rate: {report.FlakeRate:P2}");
Console.WriteLine($"Budget Exceeded: {report.BudgetExceeded}");
Console.WriteLine($"Flaky Tests: {report.FlakyTests.Count}");
```

Report includes:
- Overall flake rate
- Budget status
- List of flaky tests with metrics
- Owner accountability

## Workflows

### Adding a Test to Quarantine

1. **Identify the flake**:
   - Review test failure history
   - Reproduce the flake locally
   - Document trigger conditions

2. **Create RCA issue**:
   - Use the RCA template
   - Complete all required sections
   - Assign to owner

3. **Apply quarantine attribute**:
   - Add `[Quarantine]` attribute to test
   - Set expiry date (max 30 days)
   - Link to RCA issue

4. **Track progress**:
   - Update RCA issue weekly
   - Resolve before expiry
   - Remove attribute when fixed

### Resolving a Quarantined Test

1. **Implement permanent fix**:
   - Follow the fix proposal in RCA
   - Add tests to prevent regression
   - Update documentation if needed

2. **Validate fix**:
   - Run test multiple times (≥ 20 executions)
   - Verify no flaky behavior
   - Check CI stability

3. **Remove quarantine**:
   - Remove `[Quarantine]` attribute
   - Close RCA issue
   - Update flake metrics

### Weekly Flake Review

**Attendees**: Tech leads, test owners, CI maintainers

**Agenda**:
1. Review flake rate metrics
2. Review expired quarantines
3. Review new flaky test candidates
4. Assign owners to new flakes
5. Track progress on existing quarantines

**Outputs**:
- Action items for flake resolution
- Quarantine status updates
- Process improvements

## Enforcement

### CI Gate Behavior

- **PR Gate**: Fail on any test failure (no silent retries)
- **Quarantined Tests**: Skipped with clear message
- **Expired Quarantines**: Fail with `QuarantineExpiredException`

### Accountability

- **Owner**: Responsible for resolution before expiry
- **Tech Lead**: Reviews and approves quarantines
- **CI Team**: Monitors flake budget and alerts

### Escalation

If a quarantine expires without resolution:
1. Test fails in CI (blocks PRs)
2. Issue escalated to tech lead
3. Emergency triage meeting scheduled
4. Resolution plan documented

## Metrics and Reporting

### Key Metrics

- **Flake Rate**: Percentage of failed executions over 14 days
- **Quarantine Count**: Number of active quarantines
- **Expired Count**: Number of expired quarantines
- **Resolution Time**: Average time to resolve quarantines
- **Owner Accountability**: Quarantines per owner

### Reporting

- **Daily**: Automated flake rate check (alert if exceeded)
- **Weekly**: Flake review meeting
- **Monthly**: Flaky test governance report

## Best Practices

### Preventing Flaky Tests

1. **Avoid timing dependencies**:
   - Use explicit synchronization
   - Avoid `Thread.Sleep()`
   - Use proper async/await patterns

2. **Isolate tests**:
   - No shared state between tests
   - Clean up resources in `Dispose()`
   - Use fresh test data

3. **Make tests deterministic**:
   - Control external dependencies
   - Use test doubles for external services
   - Seed random data

4. **Add resilience**:
   - Retry only with explicit documentation
   - Add timeouts for async operations
   - Handle transient failures gracefully

### Writing Reliable Tests

```csharp
public class ReliableTestExample
{
    [Fact]
    public async Task ShouldHandleAsyncOperation()
    {
        // Arrange
        var service = new MyService();
        var cancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token;

        // Act
        var result = await service.DoSomethingAsync(cancellationToken);

        // Assert
        result.Should().NotBeNull();
    }

    public void Dispose()
    {
        // Clean up resources
    }
}
```

## References

- **Implementation**: `tests/Shared/Ghost.Testing/Reliability/FlakyTestGovernance.cs`
- **Testing Reference**: `docs/testing-reference.md`
- **Agent Playbook**: `docs/agent-playbook.md`
- **Testing Architecture RFC**: `docs/rfc/testing-architecture-overhaul.md` - Comprehensive test architecture with deterministic lanes and flake governance

## Change History

| Date | Version | Changes |
|------|---------|---------|
| 2026-02-11 | 1.0 | Initial policy |
