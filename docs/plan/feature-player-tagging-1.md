---
goal: Implement US-02, tag every streamed match event with the player's name and team
version: 1.0
date_created: 2026-10-12
last_updated: 2026-10-12
owner: GoalGetter team
status: 'Planned'
tags: [feature, api, contracts, personalization, testing]
---

# Introduction

![Status: Planned](https://img.shields.io/badge/status-Planned-blue)

**Summary for reviewers**: The ApiService resolves each event's player id against the fixture lineups and streams a `TaggedMatchEvent` that carries the player name and the team name. The `/match` page then shows "who did what" for Harbor City FC and Northgate Rovers without doing its own lookup. Success is recognized when the tests in both phases pass and the manual flow in Section 4 shows a player and team name on every event.

This plan implements user story US-02 from `docs/user-story-map.md`: given an incoming event with a player ID, when the event is processed, then it shows the player's name and team. It builds on `docs/plan/feature-match-playback-1.md` (US-01), which already streams `MatchEvent` over `GET /matches/{matchId}/events/stream`.

Definitions used in this plan:
- **Tagging**: the processing step that turns a `MatchEvent` (ids only) into a `TaggedMatchEvent` (ids plus player name and team name).
- **Lineup**: the 11 players of one team in `Fixture.Home.Lineup` or `Fixture.Away.Lineup`.
- **SSE**: Server-Sent Events, a one-way HTTP stream with content type `text/event-stream`.

Current state this plan changes: `GoalGetter.Web/Components/Pages/Match.razor` builds a `playerNames` dictionary from the fixture and shows only the player name. The stream carries only `PlayerId` and `TeamId`, and no team name is shown anywhere.

## 1. Requirements & Constraints

- **REQ-001**: Every event on `GET /matches/{matchId}/events/stream` carries `teamName` and `playerName`, resolved from the fixture lineups, in addition to every field `MatchEvent` has today
- **REQ-002**: A player is resolved by `PlayerId` inside the lineup of the team named by the event's `TeamId`. A player id found only in the other team's lineup is not a match
- **REQ-003**: An event whose team or player cannot be resolved is still delivered. It carries `Unknown team` or `Unknown player` for the missing part, and the server logs one warning per such event
- **REQ-004**: Every event generated for match 1 resolves to a real player and team. No `Unknown player` or `Unknown team` appears in its stream
- **REQ-005**: The SSE event name (`matchEvent`), the SSE event id, the event order, and the live pace are unchanged
- **REQ-006**: A reader that deserializes the stream payload as `MatchEvent` keeps working, because the tagged payload only adds properties
- **REQ-007**: The `/match` page shows the player name and the team name for each event, read from the stream and not looked up in the browser
- **SEC-001**: Names are written to the page through Razor expressions, which HTML-encode them. No raw markup rendering is used
- **SEC-002**: The warning log contains event id, player id, and team id only. It does not contain request data or exception text
- **CON-001**: Target `net10.0`, ASP.NET Core minimal APIs, and Aspire as in `GoalGetter/GoalGetter.sln`
- **CON-002**: No new project and no new package
- **CON-003**: `Fixture`, `Team`, `Player`, and `MatchEvent` in `GoalGetter/GoalGetter.Contracts/Matches/MatchContracts.cs` do not change. The generator and playback are not touched
- **GUD-001**: Keep `Program.cs` unchanged. Registration goes through `AddMatchServices`
- **GUD-002**: Use source-generated logging (`LoggerMessage`) for the warning
- **PAT-001**: Hide tagging behind an interface (`IPlayerTagger`) registered in dependency injection, matching `IFixtureCatalog`, `ISyntheticMatchGenerator`, and `IMatchPlayback`

## 2. Implementation Steps

### Implementation Phase 1 (✅ COMPLETED) - Tagged Event Contract and Player Tagger

- **GOAL-001**: Provide `TaggedMatchEvent` and an injectable `IPlayerTagger` that resolves player and team names from a fixture
- **Completion criteria**: `dotnet test GoalGetter/GoalGetter.Tests --filter "FullyQualifiedName~ContractsTests|FullyQualifiedName~PlayerTaggerTests|FullyQualifiedName~MatchServiceRegistrationTests"` exits with 0 failures, and `dotnet build GoalGetter/GoalGetter.sln` exits with 0 errors

| Task | Description | Files | Completed | Date |
|------|-------------|-------|-----------|------|
| TASK-001 | Add the `TaggedMatchEvent` record to the shared contracts. It repeats the `MatchEvent` fields and adds `TeamName` and `PlayerName`, so `MatchEvent` readers still work | FILE-001, FILE-002 | Yes | 2026-10-12 |
| TASK-002 | Create `IPlayerTagger` and `PlayerTagger`, with a source-generated warning log, and register it in `AddMatchServices` | FILE-003, FILE-004, FILE-005, FILE-006 | Yes | 2026-10-12 |

#### TASK-001 Code

```csharp
// GoalGetter/GoalGetter.Contracts/Matches/MatchContracts.cs, added below MatchEvent
public sealed record TaggedMatchEvent(
    string Id, int MatchId, int ClockSeconds, EventType Type,
    string TeamId, string TeamName, int PlayerId, string PlayerName, double X, double Y);
```

Source: https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/missing-members

By default, `System.Text.Json` ignores JSON properties that the target type does not have. A client that reads the tagged payload as `MatchEvent` therefore still works (REQ-006). The record repeats the `MatchEvent` members and does not inherit from it, because `MatchEvent` is `sealed` and CON-003 keeps it unchanged.

#### TASK-001 Tests

- **TEST-001**: `TaggedMatchEvent_SerializesNamesAndTypeAsString` in `GoalGetter/GoalGetter.Tests/ContractsTests.cs`. Serializes a `TaggedMatchEvent` with `JsonSerializer.Serialize(value, JsonSerializerOptions.Web)`. Asserts the JSON contains `"type":"Pass"`, `"playerName":"Tomas Brandt"`, and `"teamName":"Harbor City FC"`.
- **TEST-002**: `TaggedMatchEvent_PayloadDeserializesAsMatchEvent` in `GoalGetter/GoalGetter.Tests/ContractsTests.cs`. Serializes a `TaggedMatchEvent`, deserializes the JSON as `MatchEvent` with `JsonSerializerOptions.Web`. Asserts `Id`, `MatchId`, `ClockSeconds`, `Type`, `TeamId`, `PlayerId`, `X`, and `Y` equal the source values (REQ-006).

#### TASK-001 Boy Scout

No boy scout findings

#### TASK-002 Code

```csharp
// GoalGetter/GoalGetter.ApiService/Matches/PlayerTagger.cs
using GoalGetter.Contracts.Matches;

namespace GoalGetter.ApiService.Matches;

public interface IPlayerTagger
{
    TaggedMatchEvent Tag(Fixture fixture, MatchEvent matchEvent);
}

public sealed partial class PlayerTagger(ILogger<PlayerTagger> logger) : IPlayerTagger
{
    public const string UnknownPlayerName = "Unknown player";
    public const string UnknownTeamName = "Unknown team";

    public TaggedMatchEvent Tag(Fixture fixture, MatchEvent matchEvent)
    {
        var team = new[] { fixture.Home, fixture.Away }.FirstOrDefault(t => t.Id == matchEvent.TeamId);
        var player = team?.Lineup.FirstOrDefault(p => p.Id == matchEvent.PlayerId);
        if (player is null)
        {
            LogPlayerNotInLineup(logger, matchEvent.Id, matchEvent.PlayerId, matchEvent.TeamId);
        }

        return new TaggedMatchEvent(
            matchEvent.Id, matchEvent.MatchId, matchEvent.ClockSeconds, matchEvent.Type,
            matchEvent.TeamId, team?.Name ?? UnknownTeamName,
            matchEvent.PlayerId, player?.Name ?? UnknownPlayerName,
            matchEvent.X, matchEvent.Y);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning,
        Message = "Event {MatchEventId} names player {PlayerId} who is not in the lineup of team {TeamId}")]
    private static partial void LogPlayerNotInLineup(ILogger logger, string matchEventId, int playerId, string teamId);
}
```

Source: https://learn.microsoft.com/dotnet/core/extensions/logging/high-performance-logging#define-logger-messages-with-source-generation

The `LoggerMessage` method follows the source sample, adapted to a `private static partial` method with the `ILogger` passed in. The player is searched only in the lineup of the team named by the event (REQ-002). When the team is unknown, `team` is null, so the player is also unknown and both fallbacks apply (REQ-003). Lineups hold 11 players, so a linear search is enough.

```csharp
// GoalGetter/GoalGetter.ApiService/Matches/MatchServiceCollectionExtensions.cs, with the other singletons
services.AddSingleton<IPlayerTagger, PlayerTagger>();
```

#### TASK-002 Tests

All tagger tests are in `GoalGetter/GoalGetter.Tests/PlayerTaggerTests.cs`. They build `new PlayerTagger(NullLogger<PlayerTagger>.Instance)` and take the fixture from `new InMemoryFixtureCatalog().TryGet(1, out var fixture)`. Home player ids are 1 to 11 (player 1 is Tomas Brandt, `HCF`). Away player ids are 12 to 22 (player 12 is Pavel Kral, `NGR`).

- **TEST-003**: `Tag_HomePlayer_ReturnsPlayerNameAndTeamName`. Tags an event with `TeamId` `HCF` and `PlayerId` 1. Asserts `PlayerName` is `Tomas Brandt` and `TeamName` is `Harbor City FC`.
- **TEST-004**: `Tag_AwayPlayer_ReturnsPlayerNameAndTeamName`. Tags an event with `TeamId` `NGR` and `PlayerId` 12. Asserts `PlayerName` is `Pavel Kral` and `TeamName` is `Northgate Rovers`.
- **TEST-005**: `Tag_KeepsAllSourceEventFields`. Tags an event with a non-default `Id`, `ClockSeconds`, `Type`, `X`, and `Y`. Asserts each value is copied unchanged.
- **TEST-006**: `Tag_PlayerIdNotInAnyLineup_ReturnsUnknownPlayerWithTeamName`. Tags an event with `TeamId` `HCF` and `PlayerId` 999. Asserts `PlayerName` is `PlayerTagger.UnknownPlayerName` and `TeamName` is `Harbor City FC`.
- **TEST-007**: `Tag_PlayerFromOtherTeam_ReturnsUnknownPlayer`. Tags an event with `TeamId` `HCF` and `PlayerId` 12. Asserts `PlayerName` is `PlayerTagger.UnknownPlayerName`.
- **TEST-008**: `Tag_UnknownTeam_ReturnsUnknownTeamAndUnknownPlayer`. Tags an event with `TeamId` `XXX`. Asserts both fallback names.
- **TEST-009**: `Tag_AllGeneratedEventsOfMatch1_ResolveToKnownPlayerAndTeam`. Runs `new SyntheticMatchGenerator().Generate(fixture)` and tags every event. Asserts no result has `PlayerTagger.UnknownPlayerName` or `PlayerTagger.UnknownTeamName`, and each `PlayerName` belongs to the lineup of the event's team (REQ-004).
- **TEST-010**: `AddMatchServices_ResolvesPlayerTagger` in `GoalGetter/GoalGetter.Tests/MatchServiceRegistrationTests.cs`. Builds a `ServiceCollection` with `AddLogging()`, an empty in-memory `IConfiguration`, and `AddMatchServices`. Asserts `IPlayerTagger` resolves to a `PlayerTagger`.

#### TASK-002 Boy Scout

No boy scout findings

### Implementation Phase 2 - Tagged Stream and Match Page

- **GOAL-002**: Stream `TaggedMatchEvent` from the API and show the player and team name on the `/match` page
- **Completion criteria**: `dotnet test GoalGetter/GoalGetter.Tests` exits with 0 failures, including `MatchEndpointsTests`, `MatchApiClientTests`, and `WebTests`

| Task | Description | Files | Completed | Date |
|------|-------------|-------|-----------|------|
| TASK-003 | Tag each event in `ToSseItems` and change the SSE payload type from `MatchEvent` to `TaggedMatchEvent`, keeping the event name and event id | FILE-007, FILE-008 | | |
| TASK-004 | Change `MatchApiClient.StreamEventsAsync` to yield `TaggedMatchEvent`, and show player name and team name in `Match.razor` without a client-side lookup | FILE-009, FILE-010, FILE-011 | | |

#### TASK-003 Code

```csharp
// GoalGetter/GoalGetter.ApiService/Endpoints/MatchEndpoints.cs, stream route and iterator after the change
matches.MapGet("/{matchId:int}/events/stream",
    (int matchId, IFixtureCatalog catalog, IMatchPlayback playback, IPlayerTagger tagger, CancellationToken cancellationToken) =>
        matchId <= 0 ? Results.BadRequest()
        : !catalog.TryGet(matchId, out var fixture) ? Results.NotFound()
        : TypedResults.ServerSentEvents(ToSseItems(playback, tagger, fixture, cancellationToken)));

private static async IAsyncEnumerable<SseItem<TaggedMatchEvent>> ToSseItems(
    IMatchPlayback playback, IPlayerTagger tagger, Fixture fixture,
    [EnumeratorCancellation] CancellationToken cancellationToken)
{
    await foreach (var matchEvent in playback.PlayAsync(fixture, cancellationToken))
    {
        yield return new SseItem<TaggedMatchEvent>(tagger.Tag(fixture, matchEvent), MatchEventName)
        {
            EventId = matchEvent.Id,
        };
    }
}
```

Source: https://learn.microsoft.com/aspnet/core/release-notes/aspnetcore-10.0?view=aspnetcore-10.0#minimal-apis

`IPlayerTagger` is resolved from dependency injection as a route handler parameter. The `SseItem<T>` iterator with `TypedResults.ServerSentEvents` is the pattern already used in this method, with `T` changed to `TaggedMatchEvent`. `MatchEventName` and `EventId` are unchanged (REQ-005). Tagging runs per event inside the iterator, so the live pace of `MatchPlayback` is unaffected.

#### TASK-003 Tests

All tests are in `GoalGetter/GoalGetter.Tests/MatchEndpointsTests.cs` and start the AppHost through `TestAppHost.StartAsync` with `MatchPlayback:SpeedFactor=600`.

- **TEST-011**: `StreamEvents_Match1_DeliversTenEventsInClockOrder` (existing test, modified). The SSE parser deserializes `TaggedMatchEvent` instead of `MatchEvent`. All existing assertions stay: status 200, content type `text/event-stream`, event name `matchEvent`, non-decreasing `ClockSeconds`, ids `1-0001` to `1-0010`, and `EventId` equal to the data id.
- **TEST-012**: `StreamEvents_Match1_TagsEveryEventWithPlayerAndTeamName`. Reads 10 SSE items and calls `GET /matches/1/fixture`. Asserts each item's `PlayerName` equals the lineup name for its `PlayerId`, and its `TeamName` equals the team name for its `TeamId`.
- **TEST-013**: `StreamEvents_Match1_PayloadStillDeserializesAsMatchEvent`. Reads the first SSE data payload as raw JSON text and deserializes it as `MatchEvent` with `JsonSerializerOptions.Web`. Asserts `Id` is `1-0001` (REQ-006).
- **TEST-014**: `StreamEvents_UnknownMatch_ReturnsNotFoundWithoutStream` and `StreamEvents_ZeroId_ReturnsBadRequest` (existing tests). Run unchanged. Assert 404 and 400, which proves tagging did not change the validation path.

#### TASK-003 Boy Scout

No boy scout findings

#### TASK-004 Code

```csharp
// GoalGetter/GoalGetter.Web/MatchApiClient.cs, StreamEventsAsync after the change
public async IAsyncEnumerable<TaggedMatchEvent> StreamEventsAsync(
    int matchId, [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    using var request = new HttpRequestMessage(HttpMethod.Get, $"/matches/{matchId}/events/stream");
    using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    response.EnsureSuccessStatusCode();

    await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
    var parser = SseParser.Create(stream, (_, data) => JsonSerializer.Deserialize<TaggedMatchEvent>(data, JsonSerializerOptions.Web)!);

    await foreach (var item in parser.EnumerateAsync(cancellationToken))
    {
        yield return item.Data;
    }
}
```

Source: https://learn.microsoft.com/dotnet/core/whats-new/dotnet-9/libraries#networking

```razor
@* GoalGetter/GoalGetter.Web/Components/Pages/Match.razor, event list and code block after the change *@
<ul>
    @foreach (var matchEvent in events)
    {
        <li>@FormatClock(matchEvent.ClockSeconds) @matchEvent.Type, @matchEvent.PlayerName, @matchEvent.TeamName (@matchEvent.X.ToString("0"), @matchEvent.Y.ToString("0"))</li>
    }
</ul>

@code {
    private readonly List<TaggedMatchEvent> events = [];
    // The playerNames field, the dictionary build in OnInitializedAsync, and the PlayerName method are removed.
}
```

Source: https://learn.microsoft.com/aspnet/core/blazor/components/rendering?view=aspnetcore-10.0#statehaschanged

The page loop (`InvokeAsync(StateHasChanged)` after each event) is unchanged. `@matchEvent.PlayerName` and `@matchEvent.TeamName` are Razor expressions, so the values are HTML-encoded (SEC-001). A row reads like `12:05 Pass, Tomas Brandt, Harbor City FC (50, 34)`.

#### TASK-004 Tests

- **TEST-015**: `StreamEventsAsync_ParsesSseBody_YieldsEventsInOrder` in `GoalGetter/GoalGetter.Tests/MatchApiClientTests.cs` (existing test, modified). `CreateEvent` builds `TaggedMatchEvent`, and `ReadEventsAsync` returns `List<TaggedMatchEvent>`. Asserts the three received values equal the three sent values in order.
- **TEST-016**: `StreamEventsAsync_ParsesPlayerAndTeamNames` in `GoalGetter/GoalGetter.Tests/MatchApiClientTests.cs`. The stub returns one frame with `playerName` `Pavel Kral` and `teamName` `Northgate Rovers`. Asserts both values are on the received event.
- **TEST-017**: Build check. `dotnet build GoalGetter/GoalGetter.sln` exits with 0 errors and 0 warnings in `GoalGetter.Web`. Confirms the Razor component compiles after the lookup removal.
- **TEST-018**: `GetMatchPageReturnsOkStatusCodeWithStartButton` in `GoalGetter/GoalGetter.Tests/WebTests.cs` (existing test). Runs unchanged. Asserts HTTP 200 and a body with `Start match` and `Harbor City FC`.

The page's behavior after the Start match click is interactive Blazor and is covered by E2E-002.

#### TASK-004 Boy Scout

- **BSC-001**: `GoalGetter/GoalGetter.Web/Components/Pages/Match.razor` | DRY / Code smell | The page rebuilds a player-id to name dictionary from the fixture and repeats the `"Unknown player"` literal that `PlayerTagger.UnknownPlayerName` now owns (duplicated lookup logic, feature envy on `Fixture`) | Delete `playerNames`, its build in `OnInitializedAsync`, and `PlayerName(...)`, and read `PlayerName` from the event. Covered by TEST-009, TEST-016, TEST-017, and E2E-002

## 3. Boy Scout Summary

| Item | File | Category | Phase / Task | Status |
|------|------|----------|--------------|--------|
| BSC-001 | FILE-011 | DRY / Code smell | Phase 2 / TASK-004 | Open |

## 4. Manual End-to-End Test

- **Prerequisites**: .NET 10 SDK, Aspire CLI, a modern browser, and `curl`. No credentials are needed. Both phases are complete and `dotnet test GoalGetter/GoalGetter.Tests` passes
- **Start**: Run `aspire start` from `GoalGetter/`, then open the Aspire dashboard and confirm `apiservice` and `webfrontend` are Running and healthy
- **E2E-001**: Precondition: `apiservice` is Running at normal pace. Action: `curl -N {apiservice-url}/matches/1/events/stream` (use the `apiservice` endpoint URL from the dashboard, or the host address in `GoalGetter/GoalGetter.ApiService/GoalGetter.ApiService.http`), and watch for 30 seconds. Then call `GET {apiservice-url}/matches/1/fixture`. Expected: every `data:` JSON has `playerId`, `playerName`, `teamId`, and `teamName`. For each frame, `playerName` matches the lineup entry with that `playerId`, and `teamName` is `Harbor City FC` for `HCF` and `Northgate Rovers` for `NGR`. No frame shows `Unknown player` or `Unknown team`. The `id:` and `event: matchEvent` lines are as before. Press Ctrl+C to stop. The dashboard structured logs for `apiservice` show no warnings from `PlayerTagger`.
- **E2E-002**: Precondition: `webfrontend` is Running. Action: open the `webfrontend` URL from the dashboard, click Match, click Start match. Expected: event lines appear at the top every few seconds, each in the form `mm:ss Type, Player Name, Team Name (x, y)`, for example `00:12 Pass, Tomas Brandt, Harbor City FC (52, 31)`. Names come from the lineups, both teams appear over time, and the clock never decreases.
- **E2E-003**: Negative path. Precondition: `apiservice` is Running. Action: `curl -i {apiservice-url}/matches/999/events/stream`. Expected: HTTP 404, no `text/event-stream` content type, and no tagged data in the body.
- **E2E-004**: Negative path. Precondition: `/match` is loaded and `apiservice` is stopped from the dashboard Resources page. Action: click Start match. Expected: the page shows "Could not start the match. Try again." in red, the button becomes enabled again, and no event lines are added. After restarting `apiservice` from the dashboard and clicking Start match again, events arrive with names as in E2E-002.
- **Stop**: Run `aspire stop` when testing is complete

## 5. Alternatives

- **ALT-001**: Resolve names in the browser from the fixture (current behavior). Rejected because every consumer, including the overlay feed of US-08 and the narratives of US-05, would repeat the lookup and the unknown-player rule, and the team name is not on the stream today
- **ALT-002**: Add `PlayerName` and `TeamName` as optional fields on `MatchEvent`. Rejected because the generator would then carry display data, and CON-003 keeps the ingest contract limited to ids
- **ALT-003**: Wrap the event as `{ event, player, team }`. Rejected because it breaks every reader of the current payload, while the flat record only adds properties (REQ-006)
- **ALT-004**: Drop events whose player cannot be resolved. Rejected because a silent drop hides a feed problem and breaks the sequential ids. The event is delivered with a visible fallback and a logged warning instead (REQ-003)
- **ALT-005**: Tag inside `MatchPlayback`. Rejected because playback owns timing only, and tagging belongs to the processing step that later stories (US-03, US-04) extend

## 6. Dependencies

- **DEP-001**: Existing project `GoalGetter/GoalGetter.Contracts`, referenced by `GoalGetter.ApiService`, `GoalGetter.Web`, and `GoalGetter.Tests`
- **DEP-002**: Existing US-01 types `IFixtureCatalog`, `ISyntheticMatchGenerator`, `IMatchPlayback`, `MatchEndpoints`, and `MatchApiClient`, as built by `docs/plan/feature-match-playback-1.md`
- **DEP-003**: `Microsoft.Extensions.Logging` (`ILogger<T>`, `LoggerMessageAttribute`), part of the ASP.NET Core shared framework. `NullLogger<T>` for the tests comes from `Microsoft.Extensions.Logging.Abstractions`
- **DEP-004**: `GoalGetter/GoalGetter.Tests/TestAppHost.cs` and the AppHost resource names `apiservice` and `webfrontend` in `GoalGetter/GoalGetter.AppHost/AppHost.cs`

## 7. Files

- **FILE-001**: `GoalGetter/GoalGetter.Contracts/Matches/MatchContracts.cs` | MODIFY | Adds `TaggedMatchEvent` | Phase 1 / TASK-001
- **FILE-002**: `GoalGetter/GoalGetter.Tests/ContractsTests.cs` | MODIFY | Adds TEST-001 and TEST-002 | Phase 1 / TASK-001
- **FILE-003**: `GoalGetter/GoalGetter.ApiService/Matches/PlayerTagger.cs` | CREATE | `IPlayerTagger`, `PlayerTagger`, `LogPlayerNotInLineup` | Phase 1 / TASK-002
- **FILE-004**: `GoalGetter/GoalGetter.Tests/PlayerTaggerTests.cs` | CREATE | `PlayerTaggerTests` with TEST-003 to TEST-009 | Phase 1 / TASK-002
- **FILE-005**: `GoalGetter/GoalGetter.ApiService/Matches/MatchServiceCollectionExtensions.cs` | MODIFY | `AddMatchServices` registers `IPlayerTagger` | Phase 1 / TASK-002
- **FILE-006**: `GoalGetter/GoalGetter.Tests/MatchServiceRegistrationTests.cs` | MODIFY | Adds TEST-010 | Phase 1 / TASK-002
- **FILE-007**: `GoalGetter/GoalGetter.ApiService/Endpoints/MatchEndpoints.cs` | MODIFY | Stream route takes `IPlayerTagger`; `ToSseItems` yields `SseItem<TaggedMatchEvent>` | Phase 2 / TASK-003
- **FILE-008**: `GoalGetter/GoalGetter.Tests/MatchEndpointsTests.cs` | MODIFY | Updates TEST-011; adds TEST-012 and TEST-013; keeps TEST-014 | Phase 2 / TASK-003
- **FILE-009**: `GoalGetter/GoalGetter.Web/MatchApiClient.cs` | MODIFY | `StreamEventsAsync` yields `TaggedMatchEvent` | Phase 2 / TASK-004
- **FILE-010**: `GoalGetter/GoalGetter.Tests/MatchApiClientTests.cs` | MODIFY | Updates TEST-015; adds TEST-016 | Phase 2 / TASK-004
- **FILE-011**: `GoalGetter/GoalGetter.Web/Components/Pages/Match.razor` | MODIFY | Shows `PlayerName` and `TeamName`; removes `playerNames` and `PlayerName(...)` | Phase 2 / TASK-004

## 8. Testing Strategy

| Test Type | Project | Framework | Command |
|-----------|---------|-----------|---------|
| Build | `GoalGetter/GoalGetter.sln` | .NET SDK | `dotnet build GoalGetter/GoalGetter.sln` |
| Unit (contract, tagger, registration) | `GoalGetter/GoalGetter.Tests` | xUnit v3 | `dotnet test GoalGetter/GoalGetter.Tests --filter "FullyQualifiedName~ContractsTests\|FullyQualifiedName~PlayerTaggerTests\|FullyQualifiedName~MatchServiceRegistrationTests"` |
| Unit (Web client with stub handler) | `GoalGetter/GoalGetter.Tests` | xUnit v3 | `dotnet test GoalGetter/GoalGetter.Tests --filter "FullyQualifiedName~MatchApiClientTests"` |
| Integration | `GoalGetter/GoalGetter.Tests` | xUnit v3 with Aspire testing | `dotnet test GoalGetter/GoalGetter.Tests --filter "FullyQualifiedName~MatchEndpointsTests\|FullyQualifiedName~WebTests"` |
| Regression | `GoalGetter/GoalGetter.Tests` | xUnit v3 with Aspire testing | `dotnet test GoalGetter/GoalGetter.Tests` |

## 9. Risks & Assumptions

- **RISK-001**: `TaggedMatchEvent` repeats the `MatchEvent` members. A field added to `MatchEvent` later (for example pass end coordinates in US-03 or shot speed in US-04) must also be added to `TaggedMatchEvent`, or the stream drops it. TEST-005 and TEST-002 catch a missing copy only for the current fields. Plan US-03 and US-04 to extend both records, or to replace the repeated members with one shared payload
- **RISK-002**: No test asserts that the warning log is written, because that needs a log capture package and CON-002 allows no new package. The fallback names are asserted by TEST-006 to TEST-008. Check the `apiservice` structured logs by hand if the feed changes
- **RISK-003**: The `Unknown player` and `Unknown team` fallback strings are English. US-11 (Spanish) must decide whether the server or the page localizes them. It is not needed while the generator only produces known players (REQ-004)
- **ASSUMPTION-001**: A player belongs to the team named by the event's `TeamId`. This matches the generator (TEST-011 of `feature-match-playback-1.md`) and the fixture, where each `Player.TeamId` equals its lineup's team id
- **ASSUMPTION-002**: Showing the team by name is enough for "team" in US-02. Shirt number, position, and crest are not required by the acceptance criteria and are left out
- **ASSUMPTION-003**: The `[LoggerMessage]` generator accepts a `private static partial` method in a `sealed partial class` that also uses a primary constructor. Verify with `dotnet build GoalGetter/GoalGetter.sln`. If it does not compile, make the class non-primary-constructor with an `ILogger` field and use the instance form of the method
- **ASSUMPTION-004**: The US-01 plan is implemented as described in `docs/plan/feature-match-playback-1.md` and its tests pass before this plan starts

## 10. Related Specifications / Further Reading

- [GoalGetter MVP: User Story Map, US-02](../user-story-map.md)
- [Implementation plan for US-01](feature-match-playback-1.md)
- [Handle unmapped members during deserialization](https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/missing-members)
- [High-performance logging with source generation](https://learn.microsoft.com/dotnet/core/extensions/logging/high-performance-logging#define-logger-messages-with-source-generation)
- [What's new in ASP.NET Core 10, minimal APIs](https://learn.microsoft.com/aspnet/core/release-notes/aspnetcore-10.0?view=aspnetcore-10.0#minimal-apis)
- [What's new in .NET 9 libraries, System.Net.ServerSentEvents](https://learn.microsoft.com/dotnet/core/whats-new/dotnet-9/libraries#networking)
- [Blazor StateHasChanged and rendering](https://learn.microsoft.com/aspnet/core/blazor/components/rendering?view=aspnetcore-10.0#statehaschanged)
