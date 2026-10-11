---
goal: Refactor the weather sample endpoints out of Program.cs and remove hard-coded dependencies
version: 1.0
date_created: 2026-05-01
last_updated: 2026-05-01
owner: GoalGetter API team
status: 'Planned'
tags: [refactor, api, solid, testing]
---

# Introduction

![Status: Planned](https://img.shields.io/badge/status-Planned-blue)

**Summary for reviewers**: Moves the weather sample endpoints out of `GoalGetter/GoalGetter.ApiService/Program.cs`, injects the random source instead of using `Random.Shared` inline, and names the Celsius-to-Fahrenheit divisor. Observable API responses do not change. Success is recognized when the new tests pass and the manual flow in Section 4 returns the same routes and status codes as before.

This refactoring runs in two phases. Phase 1 introduces an injected forecast generator. Phase 2 extracts the endpoints and the record into their own files. Each phase builds and tests on its own.

## 1. Requirements & Constraints

- **REQ-001**: `GET /` and `GET /weatherforecast` keep their current routes, status codes, and JSON shape
- **REQ-002**: `GET /weatherforecast` returns five forecast items
- **CON-001**: No change to the public response contract. The `TemperatureF` values must match the current arithmetic exactly
- **CON-002**: Target `net10.0`. Add no new NuGet packages
- **GUD-001**: Keep `Program.cs` limited to host setup, service registration, and endpoint mapping calls
- **PAT-001**: Use constructor injection for behavior that is hard to test, such as randomness
- **SEC-001**: No change to exception handling or to what the endpoints expose

## 2. Implementation Steps

### Implementation Phase 1 - Inject Forecast Generation

- **GOAL-001**: Move random forecast generation behind an `IForecastGenerator` interface, registered in DI, so tests can control it
- **Completion criteria**: `dotnet test GoalGetter/GoalGetter.Tests --filter "FullyQualifiedName~WeatherEndpointTests"` exits with 0 failures, and `dotnet build GoalGetter/GoalGetter.sln` exits with 0 errors

| Task | Description | Files | Completed | Date |
|------|-------------|-------|-----------|------|
| TASK-001 | Create `IForecastGenerator` and `RandomForecastGenerator` in one file, register the generator in DI, and replace the inline `Random.Shared` call in `Program.cs` | FILE-001, FILE-002 | | |

#### TASK-001 Code

Source: https://learn.microsoft.com/aspnet/core/mvc/controllers/dependency-injection?view=aspnetcore-10.0#constructor-injection

```csharp
// GoalGetter/GoalGetter.ApiService/Program.cs
builder.Services.AddSingleton<IForecastGenerator, RandomForecastGenerator>();
```

```csharp
// GoalGetter/GoalGetter.ApiService/Services/ForecastGenerator.cs
public interface IForecastGenerator
{
    WeatherForecast[] Generate(int days);
}

public sealed class RandomForecastGenerator : IForecastGenerator
{
    private static readonly string[] Summaries = ["Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"];

    private readonly Random _random;

    public RandomForecastGenerator(Random random) => _random = random;

    public WeatherForecast[] Generate(int days) =>
        Enumerable.Range(1, days).Select(index => new WeatherForecast(
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            _random.Next(MinTemperatureC, MaxTemperatureC),
            Summaries[_random.Next(Summaries.Length)])).ToArray();
}
```

The `Random` dependency is registered in `Program.cs` as `builder.Services.AddSingleton(Random.Shared);` so production behavior is unchanged. The generator uses it through its constructor.

#### TASK-001 Tests

- **TEST-001**: `Get_WeatherForecast_ReturnsFiveItems` in `GoalGetter/GoalGetter.Tests/WeatherEndpointTests.cs`. Starts the AppHost, calls `GET /weatherforecast` through `app.CreateHttpClient("apiservice")`, and asserts HTTP 200 and five items in the JSON array.

#### TASK-001 Boy Scout

- **BSC-001**: `GoalGetter/GoalGetter.ApiService/Services/ForecastGenerator.cs` | Code smell (magic numbers) | Temperature bounds `-20` and `55` are inline literals | Name them `private const int MinTemperatureC = -20;` and `private const int MaxTemperatureC = 55;`. Keep the exclusive upper bound so output stays the same
- **BSC-002**: `GoalGetter/GoalGetter.ApiService/Services/ForecastGenerator.cs` | Code smell (misplaced data) | The `summaries` array is a local inside top-level code in `Program.cs` | Keep it as `private static readonly string[] Summaries` in the generator, as shown in TASK-001 Code

### Implementation Phase 2 - Extract Endpoints and Models

- **GOAL-002**: Move the endpoint mappings into `MapWeatherEndpoints`, and move the `WeatherForecast` record into `Models`, with the conversion divisor named
- **Completion criteria**: `dotnet test GoalGetter/GoalGetter.Tests` exits with 0 failures, including `WebTests`, and `Program.cs` contains no endpoint lambdas or record declarations

| Task | Description | Files | Completed | Date |
|------|-------------|-------|-----------|------|
| TASK-002 | Create `MapWeatherEndpoints` for `/` and `/weatherforecast`, and replace the inline mappings in `Program.cs` with one call | FILE-003, FILE-002 | | |
| TASK-003 | Move `WeatherForecast` to `Models`, name the `0.5556` divisor, and keep the arithmetic unchanged | FILE-001, FILE-002, FILE-004, FILE-005 | | |

#### TASK-002 Code

Source: https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis/route-handlers?view=aspnetcore-10.0#work-with-route-handlers

```csharp
// GoalGetter/GoalGetter.ApiService/Endpoints/WeatherEndpoints.cs
public static class WeatherEndpoints
{
    public static IEndpointRouteBuilder MapWeatherEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", () => "API service is running. Navigate to /weatherforecast to see sample data.");

        app.MapGet("/weatherforecast", (IForecastGenerator generator) => generator.Generate(5))
           .WithName("GetWeatherForecast");

        return app;
    }
}
```

```csharp
// GoalGetter/GoalGetter.ApiService/Program.cs
app.MapWeatherEndpoints();
```

The snippet follows the grouping pattern from the source sample. The route strings and the `WithName` value are unchanged from the current `Program.cs`.

#### TASK-002 Tests

- **TEST-002**: `Get_Root_ReturnsServiceMessage` in `GoalGetter/GoalGetter.Tests/WeatherEndpointTests.cs`. Calls `GET /` and asserts HTTP 200 and a body that starts with `API service is running`.

#### TASK-002 Boy Scout

No boy scout findings

#### TASK-003 Code

Source: Not applicable, no Microsoft API used

```csharp
// GoalGetter/GoalGetter.ApiService/Models/WeatherForecast.cs
namespace GoalGetter.ApiService.Models;

public record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    private const double FahrenheitScaleDivisor = 0.5556;

    public int TemperatureF => 32 + (int)(TemperatureC / FahrenheitScaleDivisor);
}
```

The divisor value is kept as `0.5556` so the output does not change. See RISK-001.

When the record moves into the `GoalGetter.ApiService.Models` namespace, `ForecastGenerator.cs` (FILE-001) needs `using GoalGetter.ApiService.Models;`. Removing the record from `Program.cs` (FILE-002) must happen in the same task so the project keeps compiling.

#### TASK-003 Tests

- **TEST-003**: `TemperatureF_MatchesCurrentConversion` in `GoalGetter/GoalGetter.Tests/WeatherEndpointTests.cs`. Builds `new WeatherForecast(DateOnly.MinValue, 0, null)` and asserts `TemperatureF` is 32. Builds one with `TemperatureC` 100 and asserts `TemperatureF` is 211, which is `32 + (int)(100 / 0.5556)`.

#### TASK-003 Boy Scout

- **BSC-003**: `GoalGetter/GoalGetter.ApiService/Models/WeatherForecast.cs` | Code smell (magic number) | The `0.5556` literal appears in the `TemperatureF` expression | Replaced by `FahrenheitScaleDivisor` in TASK-003 Code. Covered by TEST-003

## 3. Boy Scout Summary

| Item | File | Category | Phase / Task | Status |
|------|------|----------|--------------|--------|
| BSC-001 | FILE-001 | Magic numbers | Phase 1 / TASK-001 | Open |
| BSC-002 | FILE-001 | Misplaced data | Phase 1 / TASK-001 | Open |
| BSC-003 | FILE-004 | Magic number | Phase 2 / TASK-003 | Open |

## 4. Manual End-to-End Test

- **Prerequisites**: .NET 10 SDK, Aspire CLI, and a local clone of the repository
- **Start**: Run `aspire start` from `GoalGetter/`, then confirm `apiservice` and `webfrontend` are Running in the Aspire dashboard
- **E2E-001**: Precondition: `apiservice` is Running. Action: `GET {apiservice-url}/weatherforecast/` (`GoalGetter/GoalGetter.ApiService/GoalGetter.ApiService.http` uses `http://localhost:5485`). Expected: HTTP 200, a JSON array of five items, each with `date`, `temperatureC`, `temperatureF`, and `summary`
- **E2E-002**: Precondition: `apiservice` is Running. Action: `GET {apiservice-url}/` and then `GET {apiservice-url}/does-not-exist`. Expected: the root returns HTTP 200 with the service message, and the unknown route returns HTTP 404
- **E2E-003**: Precondition: `webfrontend` is Running. Action: open the `webfrontend` URL from the dashboard and load the home page. Expected: HTTP 200 with the page rendered, unchanged from before the refactor
- **Stop**: Run `aspire stop` when testing is complete

## 5. Alternatives

- **ALT-001**: Replace the divisor with `* 9.0 / 5.0`. Rejected in this plan because it changes `TemperatureF` values by up to one degree, which breaks CON-001. Recorded in RISK-001
- **ALT-002**: Leave the endpoints in `Program.cs` and only inject the generator. Rejected because it does not meet GUD-001

## 6. Dependencies

- **DEP-001**: `Random` must be registered in DI before the generator is resolved. The registration is part of TASK-001
- **DEP-002**: The integration tests use the AppHost resource name `apiservice` in `GoalGetter/GoalGetter.AppHost/AppHost.cs`

## 7. Files

- **FILE-001**: `GoalGetter/GoalGetter.ApiService/Services/ForecastGenerator.cs` | CREATE | `IForecastGenerator`, `RandomForecastGenerator` | Phase 1 / TASK-001
- **FILE-002**: `GoalGetter/GoalGetter.ApiService/Program.cs` | MODIFY | Registers the generator and `Random`, removes inline endpoints and record, calls `MapWeatherEndpoints` | Phase 1 / TASK-001 and Phase 2 / TASK-002
- **FILE-003**: `GoalGetter/GoalGetter.ApiService/Endpoints/WeatherEndpoints.cs` | CREATE | `WeatherEndpoints.MapWeatherEndpoints` | Phase 2 / TASK-002
- **FILE-004**: `GoalGetter/GoalGetter.ApiService/Models/WeatherForecast.cs` | CREATE | `WeatherForecast` record with `TemperatureF` | Phase 2 / TASK-003
- **FILE-005**: `GoalGetter/GoalGetter.Tests/WeatherEndpointTests.cs` | CREATE | `WeatherEndpointTests` with TEST-001, TEST-002, TEST-003 | Phase 1 / TASK-001 and Phase 2 / TASK-002, TASK-003

## 8. Testing Strategy

| Test Type | Project | Framework | Command |
|-----------|---------|-----------|---------|
| Build | `GoalGetter/GoalGetter.ApiService` | .NET SDK | `dotnet build GoalGetter/GoalGetter.ApiService/GoalGetter.ApiService.csproj` |
| Integration | `GoalGetter/GoalGetter.Tests` | xUnit v3 with Aspire testing | `dotnet test GoalGetter/GoalGetter.Tests --filter "FullyQualifiedName~WeatherEndpointTests"` |
| Regression | `GoalGetter/GoalGetter.Tests` | xUnit v3 with Aspire testing | `dotnet test GoalGetter/GoalGetter.Tests` (includes `WebTests`) |

## 9. Risks & Assumptions

- **RISK-001**: `TemperatureF` uses division by `0.5556` instead of multiplication by `9/5`. The two differ by up to one degree after truncation. Changing the formula is a product decision and is out of scope for this refactor
- **RISK-002**: `WebTests.GetWebResourceRootReturnsOkStatusCode` depends on the `webfrontend` resource. Failures there may come from the Web project and not from this refactor
- **ASSUMPTION-001**: `Random.Shared` is only used by the weather sample, so registering `Random` as a singleton does not affect other code

## 10. Related Specifications / Further Reading

- [Minimal API route handlers and route groups](https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis/route-handlers?view=aspnetcore-10.0#work-with-route-handlers)
- [Dependency injection in controllers and constructor injection](https://learn.microsoft.com/aspnet/core/mvc/controllers/dependency-injection?view=aspnetcore-10.0#constructor-injection)
