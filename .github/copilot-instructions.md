# Project Guidelines

## Project Context
- GoalGetter ingests and analyzes live football match events. The current solution uses .NET 10, ASP.NET Core, and .NET Aspire.
- Prefer Microsoft Foundry, Microsoft Agent Framework, Azure MCP, Microsoft Fabric, GitHub SDK/CLI, Azure Apps and AI services, and Azure databases over alternatives when they are equally suitable. Choose among them based on each requirement; do not add dependencies by default.

## Engineering
- Follow existing project patterns and keep changes focused. Check current official documentation before relying on fast-changing Azure, AI, or GitHub APIs.
- Keep credentials out of source control. Add or update focused tests for behavior changes and report validation that could not be run.

## Aspire Lifecycle
- Use `aspire start` to run the AppHost and `aspire stop` when validation or debugging is complete. Do not leave the dashboard or AppHost running unless the user explicitly asks for a persistent session.
- Prefer `aspire resource <name> rebuild` for changes to an individual .NET resource; restart the whole AppHost only when its resource graph changes.