---
goal: Expose live match events through the GoalGetter ApiService
version: 1.0
date_created: 2026-05-01
last_updated: 2026-05-01
owner: GoalGetter API team
status: 'Planned'
tags: [feature, api, aspire, testing]
---

# Introduction

![Status: Planned](https://img.shields.io/badge/status-Planned-blue)

**Summary for reviewers**: Adds `GET /matches/{matchId}/events` to `GoalGetter.ApiService`, returning the events of one match in minute order. Success is recognized when the xUnit tests in Phase 2 pass and the manual flow in Section 4 returns the ordered events for match 1 and HTTP 400 for match 0.

This plan adds the read endpoint in two phases. Phase 1 registers an event source in dependency injection so the data can later come from a live feed. Phase 2 adds the endpoint with input validation and integration tests that run through the Aspire AppHost.

## 1. Requirements & Constraints

- **REQ-001**: Return the events for a match id in ascending `Minute` order
- **REQ-002**: Return HTTP 400 Bad Request when `matchId` is zero or negative
- **REQ-003**: Read events through `IMatchEventSource` so a live feed can replace the in-memory source without changing the endpoint
- **SEC-001**: The endpoint is read-only and exposes no connection strings, keys, or internal exception text
- **CON-001**: Target `net10.0` and ASP.NET Core minimal APIs. Add no new NuGet packages
- **GUD-001**: Map new endpoints from an extension method in their own file, not in top-level `Program.cs` code
- **PAT-001**: Register services in the ASP.NET Core dependency injection container and inject them into endpoint parameters

## 2. Implementation Steps

### Implementation Phase 1 - Event Source and Registration

- **GOAL-001**: Provide a `MatchEvent` model and an `IMatchEventSource` with an in-memory implementation, registered in DI
- **Completion criteria**: `dotnet build GoalGetter/GoalGetter.sln` exits with 0 errors and 0 warnings from the touched projects

| Task | Description | Files | Completed | Date |
|------|-------------|-------|-----------|------|
| TASK-001 | Create `MatchEvent`, `IMatchEventSource`, and `InMemoryMatchEventSource` with seed data for match 1. Register the source as a singleton in `Program.cs` | FILE-001, FILE-002, FILE-003 | | |

#### TASK-001 Code

Source: https://learn.microsoft.com/aspnet/core/mvc/controllers/dependency-injection?view=aspnetcore-10.0#constructor-injection

```csharp
// GoalGetter/GoalGetter.ApiService/Program.cs
builder.Services.AddSingleton<IMatchEventSource, InMemoryMatchEventSource>();
```

```csharp
// GoalGetter/GoalGetter.ApiService/Models/MatchEvent.cs
namespace GoalGetter.ApiService.Models;

public record MatchEvent(int MatchId, int Minute, string Type, string Player);
```

```csharp
// GoalGetter/GoalGetter.ApiService/Services/MatchEventSource.cs
public interface IMatchEventSource
{
    IReadOnlyList<MatchEvent> GetEvents(int matchId);
}
```

`InMemoryMatchEventSource` returns the seed list for match 1 ordered by `Minute` and an empty list for any other id. The seed list is defined in TASK-001 Boy Scout item BSC-001.

#### TASK-001 Tests

- **TEST-001**: Build check. `dotnet build GoalGetter/GoalGetter.ApiService/GoalGetter.ApiService.csproj` exits with 0 errors. Confirms the new types compile and the DI registration resolves to a type that implements `IMatchEventSource`.

#### TASK-001 Boy Scout

- **BSC-001**: `GoalGetter/GoalGetter.ApiService/Services/MatchEventSource.cs` | DRY | Seed data repeats the `new MatchEvent(1, ...)` constructor call for every event | Add a private static helper `Seed(int matchId, params (int Minute, string Type, string Player)[] events)` and build the seed list with one call per match

### Implementation Phase 2 - Endpoint with Validation

- **GOAL-002**: Expose `GET /matches/{matchId:int}/events` with validation, and cover it with integration tests
- **Completion criteria**: `dotnet test GoalGetter/GoalGetter.Tests --filter "FullyQualifiedName~MatchEventsTests"` exits with 0 failures and 2 passed tests

| Task | Description | Files | Completed | Date |
|------|-------------|-------|-----------|------|
| TASK-002 | Create `MapMatchEndpoints` with the validated route, call it from `Program.cs`, and add integration tests for the success and validation paths | FILE-003, FILE-004, FILE-005 | | |

#### TASK-002 Code

Source: https://learn.microsoft.com/aspnet/core/fundamentals/error-handling-api?view=aspnetcore-10.0#problem-details

```csharp
// GoalGetter/GoalGetter.ApiService/Endpoints/MatchEndpoints.cs
public static class MatchEndpoints
{
    public static IEndpointRouteBuilder MapMatchEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/matches/{matchId:int}/events", (int matchId, IMatchEventSource source) =>
            matchId <= 0
                ? Results.BadRequest()
                : Results.Ok(source.GetEvents(matchId)));

        return app;
    }
}
```

```csharp
// GoalGetter/GoalGetter.ApiService/Program.cs, after the existing MapGet calls
app.MapMatchEndpoints();
```

The snippet is adapted from the source sample's `/users/{id:int}` route. The route name, parameter name, and response type are project-specific.

#### TASK-002 Tests

- **TEST-002**: `Get_MatchEvents_ForMatch1_ReturnsOrderedEvents` in `GoalGetter/GoalGetter.Tests/MatchEventsTests.cs`. Starts the AppHost, calls `GET /matches/1/events` through `app.CreateHttpClient("apiservice")`, and asserts HTTP 200 and minutes `12, 33, 58, 81` in that order.
- **TEST-003**: `Get_MatchEvents_ZeroId_ReturnsBadRequest` in `GoalGetter/GoalGetter.Tests/MatchEventsTests.cs`. Calls `GET /matches/0/events` and asserts HTTP 400 Bad Request.

#### TASK-002 Boy Scout

No boy scout findings

## 3. Boy Scout Summary

| Item | File | Category | Phase / Task | Status |
|------|------|----------|--------------|--------|
| BSC-001 | FILE-001 | DRY | Phase 1 / TASK-001 | Open |

## 4. Manual End-to-End Test

- **Prerequisites**: .NET 10 SDK, Aspire CLI, and a local clone of the repository. No credentials are needed
- **Start**: Run `aspire start` from `GoalGetter/`, then open the Aspire dashboard and confirm `apiservice` and `webfrontend` are Running
- **E2E-001**: Precondition: `apiservice` is Running. Action: `GET {apiservice-url}/matches/1/events` (use the endpoint URL shown for `apiservice` in the dashboard; `GoalGetter/GoalGetter.ApiService/GoalGetter.ApiService.http` uses `http://localhost:5485`). Expected: HTTP 200, a JSON array of 4 events with `minute` values 12, 33, 58, 81 in that order. Dashboard shows the request with status 200 in the `apiservice` traces.
- **E2E-002**: Negative path. Precondition: `apiservice` is Running. Action: `GET {apiservice-url}/matches/0/events`. Expected: HTTP 400 Bad Request, and the response body contains no stack trace or internal exception text.
- **E2E-003**: Precondition: `apiservice` is Running. Action: `GET {apiservice-url}/matches/2/events`. Expected: HTTP 200 with an empty JSON array, because match 2 has no seeded events.
- **Stop**: Run `aspire stop` when testing is complete

## 5. Alternatives

- **ALT-001**: Read events from a database such as Azure Cosmos DB or Azure SQL. Rejected for this phase because it adds a dependency, provisioning, and secrets before the contract is proven
- **ALT-002**: Put the endpoint directly in `Program.cs`. Rejected because it grows the top-level file and conflicts with GUD-001

## 6. Dependencies

- **DEP-001**: Aspire AppHost resource named `apiservice` in `GoalGetter/GoalGetter.AppHost/AppHost.cs`. The integration tests use this name
- **DEP-002**: Existing test packages in `GoalGetter/GoalGetter.Tests/GoalGetter.Tests.csproj` (`Aspire.Hosting.Testing` and `xunit.v3.mtp-off`). No new packages

## 7. Files

- **FILE-001**: `GoalGetter/GoalGetter.ApiService/Services/MatchEventSource.cs` | CREATE | `IMatchEventSource`, `InMemoryMatchEventSource` | Phase 1 / TASK-001
- **FILE-002**: `GoalGetter/GoalGetter.ApiService/Models/MatchEvent.cs` | CREATE | `MatchEvent` record | Phase 1 / TASK-001
- **FILE-003**: `GoalGetter/GoalGetter.ApiService/Program.cs` | MODIFY | DI registration of `IMatchEventSource`; call to `MapMatchEndpoints` | Phase 1 / TASK-001 and Phase 2 / TASK-002
- **FILE-004**: `GoalGetter/GoalGetter.ApiService/Endpoints/MatchEndpoints.cs` | CREATE | `MatchEndpoints.MapMatchEndpoints` | Phase 2 / TASK-002
- **FILE-005**: `GoalGetter/GoalGetter.Tests/MatchEventsTests.cs` | CREATE | `MatchEventsTests` with TEST-002 and TEST-003 | Phase 2 / TASK-002

## 8. Testing Strategy

| Test Type | Project | Framework | Command |
|-----------|---------|-----------|---------|
| Build | `GoalGetter/GoalGetter.ApiService` | .NET SDK | `dotnet build GoalGetter/GoalGetter.ApiService/GoalGetter.ApiService.csproj` |
| Integration | `GoalGetter/GoalGetter.Tests` | xUnit v3 with Aspire testing | `dotnet test GoalGetter/GoalGetter.Tests --filter "FullyQualifiedName~MatchEventsTests"` |
| Regression | `GoalGetter/GoalGetter.Tests` | xUnit v3 with Aspire testing | `dotnet test GoalGetter/GoalGetter.Tests` (confirms `WebTests` still passes) |

## 9. Risks & Assumptions

- **RISK-001**: `Program.cs` also holds the weather sample endpoint, which uses `Random.Shared` inline and a magic number in `TemperatureF`. This plan does not change that code, so the findings are recorded here and handled by `examples/sample-refactor.md`
- **ASSUMPTION-001**: The seeded events for match 1 are sample data. A live feed replaces them in a later plan

## 10. Related Specifications / Further Reading

- [Minimal API error handling and ProblemDetails](https://learn.microsoft.com/aspnet/core/fundamentals/error-handling-api?view=aspnetcore-10.0#problem-details)
- [Dependency injection in controllers and constructor injection](https://learn.microsoft.com/aspnet/core/mvc/controllers/dependency-injection?view=aspnetcore-10.0#constructor-injection)
- [Aspire integration testing with DistributedApplicationTestingBuilder](https://learn.microsoft.com/dotnet/aspire/testing/overview)
