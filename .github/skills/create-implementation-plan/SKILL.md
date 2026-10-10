---
name: create-implementation-plan
description: 'Create a new implementation plan file for new features, refactoring existing code or upgrading packages, design, architecture or infrastructure.'
---

# Create Implementation Plan

## Primary Directive

Your goal is to create a new implementation plan file for `${input:PlanPurpose}`. Your output must be machine-readable, deterministic, and structured for autonomous execution by other AI systems or humans.

## Execution Context

This prompt produces a single plan file that must be readable by both humans and AI agents. Pair plain-language sentences with machine-parseable structures (tables, bullets, identifiers). Interpret instructions literally and execute them systematically; ask for clarification only when a required input is missing.

## Core Requirements

- Generate implementation plans that are fully executable by AI agents or humans
- Use plain, direct sentences; define each project-specific term on first use
- Use deterministic language with zero ambiguity
- Structure all content for automated parsing and execution
- Ensure complete self-containment with no external dependencies for understanding

## Plan Structure Requirements

Plans must consist of discrete, atomic phases containing executable tasks. Each phase must be independently processable by AI agents or humans without cross-phase dependencies unless explicitly declared.

Each phase contains, in this fixed order: the implementation code, the tests that prove it, and the boy scout items for the code it touches. Code and tests are never split into separate phases. A phase is complete only when its tests pass.

## Phase Architecture

- Each phase must have measurable completion criteria
- Tasks within phases must be executable in parallel unless dependencies are specified
- All task descriptions must include specific file paths, function names, and exact implementation details
- No task should require human interpretation or decision-making

## Good Boy Scout Principle

Leave every touched file cleaner than it was found, limited to the code that the plan's tasks already change.

For each task, review the touched code for the following:

| Category | Look for |
|----------|----------|
| DRY | Duplicated logic, copy-pasted blocks, repeated literals that should be a named constant or shared method |
| SOLID | Single Responsibility breaches, Open/Closed violations (type switches edited for every new case), dependencies created with `new` instead of injected (Dependency Inversion), interfaces with unused members (Interface Segregation), subclasses that break base contracts (Liskov) |
| Code smells | Long methods, god classes, primitive obsession, feature envy, deep nesting, dead code, misleading names, magic numbers |
| Design patterns | Only when a concrete smell justifies it, e.g., Strategy for interchangeable rules, Factory for type selection, Options pattern for configuration, Repository for data access |

Rules:
- Each finding becomes a `BSC-NNN` item under its task with: file path, category, the exact smell or principle, and the proposed change
- Boy scout changes must preserve observable behavior and must be covered by a `TEST-NNN` item (existing or new)
- Never refactor code outside the touched files; record such findings as `RISK-NNN` or `ASSUMPTION-NNN` in Section 9
- Do not propose a design pattern unless a concrete smell in the touched code requires it
- If no finding applies to a task, write `No boy scout findings` under that task

## Code Snippet Sourcing

Every task that uses a Microsoft, .NET, ASP.NET Core, Aspire, or Azure API must include a code snippet from the Microsoft Learn MCP server (`microsoftdocs/mcp`):

1. Run `microsoft_docs_search` with the API or feature name to find the authoritative page
2. Run `microsoft_code_sample_search` with `language` set to `csharp` to find a matching sample
3. Run `microsoft_docs_fetch` on the page when the snippet needs prerequisites, version notes, or full context
4. Place the snippet under the task, adapt only names and paths to project conventions, and cite the source URL directly below it

Snippet rules:
- Target .NET 10, ASP.NET Core, and .NET Aspire, matching the solution in `GoalGetter/GoalGetter.sln`
- Contain no credentials; reference configuration or user secrets instead
- Keep to the minimum lines needed to show the API usage

## Manual End-to-End Testing

Each plan must end with a manual test flow that exercises the complete implemented path, from the user's entry point to the final visible or stored result.

- Declare each step as `E2E-NNN` in Section 4 with a precondition, an exact action (URL, page, endpoint, or command), and an observable expected result
- Include one negative path step with invalid input or a stopped dependency, and state the expected error behavior
- Start the AppHost with `aspire start` from `GoalGetter/` and stop it with `aspire stop` when testing completes, as required by `.github/copilot-instructions.md`
- Use the Aspire dashboard to confirm resource health and telemetry for every resource the flow touches
- Reference `GoalGetter/GoalGetter.ApiService/GoalGetter.ApiService.http` for API calls where applicable

## AI-Optimized Implementation Standards

- Use explicit, unambiguous language with zero interpretation required
- Structure all content as machine-parseable formats (tables, lists, structured data)
- Include specific file paths, line numbers, and exact code references where applicable
- Define all variables, constants, and configuration values explicitly
- Provide complete context within each task description
- Use standardized prefixes for all identifiers (REQ-, TASK-, TEST-, BSC-, E2E-, etc.)
- Include validation criteria that can be automatically verified

## Output File Specifications

