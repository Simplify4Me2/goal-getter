using GoalGetter.ApiService.Matches;
using GoalGetter.Contracts.Matches;

namespace GoalGetter.Tests;

public class FixtureCatalogTests
{
    private readonly InMemoryFixtureCatalog catalog = new();

    [Fact]
    public void TryGet_Match1_ReturnsFixtureWithTwoFullLineups()
    {
        var found = catalog.TryGet(1, out var fixture);

        Assert.True(found);
        Assert.Equal(11, fixture.Home.Lineup.Count);
        Assert.Equal(11, fixture.Away.Lineup.Count);
        Assert.All(fixture.Home.Lineup, player => Assert.Equal(fixture.Home.Id, player.TeamId));
        Assert.All(fixture.Away.Lineup, player => Assert.Equal(fixture.Away.Id, player.TeamId));
        Assert.Equal(22, fixture.Home.Lineup.Concat(fixture.Away.Lineup).Select(player => player.Id).Distinct().Count());
    }

    [Fact]
    public void TryGet_UnknownMatch_ReturnsFalse()
    {
        var found = catalog.TryGet(999, out _);

        Assert.False(found);
    }

    [Fact]
    public void TryGet_Match1_LineupHasOneGoalkeeperPerTeam()
    {
        catalog.TryGet(1, out var fixture);

        Assert.Single(fixture.Home.Lineup, player => player.Position == "GK");
        Assert.Single(fixture.Away.Lineup, player => player.Position == "GK");
    }
}
