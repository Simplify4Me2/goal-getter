---
goal: US-03 Judge a pass by distance and accuracy on the match page
version: 1.0
date_created: 2026-10-12
last_updated: 2026-10-12
owner: GoalGetter team
status: 'Planned'
tags: [feature, api, contracts, analytics, sse, blazor, testing]
---

# Introduction

![Status: Planned](https://img.shields.io/badge/status-Planned-blue)

**Summary for reviewers**: Each pass in the live match stream gets a start point and an outcome, a straight-line distance, and the passer's running accuracy, so the `/match` page can show how good each pass was. The stream computes the figures on the server, which keeps the browser free of match logic. Success means the tests in all three phases pass and the manual flow in Section 4 shows distance and accuracy on every pass row.

This plan delivers user story US-03 from [user-story-map.md](../user-story-map.md). It builds on the tagged event stream from [feature-player-tagging-1.md](feature-player-tagging-1.md) (US-02) and the playback engine from [feature-match-playback-1.md](feature-match-playback-1.md) (US-01).

Terms used in this plan:

- **Pass end point**: `X` and `Y` on the event. This is where the ball lands.
- **Pass start point**: `StartX` and `StartY` on `PassDetails`. This is where the ball was before the pass, which is the previous ball position in the synthetic match.
- **Running accuracy**: completed passes divided by attempted passes for one player on one team, counting the current pass, within one stream connection.

## 1. Requirements & Constraints

- **REQ-001**: Each event of type `Pass` carries `Pass` (`PassDetails`) with `StartX`, `StartY` (metres, pitch coordinates before the pass) and `Completed`. `X` and `Y` remain the pass end point.
- **REQ-002**: On `GET /matches/{matchId}/events/stream`, each `Pass` event carries `Quality` (`PassQuality`) whose `DistanceMetres` is the straight-line distance from `(StartX, StartY)` to `(X, Y)`.
- **REQ-003**: `Quality.AccuracyPercent` equals completed passes divided by attempted passes for the same `(TeamId, PlayerId)` delivered in the same stream, multiplied by 100. The current pass is counted.
- **REQ-004**: Events of any other type carry `Pass` and `Quality` as `null`.
- **REQ-005**: Match 1 produces both completed and failed passes, and the same sequence on every run for the same fixture.
- **REQ-006**: The `/match` page appends `, completed` or `, failed`, then `, {distance} m`, then `, {accuracy}% accuracy` to pass rows. Other rows keep the US-02 layout.
- **REQ-007**: The SSE event name, event id, clock order, and live pace are unchanged (carried from US-02 REQ-005).
- **REQ-008**: A client that reads the stream payload as `MatchEvent` keeps working. Unknown properties are ignored by the default JSON options.
- **SEC-001**: Pass figures are numbers rendered through Razor expressions. No raw markup is built from event data.
- **CON-001**: No new project or NuGet package.
- **CON-002**: Coordinates are metres on the `Pitch` class (length 105, width 68). Pass start points and end points stay inside the pitch.
- **CON-003**: This plan supersedes US-02 CON-003 for `MatchEvent` and `TaggedMatchEvent` only. Both records gain the optional fields `Pass` and `Quality`. No other field changes.
- **CON-004**: Running accuracy state lives per stream connection, because each connection replays the match from kickoff. There is no database.
- **GUD-001**: Numbers in text use `CultureInfo.InvariantCulture`. US-11 (Spanish localisation) changes this later, see RISK-006.
- **GUD-002**: `Program.cs` is unchanged. The new services are registered through `AddMatchServices`.
- **PAT-001**: Inject the new service through an interface (`IPassQualityTracker`) and register it with the same pattern as `IPlayerTagger`.

## 2. Implementation Steps

### Implementation Phase 1 - Pass Contract and Generator

- **GOAL-001**: Add pass start points and outcomes to the contracts and the generator, and carry them through the tagger. No quality measures yet.
- **Completion criteria**: `dotnet build GoalGetter/GoalGetter.sln` exits with 0 errors, and `dotnet test GoalGetter/GoalGetter.Tests --filter "FullyQualifiedName~ContractsTests|FullyQualifiedName~PlayerTaggerTests|FullyQualifiedName~SyntheticMatchGeneratorTests"` exits with 0 failures.

| Task | Description | Files | Completed | Date |
|------|-------------|-------|-----------|------|
| TASK-001 | Add `PassDetails` and the optional `Pass` parameter to `MatchEvent` and `TaggedMatchEvent`. Optional parameters keep existing positional calls compiling. | FILE-001, FILE-002 | | |
| TASK-002 | Make `PlayerTagger` copy `Pass` into `TaggedMatchEvent`, so the stream keeps pass details. | FILE-003, FILE-004 | | |
| TASK-003 | Make `SyntheticMatchGenerator` record the pass start point, roll a completion outcome, and move the pass logic into a helper. | FILE-005, FILE-006 | | |

#### TASK-001 Code

Source: https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/nullable-annotations#relationship-between-nullable-and-optional-parameters

```csharp
// GoalGetter/GoalGetter.Contracts/Matches/MatchContracts.cs
public sealed record PassDetails(double StartX, double StartY, bool Completed);

public sealed record MatchEvent(
    string Id, int MatchId, int ClockSeconds, EventType Type, string TeamId, int PlayerId,
    double X, double Y, PassDetails? Pass = null);

public sealed record TaggedMatchEvent(
    string Id, int MatchId, int ClockSeconds, EventType Type,
    string TeamId, string TeamName, int PlayerId, string PlayerName,
    double X, double Y, PassDetails? Pass = null);
```

The optional `Pass` parameter follows the same pattern as the optional parameters in the Microsoft Learn sample `record MyPoco(..., string? OptionalNullable = "default")`. Existing ten-argument `TaggedMatchEvent` calls compile unchanged, and the System.Text.Json constructor binding treats a missing `pass` property as `null`.

#### TASK-001 Tests

- **TEST-001**: `MatchEvent_PassDetailsRoundTripsThroughJson` in `GoalGetter/GoalGetter.Tests/ContractsTests.cs`. Serializes a pass with `PassDetails(50.0, 34.0, false)` using `JsonSerializerOptions.Web`, deserializes it as `MatchEvent`, and asserts equality. Expected: the record equals the original.
- **TEST-002**: `MatchEvent_NonPassSerializesPassAsNull` in `GoalGetter/GoalGetter.Tests/ContractsTests.cs`. Serializes a `Shot` event and asserts the JSON contains `"pass":null`. Expected: the property is present and null (REQ-004).

#### TASK-001 Boy Scout

No boy scout findings

#### TASK-002 Code

Source: Not applicable, no Microsoft API used

```csharp
// GoalGetter/GoalGetter.ApiService/Matches/PlayerTagger.cs
public TaggedMatchEvent Tag(Fixture fixture, MatchEvent matchEvent)
{
    // team and player lookups as today, then:
    return new TaggedMatchEvent(
        matchEvent.Id, matchEvent.MatchId, matchEvent.ClockSeconds, matchEvent.Type,
        matchEvent.TeamId, team?.Name ?? UnknownTeamName,
        matchEvent.PlayerId, player?.Name ?? UnknownPlayerName,
        matchEvent.X, matchEvent.Y, matchEvent.Pass);
}
```

#### TASK-002 Tests

- **TEST-003**: `Tag_KeepsAllSourceEventFields` in `GoalGetter/GoalGetter.Tests/PlayerTaggerTests.cs` (existing test, modified). The source event is now a pass with `PassDetails(40.0, 31.0, true)`. Asserts the tagged event's `Pass` equals the source `Pass`. Expected: the pass details are copied unchanged.
- **TEST-004**: Existing `PlayerTaggerTests` tests for unknown player and unknown team run unchanged. Expected: all pass, proving the lookup refactor in BSC-002 keeps behavior.

#### TASK-002 Boy Scout

- **BSC-001**: `GoalGetter/GoalGetter.ApiService/Matches/PlayerTagger.cs` | Code smell | Team lookup allocates a two-element array on every event (`new[] { fixture.Home, fixture.Away }.FirstOrDefault(...)`) when two comparisons are enough. | Replace with `Team? team = matchEvent.TeamId == fixture.Home.Id ? fixture.Home : matchEvent.TeamId == fixture.Away.Id ? fixture.Away : null;`. Same result for every input, including when both teams share an id. Covered by TEST-004.

#### TASK-003 Code

Source: https://learn.microsoft.com/dotnet/api/system.random.next?view=net-10.0

```csharp
// GoalGetter/GoalGetter.ApiService/Matches/SyntheticMatchGenerator.cs
public const int PassMaxAlongMetres = 15;
public const int PassMaxAcrossMetres = 10;
private const int PassMinAlongMetres = -5;
private const int PassCompletionPercent = 80;

// Inside Generate, replacing the inline pass block:
PassDetails? pass = null;
if (type == EventType.Pass)
{
    var (startX, startY) = (x, y);
    (x, y) = MovePass(random, x, y, direction);
    pass = new PassDetails(startX, startY, random.Next(100) < PassCompletionPercent);
}

events.Add(new MatchEvent(
    $"{fixture.MatchId}-{events.Count + 1:D4}", fixture.MatchId, clock, type,
    actor.Id, player.Id, x, y, pass));

private static (double X, double Y) MovePass(Random random, double x, double y, int direction) => (
    Math.Clamp(x + direction * random.Next(PassMinAlongMetres, PassMaxAlongMetres + 1), 0, Pitch.LengthMetres),
    Math.Clamp(y + random.Next(-PassMaxAcrossMetres, PassMaxAcrossMetres + 1), 0, Pitch.WidthMetres));
```

The completion draw runs after the move draws, so the seeded sequence is fixed for each fixture. A failed pass keeps possession with the same passer's team. `random.Next(upper)` returns a value from 0 up to but not including `upper`, so `random.Next(100) < 80` gives an 80 percent chance.

#### TASK-003 Tests

- **TEST-005**: `Generate_OnlyPassesCarryPassDetails` in `GoalGetter/GoalGetter.Tests/SyntheticMatchGeneratorTests.cs`. Generates match 1 and asserts `Pass is not null` exactly when `Type == EventType.Pass`. Expected: no exceptions (REQ-004).
- **TEST-006**: `Generate_IncludesCompletedAndFailedPasses` in `GoalGetter/GoalGetter.Tests/SyntheticMatchGeneratorTests.cs`. Asserts the generated passes contain at least one `Completed == true` and at least one `Completed == false`. Expected: both outcomes present (REQ-005).
- **TEST-007**: `Generate_PassStartAndEndStayInsidePitch` in `GoalGetter/GoalGetter.Tests/SyntheticMatchGeneratorTests.cs`. Asserts every `StartX`, `X` lies in `[0, Pitch.LengthMetres]` and every `StartY`, `Y` lies in `[0, Pitch.WidthMetres]` (CON-002).
- **TEST-008**: `Generate_PassReachStaysWithinConfiguredLimits` in `GoalGetter/GoalGetter.Tests/SyntheticMatchGeneratorTests.cs`. Asserts `Math.Abs(X - StartX) <= PassMaxAlongMetres` and `Math.Abs(Y - StartY) <= PassMaxAcrossMetres` for every pass. Expected: both hold, and the constants are public so the test reads them.
- **TEST-009**: Existing `SyntheticMatchGeneratorTests` tests `Generate_KeepsCoordinatesInsidePitch` and `Generate_SameFixtureTwice_ReturnsIdenticalEvents` run unchanged. Expected: all pass, which shows the seeded output is still deterministic (REQ-005).

#### TASK-003 Boy Scout

- **BSC-002**: `GoalGetter/GoalGetter.ApiService/Matches/SyntheticMatchGenerator.cs` | Code smell | `Generate` mixes the pass movement magic numbers (`-5`, `16`, `-10`, `11`) and the pitch clamping inline inside a long method. | Move the constants into named `const` fields and the movement into `MovePass`, as shown in the TASK-003 code. The draw order inside the helper is the same as the inline code, so the output after this task is unchanged. Covered by TEST-008 and TEST-009.

### Implementation Phase 2 - Pass Quality and Stream

- **GOAL-002**: Compute each pass's distance and its passer's running accuracy in a per-stream tracker, and send the result on every pass event of the SSE stream.
- **Completion criteria**: `dotnet build GoalGetter/GoalGetter.sln` exits with 0 errors, and `dotnet test GoalGetter/GoalGetter.Tests --filter "FullyQualifiedName~PassQualityTrackerTests|FullyQualifiedName~MatchServiceRegistrationTests|FullyQualifiedName~MatchEndpointsTests"` exits with 0 failures.

| Task | Description | Files | Completed | Date |
|------|-------------|-------|-----------|------|
| TASK-004 | Add `PassQuality`, the optional `Quality` parameter on `TaggedMatchEvent`, the `IPassQualityTracker` interface, and a `PassQualityTracker` class that keeps tallies per `(TeamId, PlayerId)`. Register it as transient. | FILE-001, FILE-007, FILE-008, FILE-009, FILE-010 | | |
| TASK-005 | Wire the tracker into the SSE stream so each pass event carries `Quality`, and other events carry `null`. | FILE-011, FILE-012 | | |

#### TASK-004 Code

Source: https://learn.microsoft.com/dotnet/api/system.collections.generic.dictionary-2.trygetvalue?view=net-10.0
Source: https://learn.microsoft.com/dotnet/csharp/fundamentals/types/structs#declare-a-struct

```csharp
// GoalGetter/GoalGetter.Contracts/Matches/MatchContracts.cs
public sealed record PassQuality(double DistanceMetres, double AccuracyPercent);

public sealed record TaggedMatchEvent(
    string Id, int MatchId, int ClockSeconds, EventType Type,
    string TeamId, string TeamName, int PlayerId, string PlayerName,
    double X, double Y, PassDetails? Pass = null, PassQuality? Quality = null);

// GoalGetter/GoalGetter.ApiService/Matches/PassQualityTracker.cs
public interface IPassQualityTracker
{
    // Returns null for every event that is not a pass.
    PassQuality? Measure(MatchEvent matchEvent);
}

public sealed class PassQualityTracker : IPassQualityTracker
{
    private readonly Dictionary<(string TeamId, int PlayerId), (int Attempted, int Completed)> tallies = new();

    public PassQuality? Measure(MatchEvent matchEvent)
    {
        if (matchEvent.Pass is not { } pass)
        {
            return null;
        }

        var key = (matchEvent.TeamId, matchEvent.PlayerId);
        tallies.TryGetValue(key, out var tally);
        tally = (tally.Attempted + 1, tally.Completed + (pass.Completed ? 1 : 0));
        tallies[key] = tally;

        var dx = matchEvent.X - pass.StartX;
        var dy = matchEvent.Y - pass.StartY;
        return new PassQuality(
            Math.Sqrt(dx * dx + dy * dy),
            tally.Completed * 100.0 / tally.Attempted);
    }
}

// GoalGetter/GoalGetter.ApiService/Matches/MatchServiceCollectionExtensions.cs
// Transient: each stream connection replays from kickoff and needs its own tallies.
services.AddTransient<IPassQualityTracker, PassQualityTracker>();
```

`TryGetValue` leaves `tally` as the default `(0, 0)` when the key is missing, so the first pass of a player starts from zero. `Math.Sqrt` on the squared differences gives the straight-line distance in metres.

#### TASK-004 Tests

- **TEST-010**: `Measure_NonPassEvent_ReturnsNull` in `GoalGetter/GoalGetter.Tests/PassQualityTrackerTests.cs` (CREATE). Measures a `Shot` and asserts the result is `null`. Expected: null (REQ-004).
- **TEST-011**: `Measure_CompletedPass_ReturnsStraightLineDistance` in `GoalGetter/GoalGetter.Tests/PassQualityTrackerTests.cs`. Start `(10, 10)`, end `(13, 14)`, completed. Expected: `DistanceMetres` 5.0 and `AccuracyPercent` 100 (REQ-002).
- **TEST-012**: `Measure_FirstPassFailed_ReturnsZeroAccuracy` in `GoalGetter/GoalGetter.Tests/PassQualityTrackerTests.cs`. Expected: `AccuracyPercent` 0 (REQ-003).
- **TEST-013**: `Measure_CompletedThenFailedThenCompleted_ReturnsRunningAccuracy` in `GoalGetter/GoalGetter.Tests/PassQualityTrackerTests.cs`. Expected accuracy sequence 100, 50, and 66.67 (within 0.01) (REQ-003).
- **TEST-014**: `Measure_ShotsDoNotChangeAccuracy` in `GoalGetter/GoalGetter.Tests/PassQualityTrackerTests.cs`. A shot between two passes by the same player does not change the second pass's accuracy. Expected: the accuracy is the same as without the shot (REQ-003).
- **TEST-015**: `Measure_TalliesArePerTeamAndPlayer` in `GoalGetter/GoalGetter.Tests/PassQualityTrackerTests.cs`. A failed pass by `("HCF", 1)` and a completed pass by `("NGR", 1)`. Expected: the NGR pass has 100 percent (REQ-003).
- **TEST-016**: `Measure_ZeroLengthPass_ReturnsZeroDistance` in `GoalGetter/GoalGetter.Tests/PassQualityTrackerTests.cs`. Start equals end. Expected: `DistanceMetres` 0.
- **TEST-017**: `AddMatchServices_RegistersPassQualityTrackerAsTransient` in `GoalGetter/GoalGetter.Tests/MatchServiceRegistrationTests.cs`. Resolves `IPassQualityTracker` twice from the root provider and asserts the instances differ. Expected: two distinct instances.

#### TASK-004 Boy Scout

No boy scout findings

#### TASK-005 Code

Source: https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/record#nondestructive-mutation
Source: https://learn.microsoft.com/dotnet/core/extensions/dependency-injection/service-lifetimes

```csharp
// GoalGetter/GoalGetter.ApiService/Endpoints/MatchEndpoints.cs
matches.MapGet("/{matchId:int}/events/stream",
    (int matchId, IFixtureCatalog catalog, IMatchPlayback playback, IPlayerTagger tagger,
     IPassQualityTracker passQuality, CancellationToken cancellationToken) =>
        matchId <= 0 ? Results.BadRequest()
        : !catalog.TryGet(matchId, out var fixture) ? Results.NotFound()
        : TypedResults.ServerSentEvents(ToSseItems(playback, tagger, passQuality, fixture, cancellationToken)));

private static async IAsyncEnumerable<SseItem<TaggedMatchEvent>> ToSseItems(
    IMatchPlayback playback, IPlayerTagger tagger, IPassQualityTracker passQuality,
    Fixture fixture, [EnumeratorCancellation] CancellationToken cancellationToken)
{
    await foreach (var matchEvent in playback.PlayAsync(fixture, cancellationToken))
    {
        var tagged = tagger.Tag(fixture, matchEvent) with { Quality = passQuality.Measure(matchEvent) };
        yield return new SseItem<TaggedMatchEvent>(tagged, MatchEventName) { EventId = matchEvent.Id };
    }
}
```

The `with` expression copies the tagged record and sets only `Quality`. The tracker is resolved per request, so each stream connection gets its own tallies.

#### TASK-005 Tests

- **TEST-018**: `StreamEvents_PassEventsCarryQualityAndOtherEventsDoNot` in `GoalGetter/GoalGetter.Tests/MatchEndpointsTests.cs`. Reads the first 60 events of match 1 with the existing `ReadStreamAsync<T>` helper. Asserts `Quality is not null` exactly when `Pass is not null`, and that at least one pass exists. Expected: the rule holds for every event (REQ-002, REQ-004).
- **TEST-019**: `StreamEvents_PassAccuracyMatchesRunningTallyPerPlayer` in `GoalGetter/GoalGetter.Tests/MatchEndpointsTests.cs`. Recomputes each player's running accuracy from the `Pass.Completed` values in the same stream, and asserts each pass's `AccuracyPercent` equals the recomputed value within 0.0001. Expected: matches for every pass (REQ-003).
- **TEST-020**: Existing `MatchEndpointsTests` tests for the stream, 404 on unknown match, and 400 on invalid match id run unchanged. Expected: all pass (REQ-007).

#### TASK-005 Boy Scout

- **BSC-003**: `GoalGetter/GoalGetter.ApiService/Endpoints/MatchEndpoints.cs` | Code smell | Stream helper `ToSseItems` takes five parameters, and each new service adds one more. | Keep the five parameters in this plan, and record the grouping option as RISK-005. No change in this task. Covered by TEST-018 and TEST-019.

### Implementation Phase 3 - Web Formatting and Match Page

- **GOAL-003**: Show distance, outcome, and running accuracy on pass rows of the `/match` page, with the row text built in a testable formatter.
- **Completion criteria**: `dotnet build GoalGetter/GoalGetter.sln` exits with 0 errors, and `dotnet test GoalGetter/GoalGetter.Tests` exits with 0 failures across all test classes.

| Task | Description | Files | Completed | Date |
|------|-------------|-------|-----------|------|
| TASK-006 | Add `MatchEventFormatter` in the Web project with `FormatClock` and `Format`. Pass rows get the outcome, distance, and accuracy suffix. | FILE-013, FILE-014 | | |
| TASK-007 | Make the `/match` page use the formatter, remove its local clock helper, and cover the client parsing of the new fields. | FILE-015, FILE-016 | | |

#### TASK-006 Code

Source: https://learn.microsoft.com/dotnet/standard/base-types/custom-numeric-format-strings#the-,-custom-specifier

```csharp
// GoalGetter/GoalGetter.Web/MatchEventFormatter.cs
using System.Globalization;
using GoalGetter.Contracts.Matches;

namespace GoalGetter.Web;

public static class MatchEventFormatter
{
    public static string FormatClock(int seconds) => $"{seconds / 60:00}:{seconds % 60:00}";

    public static string Format(TaggedMatchEvent matchEvent)
    {
        var line = $"{FormatClock(matchEvent.ClockSeconds)} {matchEvent.Type}, {matchEvent.PlayerName}, " +
                   $"{matchEvent.TeamName} ({matchEvent.X:0}, {matchEvent.Y:0})";

        if (matchEvent.Pass is null || matchEvent.Quality is null)
        {
            return line;
        }

        var outcome = matchEvent.Pass.Completed ? "completed" : "failed";
        var distance = matchEvent.Quality.DistanceMetres.ToString("0.0", CultureInfo.InvariantCulture);
        var accuracy = matchEvent.Quality.AccuracyPercent.ToString("0", CultureInfo.InvariantCulture);
        return $"{line}, {outcome}, {distance} m, {accuracy}% accuracy";
    }
}
```

The `"0.0"` and `"0"` custom numeric format strings with `CultureInfo.InvariantCulture` match the Microsoft Learn pattern `value.ToString("#,#", CultureInfo.InvariantCulture)`, so the output does not depend on the viewer's locale (GUD-001).

#### TASK-006 Tests

- **TEST-021**: `Format_NonPassEvent_UsesUs02Layout` in `GoalGetter/GoalGetter.Tests/MatchEventFormatterTests.cs` (CREATE). A shot at 42 seconds by "Tomas Brandt" for "Harbor City FC" at `(52, 31)`. Expected: `00:42 Shot, Tomas Brandt, Harbor City FC (52, 31)` (REQ-006).
- **TEST-022**: `Format_CompletedPass_AppendsOutcomeDistanceAndAccuracy` in `GoalGetter/GoalGetter.Tests/MatchEventFormatterTests.cs`. A completed pass at 65 seconds with `DistanceMetres` 18.4 and `AccuracyPercent` 75. Expected: `01:05 Pass, Tomas Brandt, Harbor City FC (52, 31), completed, 18.4 m, 75% accuracy` (REQ-006).
- **TEST-023**: `Format_FailedPass_ShowsFailed` in `GoalGetter/GoalGetter.Tests/MatchEventFormatterTests.cs`. Expected: the suffix contains `, failed, ` (REQ-006).
- **TEST-024**: `Format_PassWithoutQuality_OmitsSuffix` in `GoalGetter/GoalGetter.Tests/MatchEventFormatterTests.cs`. A pass with `Quality` null. Expected: the output has no `m,` or `accuracy` text (REQ-006).
- **TEST-025**: `FormatClock_ReturnsMinutesAndSeconds` in `GoalGetter/GoalGetter.Tests/MatchEventFormatterTests.cs`. `FormatClock(754)` returns `12:34` (REQ-007).

#### TASK-006 Boy Scout

No boy scout findings

#### TASK-007 Code

Source: Not applicable, no Microsoft API used

```razor
@* GoalGetter/GoalGetter.Web/Components/Pages/Match.razor *@
<ul>
    @foreach (var matchEvent in events)
    {
        <li>@MatchEventFormatter.Format(matchEvent)</li>
    }
</ul>

@code {
    // The local FormatClock helper is removed. The clock label uses MatchEventFormatter.FormatClock.
}
```

The page encodes the returned string through the normal Razor expression, which keeps SEC-001.

#### TASK-007 Tests

- **TEST-026**: `StreamEventsAsync_ParsesPassDetailsAndQuality` in `GoalGetter/GoalGetter.Tests/MatchApiClientTests.cs`. The existing `StubHandler` returns one SSE frame holding a `TaggedMatchEvent` with `PassDetails(40, 31, true)` and `PassQuality(12, 75)`. Expected: the yielded event equals the sent event, with `Pass.Completed` true and `Quality.AccuracyPercent` 75 (REQ-001, REQ-002).
- **TEST-027**: Build check `dotnet build GoalGetter/GoalGetter.sln`. Expected: `GoalGetter.Web` compiles with the new `MatchEventFormatter` reference and no unresolved `FormatClock` in `Match.razor`.
- **TEST-028**: Existing `WebTests` run unchanged. Expected: all pass, which shows the page still renders the layout (REQ-007).

#### TASK-007 Boy Scout

No boy scout findings

## 3. Boy Scout Summary

| Item | File | Category | Phase / Task | Status |
|------|------|----------|--------------|--------|
| BSC-001 | GoalGetter/GoalGetter.ApiService/Matches/PlayerTagger.cs | Code smell | Phase 1 / TASK-002 | Open |
| BSC-002 | GoalGetter/GoalGetter.ApiService/Matches/SyntheticMatchGenerator.cs | Code smell | Phase 1 / TASK-003 | Open |
| BSC-003 | GoalGetter/GoalGetter.ApiService/Endpoints/MatchEndpoints.cs | Code smell | Phase 2 / TASK-005 | Open |

## 4. Manual End-to-End Test

- **Prerequisites**: .NET 10 SDK, Aspire CLI, and a browser. The stream request format is in `GoalGetter/GoalGetter.ApiService/GoalGetter.ApiService.http`.
- **Start**: Run `aspire start` from `GoalGetter/`, then open the Aspire dashboard and confirm all resources are Running
- **E2E-001**: Precondition: AppHost is running. Action: request `GET /matches/1/events/stream` from the ApiService URL shown in the dashboard and read the first 60 `data:` lines. Expected: every line with `"type":"Pass"` has a `pass` object with `startX`, `startY`, and `completed`, and a `quality` object with `distanceMetres` and `accuracyPercent`. The distance equals the square root of `(x - startX)^2 + (y - startY)^2` within 0.0001. The first pass of each player has `accuracyPercent` 100 when `completed` is true and 0 when it is false. Other lines have `"pass":null` and `"quality":null`.
- **E2E-002**: Precondition: AppHost is running. Action: open the web frontend, go to `/match`, and press Start. Expected: each pass row reads like `00:12 Pass, Tomas Brandt, Harbor City FC (52, 31), completed, 18.4 m, 75% accuracy`, or `failed` in place of `completed`. Non-pass rows keep the US-02 layout with no suffix, and no row shows `Unknown`.
- **E2E-003**: Negative path. Precondition: AppHost is running. Action: request `GET /matches/999/events/stream`. Expected: `404 Not Found`, with no stream body, as in US-02.
- **E2E-004**: Negative path. Precondition: stop the ApiService resource in the dashboard. Action: open `/match` and press Start. Expected: the page shows the existing "Could not start the match" message and no pass rows.
- **Stop**: Run `aspire stop` when testing is complete

## 5. Alternatives

- **ALT-001**: Add a separate `PassEvent` record type. Rejected because the stream payload is one `TaggedMatchEvent` type. A subtype would need polymorphic JSON handling in the Blazor client and the feature only adds two optional fields.
- **ALT-002**: Store the distance in the generator or the ingest contract. Rejected because the ingest contract carries raw coordinates. Derived measures are computed when the stream is read, following the same reasoning as US-02 ALT-001.
- **ALT-003**: Register the tracker as a singleton keyed by match. Rejected because two viewers connected to the same match would share one tally, and a late viewer would see accuracy that does not start from kickoff.
- **ALT-004**: Compute accuracy in the browser. Rejected because the overlay and other consumers would repeat the logic. It also makes the `/match` page carry the tally state.
- **ALT-005**: Keep all synthetic passes completed. Rejected because accuracy would always read 100 percent, which does not test the feature.
- **ALT-006**: Treat a failed pass as a possession change. Rejected for this story because it adds implicit turnover events that US-07 must define first (see RISK-003).

## 6. Dependencies

- **DEP-001**: US-02 implementation (`IPlayerTagger`, `TaggedMatchEvent`, `ToSseItems`, `Match.razor`) as described in [feature-player-tagging-1.md](feature-player-tagging-1.md). This plan builds on those types.
- **DEP-002**: US-01 playback engine (`IMatchPlayback`, `SyntheticMatchGenerator`) as described in [feature-match-playback-1.md](feature-match-playback-1.md).
- **DEP-003**: `Microsoft.Extensions.DependencyInjection` transient registration, provided by the ASP.NET Core shared framework. No package change.
- **DEP-004**: `System.Text.Json` with `JsonSerializerOptions.Web`, provided by the shared framework. No package change.
- **DEP-005**: The `GoalGetter.Tests` project with the `TestAppHost` fixture for endpoint and web tests.

## 7. Files

- **FILE-001**: `GoalGetter/GoalGetter.Contracts/Matches/MatchContracts.cs` | MODIFY | `PassDetails`, optional `MatchEvent.Pass`, `TaggedMatchEvent.Pass`, `PassQuality`, optional `TaggedMatchEvent.Quality` | Phase 1 / TASK-001, Phase 2 / TASK-004
- **FILE-002**: `GoalGetter/GoalGetter.Tests/ContractsTests.cs` | MODIFY | Adds TEST-001 and TEST-002 | Phase 1 / TASK-001
- **FILE-003**: `GoalGetter/GoalGetter.ApiService/Matches/PlayerTagger.cs` | MODIFY | `Tag` copies `Pass`; team lookup without array allocation (BSC-001) | Phase 1 / TASK-002
- **FILE-004**: `GoalGetter/GoalGetter.Tests/PlayerTaggerTests.cs` | MODIFY | `Tag_KeepsAllSourceEventFields` covers `Pass` (TEST-003); existing tests run unchanged (TEST-004) | Phase 1 / TASK-002
- **FILE-005**: `GoalGetter/GoalGetter.ApiService/Matches/SyntheticMatchGenerator.cs` | MODIFY | Pass start points, completion roll, public reach constants, `MovePass` helper (BSC-002) | Phase 1 / TASK-003
- **FILE-006**: `GoalGetter/GoalGetter.Tests/SyntheticMatchGeneratorTests.cs` | MODIFY | Adds TEST-005 to TEST-008; existing tests run unchanged (TEST-009) | Phase 1 / TASK-003
- **FILE-007**: `GoalGetter/GoalGetter.ApiService/Matches/PassQualityTracker.cs` | CREATE | `IPassQualityTracker`, `PassQualityTracker` | Phase 2 / TASK-004
- **FILE-008**: `GoalGetter/GoalGetter.ApiService/Matches/MatchServiceCollectionExtensions.cs` | MODIFY | `AddTransient<IPassQualityTracker, PassQualityTracker>()` | Phase 2 / TASK-004
- **FILE-009**: `GoalGetter/GoalGetter.Tests/PassQualityTrackerTests.cs` | CREATE | Adds TEST-010 to TEST-016 | Phase 2 / TASK-004
- **FILE-010**: `GoalGetter/GoalGetter.Tests/MatchServiceRegistrationTests.cs` | MODIFY | Adds TEST-017 | Phase 2 / TASK-004
- **FILE-011**: `GoalGetter/GoalGetter.ApiService/Endpoints/MatchEndpoints.cs` | MODIFY | Stream route and `ToSseItems` take `IPassQualityTracker` and set `Quality` with `with` (BSC-003 noted) | Phase 2 / TASK-005
- **FILE-012**: `GoalGetter/GoalGetter.Tests/MatchEndpointsTests.cs` | MODIFY | Adds TEST-018 and TEST-019; existing tests run unchanged (TEST-020) | Phase 2 / TASK-005
- **FILE-013**: `GoalGetter/GoalGetter.Web/MatchEventFormatter.cs` | CREATE | `FormatClock`, `Format` with pass suffix | Phase 3 / TASK-006
- **FILE-014**: `GoalGetter/GoalGetter.Tests/MatchEventFormatterTests.cs` | CREATE | Adds TEST-021 to TEST-025 | Phase 3 / TASK-006
- **FILE-015**: `GoalGetter/GoalGetter.Web/Components/Pages/Match.razor` | MODIFY | Rows use `MatchEventFormatter`; local `FormatClock` removed | Phase 3 / TASK-007
- **FILE-016**: `GoalGetter/GoalGetter.Tests/MatchApiClientTests.cs` | MODIFY | Adds TEST-026 | Phase 3 / TASK-007

## 8. Testing Strategy

| Test Type | Project | Framework | Command |
|-----------|---------|-----------|---------|
| Unit (contracts, generator, tagger) | `GoalGetter/GoalGetter.Tests` | xUnit v3 | `dotnet test GoalGetter/GoalGetter.Tests --filter "FullyQualifiedName~ContractsTests\|FullyQualifiedName~PlayerTaggerTests\|FullyQualifiedName~SyntheticMatchGeneratorTests"` |
| Unit (tracker, registration) | `GoalGetter/GoalGetter.Tests` | xUnit v3 | `dotnet test GoalGetter/GoalGetter.Tests --filter "FullyQualifiedName~PassQualityTrackerTests\|FullyQualifiedName~MatchServiceRegistrationTests"` |
| Unit (formatter, client) | `GoalGetter/GoalGetter.Tests` | xUnit v3 | `dotnet test GoalGetter/GoalGetter.Tests --filter "FullyQualifiedName~MatchEventFormatterTests\|FullyQualifiedName~MatchApiClientTests"` |
| Integration (SSE stream) | `GoalGetter/GoalGetter.Tests` | xUnit v3 with `TestAppHost` | `dotnet test GoalGetter/GoalGetter.Tests --filter "FullyQualifiedName~MatchEndpointsTests"` |
| Web | `GoalGetter/GoalGetter.Tests` | xUnit v3 with `TestAppHost` | `dotnet test GoalGetter/GoalGetter.Tests --filter "FullyQualifiedName~WebTests"` |
| Full regression | `GoalGetter/GoalGetter.Tests` | xUnit v3 | `dotnet test GoalGetter/GoalGetter.Tests` |

## 9. Risks & Assumptions

- **RISK-001**: `MatchEvent` and `TaggedMatchEvent` repeat their fields, so a new field must be added to both and copied in `PlayerTagger.Tag`. TEST-003 catches a missed `Pass` copy, but not a future field that nobody tests. Mitigation: each new field gets a copy test. A shared base payload is a later refactor (US-04).
- **RISK-002**: Optional trailing parameters on positional records compile silently when a call site omits them. A future field might be dropped without a compile error. Mitigation: TEST-001 and TEST-003 check the fields round-trip.
- **RISK-003**: Failed passes keep possession (ASSUMPTION-001). US-07 (turnovers) may need a possession change event. This plan does not add it, so US-07 must revisit the generator.
- **RISK-004**: Each stream connection starts its tallies at kickoff. A client that reconnects mid-match gets the replay from kickoff, so the accuracy it sees matches the first connection. A client that joins after playback has moved on sees the same numbers, since the server replays from zero.
- **RISK-005**: `ToSseItems` now takes five parameters. The next story that adds a service should group the services into one context object. BSC-003 records this; no change in this plan.
- **RISK-006**: Distance and accuracy use invariant culture, so a decimal point is shown. US-11 (Spanish localisation) needs a decision on decimal commas before the formatter is localised.
- **ASSUMPTION-001**: Failed passes keep possession with the passer's team. The generator does not add a possession change for a failed pass.
- **ASSUMPTION-002**: `X` and `Y` remain the pass end point. `PassDetails` carries the start point. The US-02 consumer that reads `X` and `Y` as the event location still gets the end point.
- **ASSUMPTION-003**: Distance is the straight-line distance on the pitch in metres. It is not the path length of the ball.
- **ASSUMPTION-004**: Running accuracy counts only passes. Shots, tackles, and other events do not change it.
- **ASSUMPTION-005**: The synthetic completion rate of 80 percent is a tuning value for demo data, not a football statistic.
- **ASSUMPTION-006**: US-02 (`feature-player-tagging-1.md`) is implemented and its tests pass before this plan starts.

## 10. Related Specifications / Further Reading

- [User story map](../user-story-map.md) (US-03 origin)
- [US-02 plan: feature-player-tagging-1.md](feature-player-tagging-1.md)
- [US-01 plan: feature-match-playback-1.md](feature-match-playback-1.md)
- [Nullable annotations and optional parameters (System.Text.Json)](https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/nullable-annotations#relationship-between-nullable-and-optional-parameters)
- [Records: nondestructive mutation](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/record#nondestructive-mutation)
- [Random.Next](https://learn.microsoft.com/dotnet/api/system.random.next?view=net-10.0)
- [Dictionary.TryGetValue](https://learn.microsoft.com/dotnet/api/system.collections.generic.dictionary-2.trygetvalue?view=net-10.0)
- [Struct declaration (Math.Sqrt sample)](https://learn.microsoft.com/dotnet/csharp/fundamentals/types/structs#declare-a-struct)
- [Service lifetimes](https://learn.microsoft.com/dotnet/core/extensions/dependency-injection/service-lifetimes)
- [Custom numeric format strings](https://learn.microsoft.com/dotnet/standard/base-types/custom-numeric-format-strings)
