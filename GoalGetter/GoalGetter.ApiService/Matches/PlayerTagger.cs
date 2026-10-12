using GoalGetter.Contracts.Matches;

namespace GoalGetter.ApiService.Matches;

public interface IPlayerTagger
{
    // Unresolved ids keep the event and get placeholder names instead of being dropped.
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
