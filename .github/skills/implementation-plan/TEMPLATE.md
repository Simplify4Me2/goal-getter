# Implementation Plan Template
 
This template shows the structure and sections for implementation guides created by the `implementation-plan` skill.
 
## File Naming
 
- New features: `<feature>-implementation-guide.md`
- Approaches/alternatives: `<approach>-implementation-guide.md`  
- Design/architecture: `<component>-design-plan.md`
- Refactoring: `<module>-refactoring-plan.md`
 
**Location**: `docs/plan/` (workspace root)
 
## Frontmatter (Optional)
 
```markdown
# Implementation Plan: [Title]
 
> **Status**: Draft | In Progress | Review | Approved  
> **Author**: [Name]  
> **Created**: [Date]  
> **Target Completion**: [Date]
```
 
## Sections (in order)
 
### 1. Overview
 
2–3 sentences summarizing what is being built/changed and why. Include key benefits or driving reasons.
 
```markdown
## Overview
 
[Feature/change] is [1 sentence description]. This improves [specific benefit] and aligns with [architectural goal/ADR].
 
**Key Benefits:**
- Benefit 1
- Benefit 2
- Benefit 3
```
 
### 2. Architecture Overview
 
Visual representation of the system and how the change integrates.
 
```markdown
## Architecture Overview
 
[Text description, 3-5 sentences]
 
[Mermaid diagram or ASCII art showing components, data flow, and connections]
 
**Key Components:**
- Component A: [responsibility]
- Component B: [responsibility]
```
 
**Include:**
- Boxes/nodes for each layer or service
- Arrows showing data flow
- Labels on arrows indicating what passes between components
- Legend if using specialized symbols
 
### 3. Current State Assessment (if refactoring/upgrade)
 
How the system works today and constraints.
 
```markdown
## Current State
 
[Description of existing implementation]
 
**Current Flow:**
[Diagram or step-by-step]
 
**Constraints:**
- Constraint 1
- Constraint 2
```
 
### 4. Step-by-Step Implementation
 
**Critical rule**: Each step is independent and is implemented/tested in isolation.
 
**Template per step:**
 
```markdown
## Step N: [Action - Clear, Concise Title]
 
**File**: `path/to/file.ts` or multiple files
 
**Intention**:  
[1-2 sentences: what this step accomplishes and why it's necessary]
 
**Benefit**:  
[1-2 sentences: what value this step adds to the overall solution]
 
**Test Specification**:
[Brief explanation of what behavior we're defining]
// Define expected behavior FIRST
 
**Backend (C#)**:
\`\`\`csharp
[Fact]
public async Task MethodName_Scenario_ExpectedOutcome()
{
    // Arrange
    var input = ...;
   
    // Act
    var result = await service.MethodAsync(input);
   
    // Assert
    Assert.Equal(expected, result);
}
\`\`\`
 
**Frontend (TypeScript)**:
\`\`\`typescript
it('should [expected behavior] when [condition]', () => {
    // Arrange
    const input = ...;
   
    // Act
    const result = component.method(input);
   
    // Assert
    expect(result).toEqual(expected);
});
\`\`\`
 
**Implementation**:
 
[Brief explanation: what code satisfies the test]
 
\`\`\`typescript
// New code that makes the test pass
export function method(input: Type): ReturnType {
  // Implementation
}
\`\`\`
 
**Verification**:  
- [ ] Run test for this step (should pass)
- [ ] Manual check: [specific action to verify] → [expected outcome]
 
**Why This Approach**:  
[Design rationale — explain specific choices, alternatives considered, trade-offs]
```
 
**Best practices:**
- Use 6–10 steps for typical features
- Label with action verbs: "Add", "Update", "Refactor", "Extend"
- Include file paths with full workspace-relative paths
- Show before/after code or unified diffs
- Test spec **before** implementation code
- Each test is runnable and documents the contract
- Include happy path + 2–3 edge cases per step
- Explain *why* not just *what*
 
### 5. Code Cleanup Opportunities
 
Scout the files being touched and identify opportunities to improve the overall codebase.
 
**Template:**
 