- Save implementation plan files in `/plan/` directory
- Use naming convention: `[purpose]-[component]-[version].md`
- Purpose prefixes: `upgrade|refactor|feature|data|infrastructure|process|architecture|design`
- Example: `upgrade-system-command-4.md`, `feature-auth-module-1.md`
- File must be valid Markdown with proper front matter structure

## Mandatory Template Structure

All implementation plans must strictly adhere to the following template. Each section is required and must be populated with specific, actionable content. Section order and section headers are fixed; do not add, remove, or rename sections. AI agents must validate template compliance before execution.

## Template Validation Rules

- All front matter fields must be present and properly formatted
- All section headers must match exactly (case-sensitive)
- All identifier prefixes must follow the specified format
- Tables must include all required columns
- No placeholder text may remain in the final output
- Every phase must contain code, tests, and boy scout items (or `No boy scout findings`), in that order
- Every task that uses a Microsoft, .NET, Azure, or Aspire API must cite a Microsoft Learn source URL directly below its code snippet
- Every task must reference `FILE-NNN` items; every `FILE-NNN` must be a repository-relative path with an action of `CREATE`, `MODIFY`, or `DELETE`
- Section 4 must contain at least one positive `E2E-NNN` step and one negative-path `E2E-NNN` step
- **Identifiers must be uniquely declared.** Every identifier (`REQ-NNN`, `SEC-NNN`, `CON-NNN`, `GUD-NNN`, `PAT-NNN`, `GOAL-NNN`, `TASK-NNN`, `ALT-NNN`, `DEP-NNN`, `FILE-NNN`, `TEST-NNN`, `RISK-NNN`, `ASSUMPTION-NNN`, `BSC-NNN`, `E2E-NNN`) must be **declared exactly once**. A declaration is where the identifier introduces a row: the leading cell in a TASK table row, or the bolded prefix in a bullet line like `- **REQ-001**: ...` (`GOAL-NNN` is always declared as a bolded bullet). The same identifier may then appear any number of times as a **reference** elsewhere in the plan (a `TASK` body citing a `REQ`, one `TASK` citing another `TASK`, the Dependencies section pointing at a `DEP` already declared upstream, etc.). References are expected and not collisions.

## Identifier Uniqueness Check

Run these checks before finalizing the plan. Checks (1) and (2) target declarations and must return zero rows. Check (3) is a broad informational scan: it will surface valid references too, so use it for awareness rather than as a gate.

```bash
# Set PLAN_FILE to the plan being validated.
PLAN_FILE="/plan/<purpose>-<component>-<version>.md"

# 1) Duplicate TASK / GOAL declarations in table rows.
grep -oE '\| (TASK|GOAL)-[0-9]+ \|' "$PLAN_FILE" \
  | sed -E 's/.*((TASK|GOAL)-[0-9]+).*/\1/' \
  | sort | uniq -d

# 2) Duplicate declaration IDs in bullet-style spec lines.
grep -oE '^- \*\*(REQ|SEC|CON|GUD|RISK|ASSUMPTION|TASK|GOAL|FILE|TEST|PAT|ALT|DEP|BSC|E2E)-[0-9]+\*\*:' "$PLAN_FILE" \
  | sed -E 's/^- \*\*([A-Z0-9]+-[0-9]+)\*\*:.*/\1/' \
  | sort | uniq -d

# 3) Broad duplicate scan (diagnostic only; may include valid references).
grep -oE '(REQ|SEC|CON|GUD|RISK|ASSUMPTION|TASK|GOAL|FILE|TEST|PAT|ALT|DEP|BSC|E2E)-[0-9]+' "$PLAN_FILE" \
  | sort | uniq -d
```

Prerequisites: a POSIX-compatible shell (`sh` / `bash`) with `grep`, `sed`, `sort`, and `uniq`. On Windows without these tools, use equivalent platform-native commands and preserve the same declaration-vs-reference logic.

If check (1) or (2) returns any row, re-number the duplicate so each identifier is declared exactly once, then re-run the checks until both are empty.

## Status

The status of the implementation plan must be clearly defined in the front matter and must reflect the current state of the plan. The status can be one of the following (status_color in brackets): `Completed` (bright green badge), `In progress` (yellow badge), `Planned` (blue badge), `Deprecated` (red badge), or `On Hold` (orange badge). It should also be displayed as a badge in the introduction section.

```md
---
goal: [Concise Title Describing the Package Implementation Plan's Goal]
version: [Optional: e.g., 1.0, Date]
date_created: [YYYY-MM-DD]
last_updated: [Optional: YYYY-MM-DD]
owner: [Optional: Team/Individual responsible for this spec]
status: 'Completed'|'In progress'|'Planned'|'Deprecated'|'On Hold'
tags: [Optional: List of relevant tags or categories, e.g., `feature`, `upgrade`, `chore`, `architecture`, `migration`, `bug` etc]
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

[List any risks or assumptions. Boy scout findings outside the touched files are recorded here.]

- **RISK-001**: Risk 1
- **ASSUMPTION-001**: Assumption 1

## 10. Related Specifications / Further Reading

[Link to related spec 1]
[Link to relevant external documentation]
[Microsoft Learn URLs used for code snippets]
```