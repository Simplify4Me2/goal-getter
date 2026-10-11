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