\`\`\`markdown
## Code Cleanup Opportunities
 
### Files Analyzed
- `path/to/file1.ts` — [reason: e.g., "new service method added", "updated validation logic"]
- `path/to/file2.cs` — [reason]
 
### Duplication & DRY Violations
 
| Location | Issue | Solution | Priority |
|----------|-------|----------|----------|
| `Services/MetricsService.cs:45–52` | Validation logic duplicated in 3 other services | Extract to `ValidationHelper.ValidateMetrics()` | Important |
| `Components/Dashboard.ts:120–130` | Date formatting repeated across 5 components | Create shared utility `formatOcrDate()` in `src/utils/date.ts` | Important |
| `Controllers/OcrController.cs:15–20` | Error response mapping appears in 4 controllers | Create base `BaseApiController` with `CreateErrorResponse()` | Nice-to-Have |
 
### Reusable Abstractions
 
**Proposed Extraction 1: Metric Aggregation Helper**
 
**Current State** (duplicated in 2 files):
\`\`\`csharp
public decimal CalculateAverageConfidence(List<OcrResult> results)
{
    return results.Any() ? results.Average(r => r.Confidence) : 0m;
}
\`\`\`
 
**Proposed Location**: `Core/Helpers/MetricAggregation.cs`
 
**Benefit**: Single source of truth, consistent calculation logic, easier to test
 
---
 
**Proposed Extraction 2: Angular Date/Time Utility**
 
**Current State** (3 components format dates independently):
- `Dashboard.ts` — custom date pipe
- `ReportCard.ts` — moment.js formatting
- `Timeline.ts` — native Date methods
 
**Proposed Location**: `src/utils/date.ts` (export `formatOcrTimestamp()`, `formatDuration()`)
 
**Benefit**: Consistent time display, centralized locale/timezone handling
 
### Dead Code & Cleanup
 
- [ ] `Infrastructure/LegacyProcessRepository.cs` — unused, replaced by `ProcessFileRepository` (safe to remove)
- [ ] `Services/CachedMetricsService.cs:99–115` — unreachable else branch (review logic)
 
### Naming & Consistency
 
- **Issue**: `GetMetricsByDateRange()` vs `FetchMetricsForPeriod()` inconsistent naming across services
- **Action**: Standardize on `Get*` prefix for synchronous calls, `Fetch*` for async calls (align with project convention)
 
### Estimated Effort
 
- Extractions: **2–3 hours** (can be done incrementally or bundled)
- Dead code removal: **30 minutes**
- Naming alignment: **1 hour** (may require search-and-replace + tests)
 
**Recommendation**: Extract metric validation and date utilities **as part of this PR**. Schedule dead code cleanup for a follow-up.
\`\`\`
 
### Scope of Analysis
 
Apply this analysis **only to files being directly modified** in this implementation. Note opportunities for *other* parts of the codebase but defer their cleanup to separate issues/PRs.
 
### 6. Full-Workflow Verification
 
Reference the per-step unit tests from Step 4 and add a final end-to-end verification checklist.
 
## Full-Workflow Verification
 
### Per-Step Unit Tests
Refer back to Steps 1–N where each test spec is defined. All unit tests pass independently before proceeding to integration.
 
### End-to-End Verification Checklist
 
User-facing actions to confirm the feature works as a whole:
- [ ] Action 1 → verify Y happens
- [ ] Action 2 → verify Z appears
- [ ] Edge case → verify graceful handling
 
**How to run all tests for this feature:**
\`\`\`bash
# Backend (C#)
dotnet test --filter "FeatureName"
 
# Frontend (TypeScript)
npm test -- --testNamePattern="FeatureName"
\`\`\`
```
 
### 7. Rollout & Migration
 
Deployment process, performance impact, and rollback.
 
```markdown
## Rollout & Migration
 
### Rollout Checklist
 
- [ ] Step 1: [action] → verify [outcome]
- [ ] Step 2: [action] → verify [outcome]
- [ ] Final verification: [full scenario] → verify [all parts work]
 
### Migration Path
 
[If existing system must be transitioned]
 
1. Deploy backend (backward compatible)
2. Deploy frontend (new signal, old calls still work)
3. Monitor for [X hours/days]
4. Remove legacy code
 
### Rollback Procedure
 
[If needed]
 
1. Revert [service/component]
2. Clear [caches/indexes]
3. Verify [health check]
 
**Estimated rollback time**: [X minutes]
```
 
### 8. FAQ
 
Common questions and answers.
 
```markdown
## FAQ
 
**Q: Why not approach X?**  
A: [Explanation of trade-offs or constraints that ruled it out]
 
**Q: What if [scenario]?**  
A: [How the design handles edge cases]
 
**Q: How do I test this?**  
A: [Quick reference to testing section or specific command]
```
 
## Formatting Conventions
 
| Element | Format |
|---------|--------|
| File paths | `path/to/file.ts` (inline code) |
| Class/function names | `MethodName()` (inline code) |
| Query parameters | `?projectId=MBMC` (inline code) |
| Code blocks | Language-tagged: ` ```typescript`, ` ```csharp`, ` ```bash` |
| Emphasis | **bold** for key terms, *italic* for emphasis |
| Lists | Unordered for variants, ordered for procedures |
| Tables | Use for comparisons, API contracts, performance metrics |
| Diagrams | Mermaid (preferred) or ASCII art |
 
## Example Sections to Include (if applicable)
 
**Backend-focused plans**: Controller → Service → Repository → DTO changes  
**Frontend-focused plans**: Service → Component → Template → Styles changes  
**Full-stack plans**: Architecture diagram showing all layers  
**Migrations**: Current state → proposed state → migration steps  
**Refactoring**: Current code → issues identified → proposed structure → step-by-step changes  
 
## Length Guidelines
 
- **Overview + Architecture**: 500–800 words
- **Each Step**: 250–400 words (test + implementation + rationale)
- **Code Cleanup**: 200–300 words
- **Full-Workflow Verification**: 100–200 words
- **Rollout + FAQ**: 300–600 words
- **Total**: 5–15 KB (full guide)
 
Aim for comprehensive but scannable. Use headings, tables, and code blocks to break up text.
 
## Review Checklist
 
Before marking a plan as "Ready":
 
- [ ] All steps are independent (can be done in any order or combined)
- [ ] Test examples are syntactically correct and runnable
- [ ] Tests cover happy path + 2–3 edge cases per step
- [ ] Intentions and benefits are clearly stated for each step
- [ ] Why This Approach rationale explains design decisions
- [ ] End-to-end verification checklist is step-by-step and verifiable
- [ ] Architecture diagram shows data flow and integration points
- [ ] File paths are accurate and workspace-relative
- [ ] File is in `docs/plan/` and named appropriately
- [ ] No TODO or placeholder comments left in final version
- [ ] Code cleanup opportunities section documents duplication and DRY violations
- [ ] Proposed extractions are realistic and benefit multiple files
- [ ] Dead code or refactoring candidates are clearly marked with priority
- [ ] Estimated effort for cleanup is documented (quick wins vs. larger work)