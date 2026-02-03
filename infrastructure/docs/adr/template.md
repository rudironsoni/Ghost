# Architecture Decision Record Template

**ADR Number:** [ADR-XXX]  
**Title:** [Short descriptive title]  
**Date:** [YYYY-MM-DD]  
**Status:** [Proposed | Accepted | Deprecated | Superseded]  
**Deciders:** [List of people involved in the decision]  
**Technical Story:** [Link to ticket/issue if applicable]

## Context and Problem Statement

[Describe the context and problem statement, e.g., in free form using two to three sentences. You may want to articulate the problem in form of a question.]

**Related Context:**
- [Any related architectural context]
- [Dependencies or constraints]
- [Business drivers or requirements]

## Decision Drivers

* [driver 1, e.g., a force, facing concern, …]
* [driver 2, e.g., a force, facing concern, …]
* [driver 3, e.g., a force, facing concern, …]

## Considered Options

### Option 1: [Title]

**Description:**
[Detailed description of the option]

**Pros:**
* [Pro 1]
* [Pro 2]
* [Pro 3]

**Cons:**
* [Con 1]
* [Con 2]
* [Con 3]

**Cost:**
* Initial: $[estimate]
* Monthly: $[estimate]

**Risks:**
* [Risk 1]
* [Risk 2]

---

### Option 2: [Title]

**Description:**
[Detailed description of the option]

**Pros:**
* [Pro 1]
* [Pro 2]
* [Pro 3]

**Cons:**
* [Con 1]
* [Con 2]
* [Con 3]

**Cost:**
* Initial: $[estimate]
* Monthly: $[estimate]

**Risks:**
* [Risk 1]
* [Risk 2]

---

### Option 3: [Title]

**Description:**
[Detailed description of the option]

**Pros:**
* [Pro 1]
* [Pro 2]
* [Pro 3]

**Cons:**
* [Con 1]
* [Con 2]
* [Con 3]

**Cost:**
* Initial: $[estimate]
* Monthly: $[estimate]

**Risks:**
* [Risk 1]
* [Risk 2]

## Decision Outcome

**Chosen option:** "[Option X]"

**Justification:**
[Explain why this option was chosen. Include reasoning about how it addresses the problem statement and decision drivers.]

## Consequences

### Positive Consequences

* [e.g., improvement of quality attribute satisfaction, follow-up decisions required, …]
* [...]

### Negative Consequences

* [e.g., compromising quality attribute, follow-up decisions required, …]
* [...]

### Neutral Consequences

* [e.g., additional work required, …]
* [...]

## Implementation

### Migration Path

1. [Step 1]
2. [Step 2]
3. [Step 3]

### Timeline

| Phase | Duration | Owner | Status |
|-------|----------|-------|--------|
| Design | [X weeks] | [Name] | [Not Started/In Progress/Complete] |
| Implementation | [X weeks] | [Name] | [Not Started/In Progress/Complete] |
| Testing | [X weeks] | [Name] | [Not Started/In Progress/Complete] |
| Rollout | [X weeks] | [Name] | [Not Started/In Progress/Complete] |

### Success Criteria

- [ ] [Criterion 1]
- [ ] [Criterion 2]
- [ ] [Criterion 3]
- [ ] [Criterion 4]

### Rollback Plan

[Describe how to rollback if the decision doesn't work out]

## Monitoring and Validation

### Metrics to Track

| Metric | Target | Measurement Method |
|--------|--------|-------------------|
| [Metric 1] | [Value] | [How to measure] |
| [Metric 2] | [Value] | [How to measure] |
| [Metric 3] | [Value] | [How to measure] |

### Validation Period

* **Duration:** [X weeks/months]
* **Review Date:** [YYYY-MM-DD]
* **Success Threshold:** [Define what success looks like]

## Links

* [Link to implementation]
* [Link to related ADRs]
* [Link to documentation]
* [Link to discussions]

## Appendix

### Research and References

* [Reference 1]
* [Reference 2]
* [Reference 3]

### Discussion Notes

[Any important points from discussions that led to this decision]

### Alternative Approaches Briefly Considered

* [Alternative 1] - Rejected because [reason]
* [Alternative 2] - Rejected because [reason]

---

## Change History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | [YYYY-MM-DD] | [Name] | Initial version |

---

## Review and Approval

| Role | Name | Date | Signature |
|------|------|------|-----------|
| Author | [Name] | [Date] | |
| Reviewer | [Name] | [Date] | |
| Approver | [Name] | [Date] | |

---

## ADR Usage Guidelines

### When to Create an ADR

Create an ADR when making a decision that:
- Has significant impact on the system architecture
- Affects multiple teams or components
- Involves trade-offs between quality attributes
- Has long-term consequences
- Costs significant time or money to implement
- Is difficult to reverse

### ADR Status Definitions

- **Proposed:** The ADR is under discussion
- **Accepted:** The decision has been approved and will be implemented
- **Deprecated:** The decision is no longer relevant but was never replaced
- **Superseded:** The decision has been replaced by a newer ADR (link to new ADR)

### Best Practices

1. **Be Specific:** Provide enough detail for someone unfamiliar with the context to understand the decision
2. **Be Honest:** Document both pros and cons objectively
3. **Be Timely:** Create ADRs before or during implementation, not after
4. **Be Collaborative:** Involve relevant stakeholders in the decision process
5. **Be Concise:** Keep ADRs focused on one decision
6. **Be Current:** Update status when decisions are superseded

### ADR Naming Convention

```
ADR-[XXX]-[short-title].md
```

Examples:
- `ADR-001-kubernetes-over-docker-compose.md`
- `ADR-002-terraform-infrastructure-as-code.md`
- `ADR-003-gitops-with-argocd.md`

### File Location

```
infrastructure/docs/adr/
├── README.md                           # Index of all ADRs
├── template.md                         # This template
├── ADR-001-kubernetes-deployment.md
├── ADR-002-database-selection.md
└── ADR-003-api-gateway-pattern.md
```

### Review Process

1. Author creates ADR with status "Proposed"
2. Share with relevant stakeholders for review
3. Discuss in architecture review meeting
4. Update ADR based on feedback
5. Get approval from technical lead/architect
6. Update status to "Accepted"
7. Begin implementation

### Maintenance

- Review ADRs quarterly
- Update status if decisions are superseded
- Link related ADRs
- Keep implementation status current

---

**Example ADR:** See `ADR-001-kubernetes-over-docker-compose.md` for a complete example.
