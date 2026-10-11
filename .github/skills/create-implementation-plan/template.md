# Implementation Plan Template

Use this template to write a single implementation plan. Replace every bracketed placeholder, keep the section order and headers exactly as written, and follow the rules in `SKILL.md`.

## Template
```md
---
goal: [Concise Title Describing the Implementation Plan's Goal]
version: [Optional: e.g., 1.0, Date]
date_created: [YYYY-MM-DD]
last_updated: [Optional: YYYY-MM-DD]
owner: [Optional: Team/Individual responsible for this spec]
status: 'Completed'|'In progress'|'Planned'|'Deprecated'|'On Hold'
tags: [Optional: List of relevant tags or categories, e.g., `feature`, `upgrade`, `refactor`, `architecture`, `migration`, `bug` etc]
---

# Introduction

![Status: <status>](https://img.shields.io/badge/status-<status>-<status_color>)

**Summary for reviewers**: [Up to three plain-language sentences: what changes, why it matters, and how success is recognized.]

[A short concise introduction to the plan and the goal it is intended to achieve.]

## 1. Requirements & Constraints

[Explicitly list all requirements & constraints that affect the plan and constrain how it is implemented. Use bullet points or tables for clarity.]

- **REQ-001**: Requirement 1
- **SEC-001**: Security Requirement 1
- **[3 LETTERS]-001**: Other Requirement 1
- **CON-001**: Constraint 1
- **GUD-001**: Guideline 1
- **PAT-001**: Pattern to follow 1

## 2. Implementation Steps

[Each phase is one independently deliverable increment. Within a phase the order is fixed: code, then tests, then boy scout items.]

### Implementation Phase 1 - [Short Phase Name]

- **GOAL-001**: [Describe the goal of this phase, e.g., "Expose live match events through the ApiService"]
- **Completion criteria**: [Measurable criteria, e.g., "`dotnet test GoalGetter/GoalGetter.Tests` exits with 0 failures"]

| Task | Description | Files | Completed | Date |
|------|-------------|-------|-----------|------|
| TASK-001 | [What to implement, and why] | FILE-001 | | |
| TASK-002 | [What to implement, and why] | FILE-002, FILE-003 | | |

#### TASK-001 Code

Source: [Microsoft Learn URL]

```csharp
// Snippet from Microsoft Learn, adapted only to project naming and conventions
```

#### TASK-001 Tests

- **TEST-001**: [Test name] in [repository-relative test file path]. Asserts [behavior]. Expected: [result].

#### TASK-001 Boy Scout

- **BSC-001**: [File path] | [DRY / SOLID / Code smell / Design pattern] | [Exact smell or principle] | [Proposed behavior-preserving change]

#### TASK-002 Code

[Repeat the Code, Tests, and Boy Scout structure for TASK-002.]

### Implementation Phase 2 - [Short Phase Name]

- **GOAL-002**: [Describe the goal of this phase]
- **Completion criteria**: [Measurable criteria]

[Repeat the full phase structure for each phase.]

## 3. Boy Scout Summary

[Index of every `BSC-NNN` item declared in Section 2. Reference only; do not declare new items here.]

| Item | File | Category | Phase / Task | Status |
|------|------|----------|--------------|--------|
| BSC-001 | FILE-001 | DRY | Phase 1 / TASK-001 | |

## 4. Manual End-to-End Test

[Step-by-step manual validation of the full implemented flow, from the user's entry point to the final visible or stored result. Run only after all phases are complete.]

- **Prerequisites**: [Tools, accounts, and data needed before step 1]
- **Start**: Run `aspire start` from `GoalGetter/`, then open the Aspire dashboard and confirm all resources are Running
- **E2E-001**: Precondition: [state]. Action: [exact URL, page, endpoint, or command]. Expected: [observable result].
- **E2E-002**: Negative path. Precondition: [state]. Action: [invalid input or stopped dependency]. Expected: [error behavior and message].
- **Stop**: Run `aspire stop` when testing is complete

## 5. Alternatives

[A bullet point list of any alternative approaches that were considered and why they were not chosen. This helps to provide context and rationale for the chosen approach.]

- **ALT-001**: Alternative approach 1
- **ALT-002**: Alternative approach 2

## 6. Dependencies

[List any dependencies that need to be addressed, such as libraries, frameworks, or other components that the plan relies on.]

- **DEP-001**: Dependency 1
- **DEP-002**: Dependency 2

## 7. Files

[List every file the plan touches. Use repository-relative paths. Action is one of `CREATE`, `MODIFY`, or `DELETE`.]

- **FILE-001**: `GoalGetter/GoalGetter.ApiService/Program.cs` | MODIFY | [Classes and methods changed] | [Phase / TASK]
- **FILE-002**: `GoalGetter/GoalGetter.Tests/[TestFile].cs` | CREATE | [Classes and methods added] | [Phase / TASK]

## 8. Testing Strategy

[Summarize the test types and commands. Individual tests are declared under each task in Section 2.]

| Test Type | Project | Framework | Command |
|-----------|---------|-----------|---------|
| Unit | `GoalGetter/GoalGetter.Tests` | [Framework] | `dotnet test GoalGetter/GoalGetter.Tests` |
| Integration | `GoalGetter/GoalGetter.Tests` | [Framework] | [Command] |

## 9. Risks & Assumptions

[List any risks or assumptions. Boy scout findings in code the tasks do not change are recorded here.]

- **RISK-001**: Risk 1
- **ASSUMPTION-001**: Assumption 1

## 10. Related Specifications / Further Reading

[Link to related spec 1]
[Link to relevant external documentation]
[Microsoft Learn URLs used for code snippets]
```

## Notes
- Declare each identifier once. Reference it elsewhere by the same ID. Run the uniqueness check in `SKILL.md` before finalizing.
- Order inside every phase is fixed: `TASK-NNN Code`, then `TASK-NNN Tests`, then `TASK-NNN Boy Scout`.
- Every snippet under a `Code` heading that uses a Microsoft, .NET, ASP.NET Core, Aspire, or Azure API needs a `Source:` line with a Microsoft Learn URL.
- Tasks that use no Microsoft API still keep the `Code` heading. Write `Source: Not applicable, no Microsoft API used` and show the project code.
- Use `No boy scout findings` under `Boy Scout` when a task has no findings.
- Replace all placeholders. Remove any bracketed text before the plan is saved to `/plan/`.
