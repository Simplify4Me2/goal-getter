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
- Every task that uses a Microsoft, .NET, ASP.NET Core, Aspire, or Azure API must place a code snippet under its `Code` heading, sourced from the Microsoft Learn MCP server (`microsoftdocs/mcp`):
  1. Run `microsoft_docs_search` with the API or feature name to find the authoritative page
  2. Run `microsoft_code_sample_search` with `language` set to `csharp` to find a matching sample
  3. Run `microsoft_docs_fetch` on the page when the snippet needs prerequisites, version notes, or full context
  4. Adapt only names and paths to project conventions, and cite the source URL on a `Source:` line directly below the snippet
- Snippets target .NET 10, ASP.NET Core, and .NET Aspire, matching `GoalGetter/GoalGetter.sln`
- Snippets contain no credentials; reference configuration or user secrets instead
- Snippets use the minimum lines needed to show the API usage
- Each phase completion criteria must name a build or test command that proves the phase

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
- Change only the code that the task changes; record findings in other code of a touched file as `RISK-NNN` or `ASSUMPTION-NNN` in Section 9
- Do not propose a design pattern unless a concrete smell in the touched code requires it
- If no finding applies to a task, write `No boy scout findings` under that task

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

- Save implementation plan files in `/docs/plan/` directory
- Use naming convention: `[purpose]-[component]-[version].md`
- Purpose prefixes: `upgrade|refactor|feature|data|infrastructure|process|architecture|design`
- Example: `upgrade-system-command-4.md`, `feature-auth-module-1.md`
- File must be valid Markdown with proper front matter structure

## Mandatory Template Structure

All implementation plans must strictly adhere to the structure in `template.md`. Each section is required and must be populated with specific, actionable content. Section order and section headers are fixed; do not add, remove, or rename sections. AI agents must validate template compliance before execution. Use `template.md` for the full fill-in structure, including front matter and the per-task Code, Tests, and Boy Scout subsections.

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

The front matter and introduction must reflect the current state of the plan. Use `template.md` for the exact front matter fields and the status badge markup.

## Examples

See `examples/sample.md` for a complete feature plan and `examples/sample-refactor.md` for a complete refactoring plan. Both follow `template.md` and cite Microsoft Learn snippets.