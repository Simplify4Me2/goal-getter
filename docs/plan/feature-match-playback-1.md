---
goal: Implement US-01, a football-realistic synthetic match that plays out live through the ApiService and the Web frontend
version: 1.0
date_created: 2026-10-11
last_updated: 2026-10-11
owner: GoalGetter team
status: 'Planned'
tags: [feature, api, aspire, streaming, synthetic-data, testing]
---

# Introduction

![Status: Planned](https://img.shields.io/badge/status-Planned-blue)

**Summary for reviewers**: Adds a deterministic synthetic match (fixture, lineups, and about 600 timed events with pitch coordinates) that is streamed in match-clock order at live pace through `GET /matches/{matchId}/events/stream`, and a `/match` page in the Web frontend that starts it. Success is recognized when the tests in all five phases pass and the manual flow in Section 4 shows events arriving in clock order at the configured pace.

This plan implements user story US-01 from `docs/user-story-map.md`: given a fixture with lineups and metadata, and events with IDs, timestamps and pitch coordinates (passes, shots, tackles, possession changes, pressure), when I start the match, then events arrive in match-clock order at live pace. The story is provisional until the open question in the story map is closed (synthetic event fields and overlay contract), so the event shape here is the smallest one that satisfies US-01 and is isolated in one shared contracts project so it can change in one place.

Definitions used in this plan:
- **Match clock**: whole seconds elapsed since kickoff, stored as `MatchEvent.ClockSeconds`, range 1 to 5400.
- **Live pace**: an event with clock value `c` is delivered `c / SpeedFactor` real seconds after the stream starts. `SpeedFactor` is `1.0` in normal use, so live pace is real time.
- **Pitch frame**: a 105 m by 68 m pitch. `X` runs from the home goal line (0) to the away goal line (105). `Y` runs from one touchline (0) to the other (68). The home team attacks toward increasing `X`.
- **SSE**: Server-Sent Events, a one-way HTTP stream with content type `text/event-stream`.

## 1. Requirements & Constraints

- **REQ-001**: A fixture contains match id, kickoff time, two teams, and one lineup of 11 players per team. Each player has an id, name, team id, shirt number, and position
- **REQ-002**: Every event has a unique id, match id, `ClockSeconds`, one of the types `Pass`, `Shot`, `Tackle`, `PossessionChange`, `Pressure`, a team id, a player id, and pitch coordinates `X` and `Y`
- **REQ-003**: Events are delivered in non-decreasing `ClockSeconds` order and in strictly increasing sequence order of their ids
- **REQ-004**: Event coordinates are always inside the pitch frame, and shots occur only in the attacking third of the pitch for the shooting team
- **REQ-005**: The same match id always yields the same event list, so tests and demos are reproducible
- **REQ-006**: Delivery follows live pace. With `SpeedFactor` 1.0, an event with clock `c` arrives `c` seconds after the stream starts, within a tolerance of 1 second
- **REQ-007**: `GET /matches/{matchId}/fixture` returns the fixture as JSON. `GET /matches/{matchId}/events/stream` starts the match for the caller and streams events as SSE
- **REQ-008**: The Web frontend `/match` page shows the fixture, a Start match button, the match clock, and the events as they arrive, with the player name resolved from the lineup
- **SEC-001**: `matchId` is validated before use. Zero or negative returns 400, unknown returns 404, and no response contains internal exception text
- **SEC-002**: `SpeedFactor` is bounded to 1 through 600 by options validation, so a bad value fails at startup and cannot create a zero or negative delay
- **CON-001**: Target `net10.0`, ASP.NET Core minimal APIs, and Aspire as in `GoalGetter/GoalGetter.sln`
- **CON-002**: Add one new project (`GoalGetter.Contracts`) and one new test package (`Microsoft.Extensions.TimeProvider.Testing`). No other new packages
- **CON-003**: Events are generated in memory. No database, queue, or AI service is added for US-01
- **CON-004**: The shared HTTP resilience handler from `GoalGetter.ServiceDefaults` has a total request timeout that would cut a 90-minute stream, so the streaming client removes it
- **GUD-001**: Map endpoints from an extension method in their own file. Keep `Program.cs` limited to host setup and mapping calls
- **GUD-002**: Inject `TimeProvider` wherever time or delay is used, so pace is tested without waiting
- **PAT-001**: Use the Options pattern with validation for `MatchPlaybackOptions`
- **PAT-002**: Use interfaces (`IFixtureCatalog`, `ISyntheticMatchGenerator`, `IMatchPlayback`) registered in dependency injection, so a live or stored feed can replace the synthetic one later

## 2. Implementation Steps

### Implementation Phase 1 - Shared Contracts and Fixture Catalog

- **GOAL-001**: Provide shared match contracts and an in-memory catalog with one fixture (match 1)
- **Completion criteria**: `dotnet test GoalGetter/GoalGetter.Tests --filter "FullyQualifiedName~ContractsTests|FullyQualifiedName~FixtureCatalogTests"` exits with 0 failures and 5 passed tests, and `dotnet build GoalGetter/GoalGetter.sln` exits with 0 errors

| Task | Description | Files | Completed | Date |
|------|-------------|-------|-----------|------|
| TASK-001 | Create the `GoalGetter.Contracts` class library with `Pitch`, `EventType`, `Player`, `Team`, `Fixture`, and `MatchEvent`. Add it to the solution and reference it from `GoalGetter.ApiService` and `GoalGetter.Tests` | FILE-001, FILE-002, FILE-003, FILE-004, FILE-005, FILE-006 | | |
| TASK-002 | Create `IFixtureCatalog` and `InMemoryFixtureCatalog` holding the fixture for match 1 with two lineups of 11 players | FILE-007, FILE-008 | | |

#### TASK-001 Code

Source: https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/customize-properties#enums-as-strings

Run from the repository root:

```bash
dotnet new classlib -n GoalGetter.Contracts -o GoalGetter/GoalGetter.Contracts -f net10.0
rm GoalGetter/GoalGetter.Contracts/Class1.cs
dotnet sln GoalGetter/GoalGetter.sln add GoalGetter/GoalGetter.Contracts/GoalGetter.Contracts.csproj
dotnet add GoalGetter/GoalGetter.ApiService/GoalGetter.ApiService.csproj reference GoalGetter/GoalGetter.Contracts/GoalGetter.Contracts.csproj
dotnet add GoalGetter/GoalGetter.Tests/GoalGetter.Tests.csproj reference GoalGetter/GoalGetter.Contracts/GoalGetter.Contracts.csproj GoalGetter/GoalGetter.ApiService/GoalGetter.ApiService.csproj
```

Set `<ImplicitUsings>enable</ImplicitUsings>` and `<Nullable>enable</Nullable>` in `GoalGetter.Contracts.csproj`, matching the other projects.

```csharp
// GoalGetter/GoalGetter.Contracts/Matches/MatchContracts.cs
using System.Text.Json.Serialization;

namespace GoalGetter.Contracts.Matches;

public static class Pitch
{
    public const double LengthMetres = 105;
    public const double WidthMetres = 68;
}

[JsonConverter(typeof(JsonStringEnumConverter<EventType>))]
public enum EventType { Pass, Shot, Tackle, PossessionChange, Pressure }

public sealed record Player(int Id, string Name, string TeamId, int ShirtNumber, string Position);

public sealed record Team(string Id, string Name, IReadOnlyList<Player> Lineup);

public sealed record Fixture(int MatchId, DateTimeOffset Kickoff, Team Home, Team Away);

public sealed record MatchEvent(
    string Id, int MatchId, int ClockSeconds, EventType Type, string TeamId, int PlayerId, double X, double Y);
```

The enum attribute follows the source sample, adapted to `EventType`. The `Pitch` frame is described in the Introduction.

The test project also needs `<Using Include="GoalGetter.Contracts.Matches" />` in `GoalGetter.Tests.csproj`.

#### TASK-001 Tests

- **TEST-001**: `MatchEvent_SerializesTypeAsString` in `GoalGetter/GoalGetter.Tests/ContractsTests.cs`. Serializes a `MatchEvent` with `JsonSerializer.Serialize(value, JsonSerializerOptions.Web)`. Asserts the JSON contains `"type":"PossessionChange"` and not a number.
- **TEST-002**: `MatchEvent_RoundTripsThroughJson` in `GoalGetter/GoalGetter.Tests/ContractsTests.cs`. Serializes then deserializes with `JsonSerializerOptions.Web`. Asserts the result equals the original record.
- **TEST-003**: Build check. `dotnet build GoalGetter/GoalGetter.sln` exits with 0 errors. Confirms the new project is in the solution and both references resolve.

#### TASK-001 Boy Scout

No boy scout findings

#### TASK-002 Code

Source: https://learn.microsoft.com/aspnet/core/mvc/controllers/dependency-injection?view=aspnetcore-10.0#constructor-injection

```csharp
// GoalGetter/GoalGetter.ApiService/Matches/FixtureCatalog.cs
using GoalGetter.Contracts.Matches;

namespace GoalGetter.ApiService.Matches;

public interface IFixtureCatalog
{
    bool TryGet(int matchId, out Fixture fixture);
}

public sealed class InMemoryFixtureCatalog : IFixtureCatalog
{
    private const string HomeId = "HCF";
    private const string AwayId = "NGR";
    private const int AwayFirstPlayerId = 12;

    private static readonly string[] Positions =
        ["GK", "RB", "CB", "CB", "LB", "DM", "CM", "CM", "RW", "ST", "LW"];

    private static readonly Fixture MatchOne = new(
        MatchId: 1,
        Kickoff: new DateTimeOffset(2026, 10, 17, 15, 0, 0, TimeSpan.Zero),
        Home: new Team(HomeId, "Harbor City FC", Lineup(HomeId, 1,
            "Tomas Brandt", "Luis Ferreira", "Oskar Lindqvist", "Rafael Mendes", "Jonas Weber", "Kenji Mori",
            "Andre Costa", "Marco Silva", "Diego Rivera", "Samir Haddad", "Elias Novak")),
        Away: new Team(AwayId, "Northgate Rovers", Lineup(AwayId, AwayFirstPlayerId,
            "Pavel Kral", "Declan Walsh", "Hugo Laurent", "Mikael Berg", "Tariq Bello", "Ivan Petrov",
            "Nico Santos", "Liam Hughes", "Yusuf Demir", "Felix Aaron", "Joao Teixeira")));

    public bool TryGet(int matchId, out Fixture fixture)
    {
        fixture = MatchOne;
        return matchId == MatchOne.MatchId;
    }

    private static Player[] Lineup(string teamId, int firstPlayerId, params string[] names) =>
        names.Select((name, index) =>
            new Player(firstPlayerId + index, name, teamId, index + 1, Positions[index])).ToArray();
}
```

`TryGet` follows the standard .NET `Try` pattern. When it returns false, callers must not use `fixture`. `Positions` is declared above `MatchOne` because static initializers run in textual order.

Register in `AddMatchServices` (TASK-005): `services.AddSingleton<IFixtureCatalog, InMemoryFixtureCatalog>();`.

#### TASK-002 Tests

- **TEST-004**: `TryGet_Match1_ReturnsFixtureWithTwoFullLineups` in `GoalGetter/GoalGetter.Tests/FixtureCatalogTests.cs`. Asserts `TryGet(1, out var fixture)` is true, each lineup has 11 players, every player's `TeamId` equals its team's `Id`, and all 22 player ids are distinct.
- **TEST-005**: `TryGet_UnknownMatch_ReturnsFalse` in `GoalGetter/GoalGetter.Tests/FixtureCatalogTests.cs`. Asserts `TryGet(999, out _)` is false.
- **TEST-006**: `TryGet_Match1_LineupHasOneGoalkeeperPerTeam` in `GoalGetter/GoalGetter.Tests/FixtureCatalogTests.cs`. Asserts exactly one player with `Position == "GK"` per team.

#### TASK-002 Boy Scout

No boy scout findings

### Implementation Phase 2 - Synthetic Match Generator

- **GOAL-002**: Generate a deterministic, football-realistic list of timed events with pitch coordinates for a fixture
- **Completion criteria**: `dotnet test GoalGetter/GoalGetter.Tests --filter "FullyQualifiedName~SyntheticMatchGeneratorTests"` exits with 0 failures and 7 passed tests

| Task | Description | Files | Completed | Date |
|------|-------------|-------|-----------|------|
| TASK-003 | Create `ISyntheticMatchGenerator` and `SyntheticMatchGenerator`. A possession state machine, seeded by the match id, steps the clock by 3 to 15 seconds, moves the ball toward the possessing team's goal, and emits one event per step | FILE-009, FILE-010 | | |

#### TASK-003 Code

Source: https://learn.microsoft.com/dotnet/api/system.random.-ctor?view=net-10.0

```csharp
// GoalGetter/GoalGetter.ApiService/Matches/SyntheticMatchGenerator.cs
using GoalGetter.Contracts.Matches;

namespace GoalGetter.ApiService.Matches;

public interface ISyntheticMatchGenerator
{
    IReadOnlyList<MatchEvent> Generate(Fixture fixture);
}

public sealed class SyntheticMatchGenerator : ISyntheticMatchGenerator
{
    public const int MatchDurationSeconds = 5400;

    private const int MinStepSeconds = 3;
    private const int MaxStepSeconds = 15;
    private const int PassBelow = 60;               // rolls 0-59: pass
    private const int PressureBelow = 75;           // rolls 60-74: pressure
    private const int TackleBelow = 85;             // rolls 75-84: tackle
    private const int PossessionChangeBelow = 93;   // rolls 85-92: possession change, 93-99: shot attempt
    private const double AttackingThirdMetres = Pitch.LengthMetres / 3;

    public IReadOnlyList<MatchEvent> Generate(Fixture fixture)
    {
        var random = new Random(fixture.MatchId);
        var events = new List<MatchEvent>();
        var possession = fixture.Home;
        double x = Pitch.LengthMetres / 2, y = Pitch.WidthMetres / 2;
        var clock = 0;

        while ((clock += random.Next(MinStepSeconds, MaxStepSeconds + 1)) <= MatchDurationSeconds)
        {
            var homeHasBall = possession.Id == fixture.Home.Id;
            var defending = homeHasBall ? fixture.Away : fixture.Home;
            var direction = homeHasBall ? 1 : -1;
            var inAttackingThird = homeHasBall
                ? x >= Pitch.LengthMetres - AttackingThirdMetres
                : x <= AttackingThirdMetres;

            var type = PickType(random.Next(100), inAttackingThird);
            if (type == EventType.Pass)
            {
                x = Math.Clamp(x + direction * random.Next(-5, 16), 0, Pitch.LengthMetres);
                y = Math.Clamp(y + random.Next(-10, 11), 0, Pitch.WidthMetres);
            }

            var actor = type is EventType.Pass or EventType.Shot ? possession : defending;
            var player = actor.Lineup[random.Next(actor.Lineup.Count)];
            events.Add(new MatchEvent(
                $"{fixture.MatchId}-{events.Count + 1:D4}", fixture.MatchId, clock, type, actor.Id, player.Id, x, y));

            if (type == EventType.PossessionChange)
            {
                possession = defending;
            }
        }

        return events;
    }

    private static EventType PickType(int roll, bool inAttackingThird) => roll switch
    {
        < PassBelow => EventType.Pass,
        < PressureBelow => EventType.Pressure,
        < TackleBelow => EventType.Tackle,
        < PossessionChangeBelow => EventType.PossessionChange,
        _ => inAttackingThird ? EventType.Shot : EventType.Pass,
    };
}
```

The snippet adapts the seeded `Random(int seed)` constructor, which returns the same sequence for the same seed (REQ-005). A shot roll outside the attacking third becomes a pass, which keeps REQ-004. The ball position changes only on `Pass`; other event types are recorded at the current ball position.

Register in `AddMatchServices` (TASK-005): `services.AddSingleton<ISyntheticMatchGenerator, SyntheticMatchGenerator>();`.

#### TASK-003 Tests

All tests are in `GoalGetter/GoalGetter.Tests/SyntheticMatchGeneratorTests.cs` and use the fixture from `InMemoryFixtureCatalog.TryGet(1, ...)`.

- **TEST-007**: `Generate_ReturnsEventsInNonDecreasingClockOrder`. Asserts `ClockSeconds` never decreases, the first value is at least 3, and the last value is at most 5400. Expected: pass.
- **TEST-008**: `Generate_AssignsUniqueSequentialIds`. Asserts ids equal `1-0001`, `1-0002`, and so on in list order, with no duplicates.
- **TEST-009**: `Generate_CoversAllFiveEventTypes`. Asserts every `EventType` value appears at least once.
- **TEST-010**: `Generate_KeepsCoordinatesInsidePitch`. Asserts `0 <= X <= 105` and `0 <= Y <= 68` for every event.
- **TEST-011**: `Generate_TagsPlayersFromTheActingTeamLineup`. Asserts each event's `PlayerId` is in the lineup of the team with `TeamId`.
- **TEST-012**: `Generate_ShotsOnlyHappenInAttackingThird`. Asserts home shots have `X >= 70` and away shots have `X <= 35`.
- **TEST-013**: `Generate_SameFixtureTwice_ReturnsIdenticalEvents`. Asserts the two lists are element-wise equal.

#### TASK-003 Boy Scout

No boy scout findings

### Implementation Phase 3 - Live-Pace Playback and Registration

- **GOAL-003**: Deliver generated events at live pace with injected time, and register all match services
- **Completion criteria**: `dotnet test GoalGetter/GoalGetter.Tests --filter "FullyQualifiedName~MatchPlaybackTests|FullyQualifiedName~MatchServiceRegistrationTests"` exits with 0 failures and 8 passed tests

| Task | Description | Files | Completed | Date |
|------|-------------|-------|-----------|------|
| TASK-004 | Create `MatchPlaybackOptions` (validated `SpeedFactor`) and `IMatchPlayback` with `MatchPlayback`, an async iterator that waits until each event's due time using `TimeProvider` | FILE-005, FILE-011, FILE-012, FILE-013 | | |
| TASK-005 | Create `AddMatchServices` to register options, `TimeProvider`, catalog, generator, and playback, and call it from `Program.cs` | FILE-014, FILE-015, FILE-016 | | |

#### TASK-004 Code

Source: https://learn.microsoft.com/dotnet/core/extensions/timeprovider-testing#test-delayed-operations

Add the test-only package from the repository root. The version resolves to the latest compatible release:

```bash
dotnet add GoalGetter/GoalGetter.Tests/GoalGetter.Tests.csproj package Microsoft.Extensions.TimeProvider.Testing
```

```csharp
// GoalGetter/GoalGetter.ApiService/Matches/MatchPlaybackOptions.cs
using System.ComponentModel.DataAnnotations;

namespace GoalGetter.ApiService.Matches;

public sealed class MatchPlaybackOptions
{
    public const string SectionName = "MatchPlayback";

    [Range(1.0, 600.0)]
    public double SpeedFactor { get; set; } = 1.0;
}
```

```csharp
// GoalGetter/GoalGetter.ApiService/Matches/MatchPlayback.cs
using System.Runtime.CompilerServices;
using GoalGetter.Contracts.Matches;
using Microsoft.Extensions.Options;

namespace GoalGetter.ApiService.Matches;

public interface IMatchPlayback
{
    IAsyncEnumerable<MatchEvent> PlayAsync(Fixture fixture, CancellationToken cancellationToken);
}

public sealed class MatchPlayback(
    ISyntheticMatchGenerator generator,
    IOptions<MatchPlaybackOptions> options,
    TimeProvider timeProvider) : IMatchPlayback
{
    public async IAsyncEnumerable<MatchEvent> PlayAsync(
        Fixture fixture, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var speedFactor = options.Value.SpeedFactor;
        var started = timeProvider.GetTimestamp();

        foreach (var matchEvent in generator.Generate(fixture))
        {
            var due = TimeSpan.FromSeconds(matchEvent.ClockSeconds / speedFactor);
            var wait = due - timeProvider.GetElapsedTime(started);
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, timeProvider, cancellationToken);
            }

            yield return matchEvent;
        }
    }
}
```

The `Task.Delay(delay, timeProvider)` overload is the one shown in the source sample. Computing `due` from the stream start, not from the previous event, keeps delivery from drifting when a consumer is slow: late events are sent immediately and later events still arrive on schedule.

#### TASK-004 Tests

All tests are in `GoalGetter/GoalGetter.Tests/MatchPlaybackTests.cs`. They use `FakeTimeProvider` and a stub `ISyntheticMatchGenerator` that returns events at clock 10, 25, and 25 (two events at the same second) so no real waiting occurs. Each test starts the enumerator with `GetAsyncEnumerator`, calls `MoveNextAsync().AsTask()`, and advances the fake clock with `Advance`.

- **TEST-014**: `PlayAsync_FirstEvent_IsNotDeliveredBeforeItsClockTime`. At 1.0 speed, asserts the first `MoveNextAsync` task is not completed after advancing 9 seconds and is completed after advancing 1 more second.
- **TEST-015**: `PlayAsync_EventsSharingASecond_AreDeliveredTogetherInOrder`. After advancing to 25 seconds, asserts events 2 and 3 are both delivered in list order without further advance.
- **TEST-016**: `PlayAsync_SpeedFactor60_DividesDelayBy60`. With `SpeedFactor` 60, asserts the first event (clock 10) is delivered after advancing 1/6 of a second.
- **TEST-017**: `PlayAsync_SlowConsumer_DoesNotDelayLaterEvents`. Delays consuming event 1 until the fake clock is at 30 seconds, then asserts events 2 and 3 are delivered immediately.
- **TEST-018**: `PlayAsync_Cancelled_StopsWithoutDeliveringFurtherEvents`. Cancels the token while waiting for event 1 and asserts `MoveNextAsync` throws `OperationCanceledException`.

#### TASK-004 Boy Scout

No boy scout findings

#### TASK-005 Code

Source: https://learn.microsoft.com/dotnet/core/extensions/options#options-validation

```csharp
// GoalGetter/GoalGetter.ApiService/Matches/MatchServiceCollectionExtensions.cs
namespace GoalGetter.ApiService.Matches;

public static class MatchServiceCollectionExtensions
{
    public static IServiceCollection AddMatchServices(this IServiceCollection services)
    {
        services.AddOptions<MatchPlaybackOptions>()
            .BindConfiguration(MatchPlaybackOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IFixtureCatalog, InMemoryFixtureCatalog>();
        services.AddSingleton<ISyntheticMatchGenerator, SyntheticMatchGenerator>();
        services.AddSingleton<IMatchPlayback, MatchPlayback>();

        return services;
    }
}
```

```csharp
// GoalGetter/GoalGetter.ApiService/Program.cs, after builder.Services.AddProblemDetails();
builder.Services.AddMatchServices();
```

`using GoalGetter.ApiService.Matches;` is added to the top of `Program.cs`. The options chain is the `AddOptions`, `BindConfiguration`, `ValidateDataAnnotations`, `ValidateOnStart` pattern from the source.

#### TASK-005 Tests

- **TEST-019**: `AddMatchServices_DefaultOptions_ResolvesPlaybackWithSpeedFactor1` in `GoalGetter/GoalGetter.Tests/MatchServiceRegistrationTests.cs`. Builds a `ServiceCollection` with an empty in-memory `IConfiguration`, calls `AddMatchServices`, resolves `IMatchPlayback` and `IOptions<MatchPlaybackOptions>`. Asserts both resolve and `SpeedFactor` equals 1.0.
- **TEST-020**: `AddMatchServices_SpeedFactorZero_ThrowsOptionsValidationException` in `GoalGetter/GoalGetter.Tests/MatchServiceRegistrationTests.cs`. Sets `MatchPlayback:SpeedFactor` to `0` and asserts reading `IOptions<MatchPlaybackOptions>.Value` throws `OptionsValidationException`.
- **TEST-021**: `AddMatchServices_SpeedFactor601_ThrowsOptionsValidationException` in `GoalGetter/GoalGetter.Tests/MatchServiceRegistrationTests.cs`. Same as TEST-020 with `601`.

#### TASK-005 Boy Scout

No boy scout findings

### Implementation Phase 4 - API Endpoints and Aspire Integration

- **GOAL-004**: Expose the fixture and the live event stream over HTTP, and verify them end to end through the Aspire AppHost
- **Completion criteria**: `dotnet test GoalGetter/GoalGetter.Tests` exits with 0 failures, including `WebTests` and `MatchEndpointsTests`

| Task | Description | Files | Completed | Date |
|------|-------------|-------|-----------|------|
| TASK-006 | Create `MapMatchEndpoints` with the fixture route and the SSE stream route, validate `matchId`, and call it from `Program.cs` | FILE-015, FILE-017, FILE-018 | | |
| TASK-007 | Let the AppHost pass `MatchPlayback:SpeedFactor` to `apiservice`, add a shared `TestAppHost` helper, move `WebTests` onto it, and add integration tests for both routes | FILE-019, FILE-020, FILE-021, FILE-022 | | |

#### TASK-006 Code

Source: https://learn.microsoft.com/aspnet/core/release-notes/aspnetcore-10.0?view=aspnetcore-10.0#minimal-apis

```csharp
// GoalGetter/GoalGetter.ApiService/Endpoints/MatchEndpoints.cs
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using GoalGetter.ApiService.Matches;
using GoalGetter.Contracts.Matches;

namespace GoalGetter.ApiService.Endpoints;

public static class MatchEndpoints
{
    public const string MatchEventName = "matchEvent";

    public static IEndpointRouteBuilder MapMatchEndpoints(this IEndpointRouteBuilder app)
    {
        var matches = app.MapGroup("/matches");

        matches.MapGet("/{matchId:int}/fixture", (int matchId, IFixtureCatalog catalog) =>
            matchId <= 0 ? Results.BadRequest()
            : catalog.TryGet(matchId, out var fixture) ? Results.Ok(fixture)
            : Results.NotFound());

        matches.MapGet("/{matchId:int}/events/stream",
            (int matchId, IFixtureCatalog catalog, IMatchPlayback playback, CancellationToken cancellationToken) =>
                matchId <= 0 ? Results.BadRequest()
                : !catalog.TryGet(matchId, out var fixture) ? Results.NotFound()
                : TypedResults.ServerSentEvents(ToSseItems(playback, fixture, cancellationToken)));

        return app;
    }

    private static async IAsyncEnumerable<SseItem<MatchEvent>> ToSseItems(
        IMatchPlayback playback, Fixture fixture, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var matchEvent in playback.PlayAsync(fixture, cancellationToken))
        {
            yield return new SseItem<MatchEvent>(matchEvent, MatchEventName) { EventId = matchEvent.Id };
        }
    }
}
```

```csharp
// GoalGetter/GoalGetter.ApiService/Program.cs, after the existing app.MapGet calls
app.MapMatchEndpoints();
```

`using GoalGetter.ApiService.Endpoints;` is added to `Program.cs`. The `SseItem<T>` iterator with `TypedResults.ServerSentEvents` is the pattern from the source and from https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis/responses?view=aspnetcore-10.0#iresult-return-values. The two routes return `IResult`, so the ternary branches share one type. The stream ends when the last event (clock about 5400) is sent, which closes the connection. Starting a match is connecting to the stream, and each connection plays its own copy from kickoff (see ALT-001).

Add requests to `GoalGetter/GoalGetter.ApiService/GoalGetter.ApiService.http` below the weather request:

```http
GET {{ApiService_HostAddress}}/matches/1/fixture
Accept: application/json

###

# Streams events at live pace. Use curl -N or the Aspire dashboard; this client buffers SSE.
GET {{ApiService_HostAddress}}/matches/1/events/stream
Accept: text/event-stream

###
```

#### TASK-006 Tests

Covered by the integration tests declared under TASK-007, which call both routes through the AppHost.

- **TEST-022**: Build check. `dotnet build GoalGetter/GoalGetter.ApiService/GoalGetter.ApiService.csproj` exits with 0 errors. Confirms `Program.cs` compiles with the endpoint mapping.

#### TASK-006 Boy Scout

No boy scout findings

#### TASK-007 Code

Source: https://learn.microsoft.com/dotnet/aspire/testing/write-your-first-test

Source: https://learn.microsoft.com/dotnet/aspire/compatibility/9.4/getsecretoutput-deprecated (shows `WithEnvironment(name, value)` on a resource builder; adapted to the string overload)

```csharp
// GoalGetter/GoalGetter.AppHost/AppHost.cs
var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.GoalGetter_ApiService>("apiservice")
    .WithHttpHealthCheck("/health");

if (builder.Configuration["MatchPlayback:SpeedFactor"] is { } speedFactor)
{
    apiService.WithEnvironment("MatchPlayback__SpeedFactor", speedFactor);
}

builder.AddProject<Projects.GoalGetter_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
```

```csharp
// GoalGetter/GoalGetter.Tests/TestAppHost.cs
using Microsoft.Extensions.Logging;

namespace GoalGetter.Tests;

internal static class TestAppHost
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    public static async Task<DistributedApplication> StartAsync(
        string[] appHostArgs, CancellationToken cancellationToken)
    {
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.GoalGetter_AppHost>(
            appHostArgs, cancellationToken);
        appHost.Services.AddLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Debug);
            logging.AddFilter(appHost.Environment.ApplicationName, LogLevel.Debug);
            logging.AddFilter("Aspire.", LogLevel.Debug);
        });
        appHost.Services.ConfigureHttpClientDefaults(clientBuilder => clientBuilder.AddStandardResilienceHandler());

        var app = await appHost.BuildAsync(cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);
        await app.StartAsync(cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);
        return app;
    }
}
```

```csharp
// GoalGetter/GoalGetter.Tests/WebTests.cs, GetWebResourceRootReturnsOkStatusCode after the change
await using var app = await TestAppHost.StartAsync([], cancellationToken);
var httpClient = app.CreateHttpClient("webfrontend");
await app.ResourceNotifications.WaitForResourceHealthyAsync("webfrontend", cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);
var response = await httpClient.GetAsync("/", cancellationToken);
Assert.Equal(HttpStatusCode.OK, response.StatusCode);
```

The helper keeps the existing logging filters and resilience handler from `WebTests` unchanged. `DefaultTimeout` stays a private constant in `WebTests` for the health waits. The `appHostArgs` overload of `CreateAsync` is documented as the way to pass AppHost arguments; see ASSUMPTION-001 for the verification step.

```csharp
// GoalGetter/GoalGetter.Tests/MatchEndpointsTests.cs, stream test outline
await using var app = await TestAppHost.StartAsync(["MatchPlayback:SpeedFactor=600"], cancellationToken);
await app.ResourceNotifications.WaitForResourceHealthyAsync("apiservice", cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);
var client = app.CreateHttpClient("apiservice");

using var request = new HttpRequestMessage(HttpMethod.Get, "/matches/1/events/stream");
using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

var received = new List<SseItem<MatchEvent>>();
await foreach (var item in SseParser.Create(stream, (_, bytes) => JsonSerializer.Deserialize<MatchEvent>(bytes, JsonSerializerOptions.Web)!).EnumerateAsync(cancellationToken))
{
    received.Add(item);
    if (received.Count == 10) break;
}
```

The SSE parsing call is the `SseParser.Create(...).EnumerateAsync()` pattern from https://learn.microsoft.com/dotnet/core/whats-new/dotnet-9/libraries#networking, with a typed item parser.

#### TASK-007 Tests

All tests below are in `GoalGetter/GoalGetter.Tests/MatchEndpointsTests.cs` unless a different file is named. Each starts the AppHost through `TestAppHost.StartAsync`.

- **TEST-023**: `GetFixture_Match1_ReturnsFixtureJson`. Calls `GET /matches/1/fixture`. Asserts HTTP 200 and that the deserialized `Fixture` has two lineups of 11 players.
- **TEST-024**: `GetFixture_UnknownMatch_ReturnsNotFound`. Calls `GET /matches/999/fixture`. Asserts HTTP 404.
- **TEST-025**: `GetFixture_ZeroId_ReturnsBadRequest`. Calls `GET /matches/0/fixture`. Asserts HTTP 400 and a body with no stack trace text.
- **TEST-026**: `StreamEvents_Match1_DeliversTenEventsInClockOrder`. Starts with `MatchPlayback:SpeedFactor=600`, reads 10 SSE items. Asserts status 200, content type `text/event-stream`, event name `matchEvent` for every item, `ClockSeconds` non-decreasing, ids `1-0001` to `1-0010` in order, and each item's `EventId` equals its data id.
- **TEST-027**: `StreamEvents_UnknownMatch_ReturnsNotFoundWithoutStream`. Calls `GET /matches/999/events/stream`. Asserts HTTP 404 and no `text/event-stream` content type.
- **TEST-028**: `StreamEvents_ZeroId_ReturnsBadRequest`. Calls `GET /matches/0/events/stream`. Asserts HTTP 400.
- **TEST-029**: `GetWebResourceRootReturnsOkStatusCode` in `GoalGetter/GoalGetter.Tests/WebTests.cs`. Existing test, now using `TestAppHost`. Asserts HTTP 200. Proves BSC-001 preserves behavior.

#### TASK-007 Boy Scout

- **BSC-001**: `GoalGetter/GoalGetter.Tests/WebTests.cs` | DRY | The AppHost creation, logging filters, resilience handler, build, and start block would be copied into every new integration test class | Move the block into `TestAppHost.StartAsync` and call it from `WebTests` and `MatchEndpointsTests`. Covered by TEST-029

### Implementation Phase 5 - Web Frontend Match Page

- **GOAL-005**: Let a user open `/match`, press Start match, and watch events arrive with the match clock and resolved player names
- **Completion criteria**: `dotnet test GoalGetter/GoalGetter.Tests --filter "FullyQualifiedName~MatchApiClientTests|FullyQualifiedName~WebTests"` exits with 0 failures and 6 passed tests

| Task | Description | Files | Completed | Date |
|------|-------------|-------|-----------|------|
| TASK-008 | Create `MatchApiClient` (fixture call and SSE event stream), register it without the resilience handler and with an infinite timeout, and name the API base address once | FILE-005, FILE-023, FILE-024, FILE-025, FILE-026 | | |
| TASK-009 | Create the `/match` page and add its navigation link | FILE-021, FILE-027, FILE-028 | | |

#### TASK-008 Code

Source: https://learn.microsoft.com/dotnet/core/whats-new/dotnet-9/libraries#networking

Source: https://learn.microsoft.com/dotnet/core/resilience/http-resilience#remove-standard-resilience-handlers

Add `<ProjectReference Include="..\GoalGetter.Contracts\GoalGetter.Contracts.csproj" />` to `GoalGetter.Web.csproj`. Add `<ProjectReference Include="..\GoalGetter.Web\GoalGetter.Web.csproj" />` to `GoalGetter.Tests.csproj`.

```csharp
// GoalGetter/GoalGetter.Web/MatchApiClient.cs
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text.Json;
using GoalGetter.Contracts.Matches;

namespace GoalGetter.Web;

public class MatchApiClient(HttpClient httpClient)
{
    public Task<Fixture?> GetFixtureAsync(int matchId, CancellationToken cancellationToken = default) =>
        httpClient.GetFromJsonAsync<Fixture>($"/matches/{matchId}/fixture", cancellationToken);

    public async IAsyncEnumerable<MatchEvent> StreamEventsAsync(
        int matchId, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/matches/{matchId}/events/stream");
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var parser = SseParser.Create(stream, (_, data) => JsonSerializer.Deserialize<MatchEvent>(data, JsonSerializerOptions.Web)!);
        await foreach (var item in parser.EnumerateAsync(cancellationToken))
        {
            yield return item.Data;
        }
    }
}
```

```csharp
// GoalGetter/GoalGetter.Web/Program.cs
const string ApiServiceUri = "https+http://apiservice";

builder.Services.AddHttpClient<WeatherApiClient>(client => client.BaseAddress = new(ApiServiceUri));

builder.Services.AddHttpClient<MatchApiClient>(client =>
    {
        client.BaseAddress = new(ApiServiceUri);
        client.Timeout = Timeout.InfiniteTimeSpan;
    })
    .RemoveAllResilienceHandlers();
```

`RemoveAllResilienceHandlers` removes the handler that `ConfigureHttpClientDefaults` in `GoalGetter.ServiceDefaults/Extensions.cs` adds to every client (CON-004). The existing `WeatherApiClient` registration keeps its resilience handler and its existing `https+http://` comment about scheme resolution is kept above `ApiServiceUri`.

#### TASK-008 Tests

All tests are in `GoalGetter/GoalGetter.Tests/MatchApiClientTests.cs` and use a stub `HttpMessageHandler` that returns a canned response, so no AppHost is needed.

- **TEST-030**: `StreamEventsAsync_ParsesSseBody_YieldsEventsInOrder`. The stub returns `text/event-stream` with three `event: matchEvent` frames. Asserts three `MatchEvent` values in the same order with `Type` parsed from its string name.
- **TEST-031**: `StreamEventsAsync_NotFound_ThrowsHttpRequestException`. The stub returns 404. Asserts `HttpRequestException`.
- **TEST-032**: `GetFixtureAsync_ReturnsDeserializedFixture`. The stub returns a fixture JSON body. Asserts the home team name and lineup size.
- **TEST-033**: `GetWeatherPageReturnsOkStatusCode` in `GoalGetter/GoalGetter.Tests/WebTests.cs`. Starts the AppHost, waits for `webfrontend` and `apiservice` to be healthy, and calls `GET /weather`. Asserts HTTP 200. Proves BSC-002 keeps the weather client base address working.

#### TASK-008 Boy Scout

- **BSC-002**: `GoalGetter/GoalGetter.Web/Program.cs` | DRY | The literal `"https+http://apiservice"` would be repeated once the second typed client is registered | Extract `const string ApiServiceUri` and use it for both clients. Covered by TEST-033 and TEST-030

#### TASK-009 Code

Source: https://learn.microsoft.com/aspnet/core/blazor/components/rendering?view=aspnetcore-10.0#statehaschanged

```razor
@* GoalGetter/GoalGetter.Web/Components/Pages/Match.razor *@
@page "/match"
@rendermode InteractiveServer
@using GoalGetter.Contracts.Matches
@implements IDisposable
@inject MatchApiClient MatchApi

<PageTitle>Match</PageTitle>

<h1>Match</h1>

@if (fixture is null)
{
    <p><em>Loading fixture...</em></p>
}
else
{
    <h2>@fixture.Home.Name vs @fixture.Away.Name</h2>
    <p role="status">Clock: @Clock</p>
    <button class="btn btn-primary" @onclick="StartMatchAsync" disabled="@running">Start match</button>

    @if (error is not null)
    {
        <p class="text-danger" role="alert">@error</p>
    }

    <ul>
        @foreach (var matchEvent in events)
        {
            <li>@FormatClock(matchEvent.ClockSeconds) @matchEvent.Type, @PlayerName(matchEvent), (@matchEvent.X.ToString("0"), @matchEvent.Y.ToString("0"))</li>
        }
    </ul>
}

@code {
    private const int MatchId = 1;
    private const int MaxVisibleEvents = 50;

    private readonly CancellationTokenSource cts = new();
    private readonly List<MatchEvent> events = [];
    private Fixture? fixture;
    private bool running;
    private string? error;
    private int clockSeconds;

    private string Clock => FormatClock(clockSeconds);

    protected override async Task OnInitializedAsync()
    {
        try
        {
            fixture = await MatchApi.GetFixtureAsync(MatchId, cts.Token);
        }
        catch (HttpRequestException)
        {
            error = "Could not load the match. Try again.";
        }
    }

    private async Task StartMatchAsync()
    {
        running = true;
        error = null;
        events.Clear();

        try
        {
            await foreach (var matchEvent in MatchApi.StreamEventsAsync(MatchId, cts.Token))
            {
                clockSeconds = matchEvent.ClockSeconds;
                events.Insert(0, matchEvent);
                if (events.Count > MaxVisibleEvents) events.RemoveAt(events.Count - 1);
                await InvokeAsync(StateHasChanged);
            }
        }
        catch (HttpRequestException)
        {
            error = "Could not start the match. Try again.";
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            running = false;
        }
    }

    private string PlayerName(MatchEvent matchEvent) =>
        fixture!.Home.Lineup.Concat(fixture.Away.Lineup).First(p => p.Id == matchEvent.PlayerId).Name;

    private static string FormatClock(int seconds) => $"{seconds / 60:00}:{seconds % 60:00}";

    public void Dispose() => cts.Cancel();
}
```

The `@using GoalGetter.Contracts.Matches` line is placed in `Match.razor`, so `_Imports.razor` is not touched. The component structure (`InvokeAsync(StateHasChanged)` from a background loop, and `IDisposable` cleanup) follows the source sample. `PlayerName` looks up the lineup per row, which is acceptable for at most 50 visible rows.

Add the navigation link in `GoalGetter/GoalGetter.Web/Components/Layout/NavMenu.razor`, after the Weather item:

```razor
<div class="nav-item px-3">
    <NavLink class="nav-link" href="match">
        <span class="bi bi-list-nested" aria-hidden="true"></span> Match
    </NavLink>
</div>
```

#### TASK-009 Tests

- **TEST-034**: `GetMatchPageReturnsOkStatusCodeWithStartButton` in `GoalGetter/GoalGetter.Tests/WebTests.cs`. Starts the AppHost, waits for `webfrontend` and `apiservice` to be healthy, calls `GET /match`. Asserts HTTP 200 and a body containing `Start match` and `Harbor City FC`. The pre-rendered page includes the fixture because `OnInitializedAsync` runs on the server.
- **TEST-035**: Build check. `dotnet build GoalGetter/GoalGetter.sln` exits with 0 errors and 0 warnings in `GoalGetter.Web`. Confirms the Razor component compiles.

The streaming behavior of the page after the button click is interactive Blazor and is covered by E2E-003.

#### TASK-009 Boy Scout

No boy scout findings

## 3. Boy Scout Summary

| Item | File | Category | Phase / Task | Status |
|------|------|----------|--------------|--------|
| BSC-001 | FILE-021 | DRY | Phase 4 / TASK-007 | Open |
| BSC-002 | FILE-025 | DRY | Phase 5 / TASK-008 | Open |

## 4. Manual End-to-End Test

- **Prerequisites**: .NET 10 SDK, Aspire CLI, a modern browser, and `curl`. No credentials are needed. All five phases are complete and `dotnet test GoalGetter/GoalGetter.Tests` passes
- **Start**: Run `aspire start` from `GoalGetter/`, then open the Aspire dashboard and confirm `apiservice` and `webfrontend` are Running and healthy
- **E2E-001**: Precondition: `apiservice` is Running. Action: `GET {apiservice-url}/matches/1/fixture` (use the endpoint URL shown for `apiservice` in the dashboard; `GoalGetter/GoalGetter.ApiService/GoalGetter.ApiService.http` uses `http://localhost:5485`). Expected: HTTP 200 and JSON with `matchId` 1, kickoff, `home.name` "Harbor City FC", `away.name` "Northgate Rovers", and 11 players in each `lineup`. The dashboard `apiservice` traces show the request with status 200.
- **E2E-002**: Precondition: `apiservice` is Running at normal pace (`SpeedFactor` 1.0). Action: `curl -N {apiservice-url}/matches/1/events/stream`, and watch for 60 seconds. Expected: `event: matchEvent` frames appear, the first within 3 to 15 seconds and the rest 3 to 15 seconds apart. Each frame has an `id:` such as `1-0001`, increasing by one, and JSON `data:` with `clockSeconds` that never decreases. The JSON `type` is one of `Pass`, `Shot`, `Tackle`, `PossessionChange`, `Pressure`, and `x` and `y` are inside 0 to 105 and 0 to 68. A frame with `clockSeconds` `c` arrives about `c` seconds after the command started. Press Ctrl+C to stop. The dashboard shows the request as still open until then, and the structured logs show no errors.
- **E2E-003**: Precondition: `webfrontend` is Running. Action: open the `webfrontend` URL from the dashboard, click Match in the navigation, confirm the heading shows "Harbor City FC vs Northgate Rovers", click Start match. Expected: the button becomes disabled, the clock starts counting from the first event, and new event lines appear at the top every few seconds, each with a clock time, event type, a player name from the lineups, and coordinates. Clock values never decrease. The dashboard traces show `webfrontend` calling `GET /matches/1/events/stream` on `apiservice` with the call still open, and no resilience timeout error appears in `webfrontend` logs after more than 30 seconds.
- **E2E-004**: Negative path. Precondition: `apiservice` is Running. Action: `curl -i {apiservice-url}/matches/0/events/stream`. Expected: HTTP 400 with no `text/event-stream` content type and no stack trace in the body.
- **E2E-005**: Negative path. Precondition: `apiservice` is Running. Action: `curl -i {apiservice-url}/matches/999/events/stream`. Expected: HTTP 404 with no event stream.
- **E2E-006**: Negative path. Precondition: `webfrontend` page `/match` is loaded and `apiservice` is stopped from the dashboard Resources page. Action: click Start match. Expected: the page shows "Could not start the match. Try again." in red, the button becomes enabled again, and no event lines are added. After restarting `apiservice` from the dashboard and clicking Start match again, events arrive as in E2E-003.
- **Stop**: Run `aspire stop` when testing is complete

## 5. Alternatives

- **ALT-001**: A shared match session started once by `POST /matches/{matchId}/start` and joined by many consumers. Rejected for US-01 because each connection replaying the same deterministic match meets the story. It becomes necessary for US-08 (overlay feed to a producer), where several consumers must see the same event at the same time, and it should be planned with that story
- **ALT-002**: WebSockets or SignalR for the event stream. Rejected because the flow is one-way, and SSE is built into ASP.NET Core 10 minimal APIs and works through `curl` for debugging
- **ALT-003**: Persist events in Azure Cosmos DB or Azure SQL and replay them. Rejected for US-01 because it adds provisioning, secrets, and a dependency before the event shape is validated (CON-003)
- **ALT-004**: Use an AI service to write realistic events. Rejected because US-01 needs reproducible data (REQ-005) and tests that do not depend on a model. AI is used by later stories for explanations
- **ALT-005**: Poll `GET /matches/{matchId}/events?after={id}` from the Web page. Rejected because polling adds latency and load that works against the 2-second overlay target of US-08

## 6. Dependencies

- **DEP-001**: New project `GoalGetter/GoalGetter.Contracts`, referenced by `GoalGetter.ApiService`, `GoalGetter.Web`, and `GoalGetter.Tests`
- **DEP-002**: New test package `Microsoft.Extensions.TimeProvider.Testing` in `GoalGetter/GoalGetter.Tests/GoalGetter.Tests.csproj`, version resolved by `dotnet add package`
- **DEP-003**: `System.Net.ServerSentEvents` (`SseItem<T>`, `SseParser`), part of the .NET 10 shared framework for the ApiService, the Web frontend, and the test project
- **DEP-004**: `Microsoft.Extensions.Http.Resilience` for `RemoveAllResilienceHandlers`, already available transitively through `GoalGetter.ServiceDefaults`
- **DEP-005**: Existing Aspire resource names `apiservice` and `webfrontend` in `GoalGetter/GoalGetter.AppHost/AppHost.cs`, used by the tests and the manual flow
- **DEP-006**: Existing test packages `Aspire.Hosting.Testing` and `xunit.v3.mtp-off` in `GoalGetter/GoalGetter.Tests/GoalGetter.Tests.csproj`

## 7. Files

- **FILE-001**: `GoalGetter/GoalGetter.Contracts/GoalGetter.Contracts.csproj` | CREATE | Class library project for `net10.0` | Phase 1 / TASK-001
- **FILE-002**: `GoalGetter/GoalGetter.Contracts/Matches/MatchContracts.cs` | CREATE | `Pitch`, `EventType`, `Player`, `Team`, `Fixture`, `MatchEvent` | Phase 1 / TASK-001
- **FILE-003**: `GoalGetter/GoalGetter.sln` | MODIFY | Adds the `GoalGetter.Contracts` project | Phase 1 / TASK-001
- **FILE-004**: `GoalGetter/GoalGetter.ApiService/GoalGetter.ApiService.csproj` | MODIFY | Project reference to `GoalGetter.Contracts` | Phase 1 / TASK-001
- **FILE-005**: `GoalGetter/GoalGetter.Tests/GoalGetter.Tests.csproj` | MODIFY | Project references to Contracts, ApiService, and Web; `Microsoft.Extensions.TimeProvider.Testing` package; `GoalGetter.Contracts.Matches` using | Phase 1 / TASK-001, Phase 3 / TASK-004, and Phase 5 / TASK-008
- **FILE-006**: `GoalGetter/GoalGetter.Tests/ContractsTests.cs` | CREATE | `ContractsTests` with TEST-001 and TEST-002 | Phase 1 / TASK-001
- **FILE-007**: `GoalGetter/GoalGetter.ApiService/Matches/FixtureCatalog.cs` | CREATE | `IFixtureCatalog`, `InMemoryFixtureCatalog` | Phase 1 / TASK-002
- **FILE-008**: `GoalGetter/GoalGetter.Tests/FixtureCatalogTests.cs` | CREATE | `FixtureCatalogTests` with TEST-004 to TEST-006 | Phase 1 / TASK-002
- **FILE-009**: `GoalGetter/GoalGetter.ApiService/Matches/SyntheticMatchGenerator.cs` | CREATE | `ISyntheticMatchGenerator`, `SyntheticMatchGenerator` | Phase 2 / TASK-003
- **FILE-010**: `GoalGetter/GoalGetter.Tests/SyntheticMatchGeneratorTests.cs` | CREATE | `SyntheticMatchGeneratorTests` with TEST-007 to TEST-013 | Phase 2 / TASK-003
- **FILE-011**: `GoalGetter/GoalGetter.ApiService/Matches/MatchPlaybackOptions.cs` | CREATE | `MatchPlaybackOptions` | Phase 3 / TASK-004
- **FILE-012**: `GoalGetter/GoalGetter.ApiService/Matches/MatchPlayback.cs` | CREATE | `IMatchPlayback`, `MatchPlayback` | Phase 3 / TASK-004
- **FILE-013**: `GoalGetter/GoalGetter.Tests/MatchPlaybackTests.cs` | CREATE | `MatchPlaybackTests` with TEST-014 to TEST-018 | Phase 3 / TASK-004
- **FILE-014**: `GoalGetter/GoalGetter.ApiService/Matches/MatchServiceCollectionExtensions.cs` | CREATE | `AddMatchServices` | Phase 3 / TASK-005
- **FILE-015**: `GoalGetter/GoalGetter.ApiService/Program.cs` | MODIFY | `AddMatchServices` call and `MapMatchEndpoints` call, and two `using` lines | Phase 3 / TASK-005 and Phase 4 / TASK-006
- **FILE-016**: `GoalGetter/GoalGetter.Tests/MatchServiceRegistrationTests.cs` | CREATE | `MatchServiceRegistrationTests` with TEST-019 to TEST-021 | Phase 3 / TASK-005
- **FILE-017**: `GoalGetter/GoalGetter.ApiService/Endpoints/MatchEndpoints.cs` | CREATE | `MatchEndpoints.MapMatchEndpoints`, `ToSseItems` | Phase 4 / TASK-006
- **FILE-018**: `GoalGetter/GoalGetter.ApiService/GoalGetter.ApiService.http` | MODIFY | Fixture and stream requests | Phase 4 / TASK-006
- **FILE-019**: `GoalGetter/GoalGetter.AppHost/AppHost.cs` | MODIFY | Optional `MatchPlayback__SpeedFactor` environment pass-through | Phase 4 / TASK-007
- **FILE-020**: `GoalGetter/GoalGetter.Tests/TestAppHost.cs` | CREATE | `TestAppHost.StartAsync` | Phase 4 / TASK-007
- **FILE-021**: `GoalGetter/GoalGetter.Tests/WebTests.cs` | MODIFY | Use `TestAppHost`; add TEST-033 and TEST-034 | Phase 4 / TASK-007, Phase 5 / TASK-008 and TASK-009
- **FILE-022**: `GoalGetter/GoalGetter.Tests/MatchEndpointsTests.cs` | CREATE | `MatchEndpointsTests` with TEST-023 to TEST-028 | Phase 4 / TASK-007
- **FILE-023**: `GoalGetter/GoalGetter.Web/GoalGetter.Web.csproj` | MODIFY | Project reference to `GoalGetter.Contracts` | Phase 5 / TASK-008
- **FILE-024**: `GoalGetter/GoalGetter.Web/MatchApiClient.cs` | CREATE | `MatchApiClient.GetFixtureAsync`, `MatchApiClient.StreamEventsAsync` | Phase 5 / TASK-008
- **FILE-025**: `GoalGetter/GoalGetter.Web/Program.cs` | MODIFY | `ApiServiceUri` constant; `MatchApiClient` registration | Phase 5 / TASK-008
- **FILE-026**: `GoalGetter/GoalGetter.Tests/MatchApiClientTests.cs` | CREATE | `MatchApiClientTests` with TEST-030 to TEST-032 | Phase 5 / TASK-008
- **FILE-027**: `GoalGetter/GoalGetter.Web/Components/Pages/Match.razor` | CREATE | `/match` page | Phase 5 / TASK-009
- **FILE-028**: `GoalGetter/GoalGetter.Web/Components/Layout/NavMenu.razor` | MODIFY | Match navigation link | Phase 5 / TASK-009

## 8. Testing Strategy

| Test Type | Project | Framework | Command |
|-----------|---------|-----------|---------|
| Build | `GoalGetter/GoalGetter.sln` | .NET SDK | `dotnet build GoalGetter/GoalGetter.sln` |
| Unit (contracts, catalog, generator, playback, registration) | `GoalGetter/GoalGetter.Tests` | xUnit v3 with `FakeTimeProvider` | `dotnet test GoalGetter/GoalGetter.Tests --filter "FullyQualifiedName~ContractsTests\|FullyQualifiedName~FixtureCatalogTests\|FullyQualifiedName~SyntheticMatchGeneratorTests\|FullyQualifiedName~MatchPlaybackTests\|FullyQualifiedName~MatchServiceRegistrationTests"` |
| Unit (Web client with stub handler) | `GoalGetter/GoalGetter.Tests` | xUnit v3 | `dotnet test GoalGetter/GoalGetter.Tests --filter "FullyQualifiedName~MatchApiClientTests"` |
| Integration | `GoalGetter/GoalGetter.Tests` | xUnit v3 with Aspire testing | `dotnet test GoalGetter/GoalGetter.Tests --filter "FullyQualifiedName~MatchEndpointsTests\|FullyQualifiedName~WebTests"` |
| Regression | `GoalGetter/GoalGetter.Tests` | xUnit v3 with Aspire testing | `dotnet test GoalGetter/GoalGetter.Tests` |

## 9. Risks & Assumptions

- **RISK-001**: `GoalGetter/GoalGetter.ApiService/Program.cs` still holds the weather sample (inline `Random.Shared`, a `summaries` array in top-level code, a magic number in `TemperatureF`). This plan only adds two lines to that file, so the findings are not changed here. They are a candidate for a separate refactor plan such as `.github/skills/create-implementation-plan/examples/sample-refactor.md`
- **RISK-002**: Any other `HttpClient` that streams for longer than the standard resilience timeouts will be cut off. Only `MatchApiClient` removes the handler. Later streaming clients, such as the overlay consumer in US-08, must do the same
- **RISK-003**: The `Match.razor` page holds one open SSE connection per browser circuit. A shared session (ALT-001) is needed before many viewers watch at once
- **RISK-004**: The page uses match id 1 as a constant. A match picker is outside US-01
- **RISK-005**: `GoalGetter.Tests` references the ApiService and Web executable projects. If that causes build warnings or duplicate entry point errors (the test project must stay `Exe` for xUnit v3), move the unit-tested types into a class library, or replace the references with `WebApplicationFactory`-style tests
- **ASSUMPTION-001**: `DistributedApplicationTestingBuilder.CreateAsync<T>(string[] args, CancellationToken)` passes `args` such as `MatchPlayback:SpeedFactor=600` into the AppHost configuration. Verify with TEST-026. If the value does not reach `apiservice`, set the variable through the testing builder's `apiservice` resource instead, using the same `MatchPlayback__SpeedFactor` name
- **ASSUMPTION-002**: The synthetic event fields (id, match id, clock, type, team, player, X, Y) are provisional, because the story map's open question on event fields and the rendering partner's overlay contract is still open. US-03 (pass end coordinates) and US-04 (shot speed) will extend `MatchEvent` with optional fields
- **ASSUMPTION-003**: A continuous 90-minute match with no stoppage time and no half-time break is enough for US-01. Halves and stoppage time are not required by the story
- **ASSUMPTION-004**: Players are chosen at random from the lineup of the acting team, not by position. Position-aware choice (for example the goalkeeper rarely shooting) is a later realism improvement and is not required by US-01's acceptance criteria
- **ASSUMPTION-005**: The player and club names are fictional. US-11 requires that names stay correct in Spanish, and this plan keeps names as data in the fixture, not in text templates

## 10. Related Specifications / Further Reading

- [GoalGetter MVP: User Story Map, US-01](../docs/user-story-map.md)
- [Minimal API responses, IResult return values](https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis/responses?view=aspnetcore-10.0#iresult-return-values)
- [What's new in ASP.NET Core 10, minimal APIs](https://learn.microsoft.com/aspnet/core/release-notes/aspnetcore-10.0?view=aspnetcore-10.0#minimal-apis)
- [What's new in .NET 9 libraries, System.Net.ServerSentEvents](https://learn.microsoft.com/dotnet/core/whats-new/dotnet-9/libraries#networking)
- [Test with FakeTimeProvider](https://learn.microsoft.com/dotnet/core/extensions/timeprovider-testing#test-delayed-operations)
- [Options validation](https://learn.microsoft.com/dotnet/core/extensions/options#options-validation)
- [Remove standard resilience handlers](https://learn.microsoft.com/dotnet/core/resilience/http-resilience#remove-standard-resilience-handlers)
- [Enums as strings in System.Text.Json](https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/customize-properties#enums-as-strings)
- [Random constructor with seed](https://learn.microsoft.com/dotnet/api/system.random.-ctor?view=net-10.0)
- [Aspire integration testing](https://learn.microsoft.com/dotnet/aspire/testing/write-your-first-test)
- [Blazor StateHasChanged and rendering](https://learn.microsoft.com/aspnet/core/blazor/components/rendering?view=aspnetcore-10.0#statehaschanged)
