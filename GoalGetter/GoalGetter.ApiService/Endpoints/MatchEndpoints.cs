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

        // Each connection starts its own playback from kickoff.
        matches.MapGet("/{matchId:int}/events/stream",
            (int matchId, IFixtureCatalog catalog, IMatchPlayback playback, IPlayerTagger tagger, CancellationToken cancellationToken) =>
                matchId <= 0 ? Results.BadRequest()
                : !catalog.TryGet(matchId, out var fixture) ? Results.NotFound()
                : TypedResults.ServerSentEvents(ToSseItems(playback, tagger, fixture, cancellationToken)));

        return app;
    }

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
}
