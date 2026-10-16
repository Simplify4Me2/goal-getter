---
name: implementation-plan
description: 'Create comprehensive implementation plans for new features, refactoring, upgrades, design, architecture, or infrastructure. Use when: planning a feature rollout, designing a system component, refactoring legacy code, upgrading dependencies, or mapping migration paths. Produces step-by-step guides with code examples, testing strategies, rollout checklists, and performance notes.'
argument-hint: 'Describe the feature, refactor, upgrade, or architectural change'
user-invocable: true
---
 
# Implementation Planning
 
Generate detailed, actionable implementation guides for features, refactoring, upgrades, and architectural changes.
 
## When to Use
 
- **New Features**: Map requirements → design → implementation steps → testing & rollout
- **Refactoring**: Assess current state → design approaches → choose best → document migration
- **Upgrades**: Analyze breaking changes → design compatibility layer → plan rollout
- **Architecture**: Design new component → evaluate alternatives → document integration
- **Infrastructure**: Plan migration → assess impact → document runbook
- **Design Decisions**: Create architecture decision records with implementation guidance
 
## Process
 
### 1. Assess Current State
 
Understand the existing system:
- Current implementation and constraints
- Relevant patterns and conventions (ADRs, architecture decisions)
- Dependencies and integration points
- Team skill levels and capacity
- Production vs. local environment considerations
 
**Deliverable**: Context section in the plan
 
### 2. Design Multiple Approaches
 
Evaluate 3–5 distinct solutions:
- Each approach documented with pros/cons
- Evaluated against SOLID principles, best practices, testability
- Aligned with project conventions (ADRs, design patterns)
- Trade-offs clearly articulated (complexity, performance, maintenance, risk)
 
**Deliverable**: Approaches section with clear recommendation
 
### 3. Create Detailed Step-by-Step Guide
 
For the chosen approach:
- **6–10 implementation steps**, each with:
  - **File location** — exact path to modified file
  - **Intention** — what this step accomplishes and why
  - **Benefit** — what value it adds
  - **Code examples** — runnable snippets showing before/after
  - **Test specification** — unit test(s) that define expected behavior
    - Tests serve as the behavioral contract
    - Include happy path + most obvious edge cases
  - **Implementation** — code changes to satisfy the tests
    - Show before/after with context
    - Explain specific choices and trade-offs
  - **Verification** — how to manually verify the step works
  - **Design rationale** — explain specific choices
- Numbered steps can be implemented and tested independently
- Each step fits in a single focused change (avoid monolithic rewrites)
- Each step's test is runnable in isolation
- Tests document expected behavior (better than comments)
- Implementation follows the test specification
- Red → Green → Refactor workflow
 
**Deliverable**: Step-by-step guide with test spec + implementation for each step
 
### 4. Scout Each Touched File for Improvements
 
**"Leave the scene cleaner than you found it"** — while implementing, analyze each file you're modifying:
 
**Analysis focus:**
- **Code duplication**: Does this logic exist elsewhere in the codebase? Can it be extracted to a shared utility?
- **DRY violations**: Are we repeating similar patterns, parameters, or validations?
- **Reusable abstractions**: Can helper functions, extension methods, or base classes be extracted?
- **Dead code**: Are there unused methods, properties, or branches in files being touched?
- **Naming inconsistencies**: Does this file use naming patterns that conflict with the rest of the codebase?
- **Test coverage gaps**: Are there untested code paths in the modified files?
 
**Deliverable**: "Code Cleanup Opportunities" section documenting:
- Files analyzed and why
- Duplication instances with line references
- Proposed extractions (utility functions, base classes, constants)
- Quick wins vs. larger refactoring candidates
- Priority (Critical / Important / Nice-to-Have)
 
This keeps technical debt from accumulating and compounds quality improvements over time.
 
### 5. Full-Workflow Verification
 
- **Reference per-step unit tests** from Step 3 (tests already in place)
- **Verification checklist** — user-facing actions to confirm correctness across the feature
- **Test frameworks and patterns** aligned with the project
 
**Deliverable**: Full-workflow verification checklist
 
### 6. Provide Rollout & Migration Path
 
- **Rollout checklist** — step-by-step deployment verification
- **Performance impact** — latency, throughput, resource changes
- **Migration path** — how to deploy without downtime or breaking changes
- **Rollback procedures** (if applicable) — how to revert if needed
- **FAQ** — common concerns and answers
 
**Deliverable**: Deployment section + FAQ
 
### 7. Document Architecture & Data Flow
 
- **Architecture diagram** (ASCII art or mermaid) showing new/changed components
- **Data flow diagram** — where data enters, how it flows, where it exits
- **Dependencies** — new external services, APIs, or internal modules
- **Integration points** — how this connects to existing systems
 
**Deliverable**: Architecture & Data Flow sections with diagrams
 
## Output Format
 
Plans are saved to `docs/plan/` as markdown files:
- **File naming**: `<feature>-implementation-guide.md` or `<feature>-design-plan.md`
- **Sections**: Overview → Architecture → Step 1...N → Code Cleanup → Full-Workflow Verification → Rollout → FAQ
- **Length**: 5–15 KB (full guide, not summary)
- **Code blocks**: Language-tagged for syntax highlighting (TypeScript, C#, etc.)
 
See [TEMPLATE.md](./references/TEMPLATE.md) for structure and formatting conventions.
 
## Tips for Best Results
 
1. **Provide context upfront**: Mention existing files, ADRs, constraints
2. **Be specific about "why"**: Not just "what to change" but "why we're changing it"
3. **Call out trade-offs**: No perfect solution exists; document the compromises
4. **Include team feedback**: If constraints come from code review, security, or performance reviews, mention them
5. **Write tests first**: Each step's test spec defines the contract before implementation code
6. **Scout as you document**: While writing each implementation step, note patterns or duplications in the files being touched. This catches opportunities early and justifies technical debt paydown.
 
## Examples
 
- *"Create an implementation plan for adding project-scoped filter options to the dashboard UI"*
- *"Design and plan the migration from Azure Table Storage to Cosmos DB"*
- *"Create a refactoring plan for moving from class components to functional Angular components"*
- *"Plan the upgrade from Angular 19 to Angular 21"*
- *"Design and plan a cache layer for the OCR metrics API"*
 
## See Also
 
- [TEMPLATE.md](./references/TEMPLATE.md) — structure and formatting guide
- Project ADRs in `docs/ADRs/` — design decisions that shape plans
- Existing plans in `docs/plan/` — examples and precedent