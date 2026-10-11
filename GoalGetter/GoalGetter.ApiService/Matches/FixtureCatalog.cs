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
