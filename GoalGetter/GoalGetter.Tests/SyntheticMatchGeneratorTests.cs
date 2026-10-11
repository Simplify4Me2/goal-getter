using GoalGetter.ApiService.Matches;
using GoalGetter.Contracts.Matches;

namespace GoalGetter.Tests;

public class SyntheticMatchGeneratorTests
{
    private readonly SyntheticMatchGenerator generator = new();
    private readonly Fixture fixture = LoadMatchOne();

    [Fact]
    public void Generate_ReturnsEventsInNonDecreasingClockOrder()
    {
        var clocks = generator.Generate(fixture).Select(e => e.ClockSeconds).ToList();

        Assert.Equal(clocks.Order(), clocks);
        Assert.True(clocks[0] >= 3);
        Assert.True(clocks[^1] <= SyntheticMatchGenerator.MatchDurationSeconds);
    }

    [Fact]
    public void Generate_AssignsUniqueSequentialIds()
    {
        var ids = generator.Generate(fixture).Select(e => e.Id).ToList();

        Assert.Equal(Enumerable.Range(1, ids.Count).Select(i => $"1-{i:D4}"), ids);
    }

    [Fact]
    public void Generate_CoversAllFiveEventTypes()
    {
        var types = generator.Generate(fixture).Select(e => e.Type).ToHashSet();

        Assert.All(Enum.GetValues<EventType>(), type => Assert.Contains(type, types));
    }

    [Fact]
    public void Generate_KeepsCoordinatesInsidePitch()
    {
        var events = generator.Generate(fixture);

        Assert.All(events, e => Assert.InRange(e.X, 0, Pitch.LengthMetres));
        Assert.All(events, e => Assert.InRange(e.Y, 0, Pitch.WidthMetres));
    }

    [Fact]
    public void Generate_TagsPlayersFromTheActingTeamLineup()
    {
        var lineupsByTeam = new[] { fixture.Home, fixture.Away }
            .ToDictionary(team => team.Id, team => team.Lineup.Select(player => player.Id).ToHashSet());

        Assert.All(generator.Generate(fixture), e => Assert.Contains(e.PlayerId, lineupsByTeam[e.TeamId]));
    }

    [Fact]
    public void Generate_ShotsOnlyHappenInAttackingThird()
    {
        var shots = generator.Generate(fixture).Where(e => e.Type == EventType.Shot).ToList();

        Assert.NotEmpty(shots);
        Assert.All(shots.Where(e => e.TeamId == fixture.Home.Id), e => Assert.InRange(e.X, 70, Pitch.LengthMetres));
        Assert.All(shots.Where(e => e.TeamId == fixture.Away.Id), e => Assert.InRange(e.X, 0, 35));
    }

    [Fact]
    public void Generate_SameFixtureTwice_ReturnsIdenticalEvents()
    {
        var first = generator.Generate(fixture);
        var second = generator.Generate(fixture);

        Assert.Equal(first, second);
    }

    private static Fixture LoadMatchOne()
    {
        new InMemoryFixtureCatalog().TryGet(1, out var fixture);
        return fixture;
    }
}
