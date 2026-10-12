using GoalGetter.ApiService.Matches;
using GoalGetter.Contracts.Matches;
using Microsoft.Extensions.Logging.Abstractions;

namespace GoalGetter.Tests;

public class PlayerTaggerTests
{
    private const string HomeTeamId = "HCF";
    private const string AwayTeamId = "NGR";

    [Fact]
    public void Tag_HomePlayer_ReturnsPlayerNameAndTeamName()
    {
        var fixture = GetMatchOneFixture();

        var result = CreateTagger().Tag(fixture, CreateEvent(HomeTeamId, 1));

        Assert.Equal("Tomas Brandt", result.PlayerName);
        Assert.Equal("Harbor City FC", result.TeamName);
    }

    [Fact]
    public void Tag_AwayPlayer_ReturnsPlayerNameAndTeamName()
    {
        var fixture = GetMatchOneFixture();

        var result = CreateTagger().Tag(fixture, CreateEvent(AwayTeamId, 12));

        Assert.Equal("Pavel Kral", result.PlayerName);
        Assert.Equal("Northgate Rovers", result.TeamName);
    }

    [Fact]
    public void Tag_KeepsAllSourceEventFields()
    {
        var fixture = GetMatchOneFixture();
        var matchEvent = new MatchEvent("1-0042", 1, 1234, EventType.Shot, HomeTeamId, 1, 10.5, 60.0);

        var result = CreateTagger().Tag(fixture, matchEvent);

        Assert.Equal(
            new TaggedMatchEvent("1-0042", 1, 1234, EventType.Shot, HomeTeamId, "Harbor City FC", 1, "Tomas Brandt", 10.5, 60.0),
            result);
    }

    [Fact]
    public void Tag_PlayerIdNotInAnyLineup_ReturnsUnknownPlayerWithTeamName()
    {
        var fixture = GetMatchOneFixture();

        var result = CreateTagger().Tag(fixture, CreateEvent(HomeTeamId, 999));

        Assert.Equal(PlayerTagger.UnknownPlayerName, result.PlayerName);
        Assert.Equal("Harbor City FC", result.TeamName);
    }

    [Fact]
    public void Tag_PlayerFromOtherTeam_ReturnsUnknownPlayer()
    {
        var fixture = GetMatchOneFixture();

        var result = CreateTagger().Tag(fixture, CreateEvent(HomeTeamId, 12));

        Assert.Equal(PlayerTagger.UnknownPlayerName, result.PlayerName);
    }

    [Fact]
    public void Tag_UnknownTeam_ReturnsUnknownTeamAndUnknownPlayer()
    {
        var fixture = GetMatchOneFixture();

        var result = CreateTagger().Tag(fixture, CreateEvent("XXX", 1));

        Assert.Equal(PlayerTagger.UnknownTeamName, result.TeamName);
        Assert.Equal(PlayerTagger.UnknownPlayerName, result.PlayerName);
    }

    [Fact]
    public void Tag_AllGeneratedEventsOfMatch1_ResolveToKnownPlayerAndTeam()
    {
        var fixture = GetMatchOneFixture();
        var tagger = CreateTagger();
        var teams = new[] { fixture.Home, fixture.Away };

        var tagged = new SyntheticMatchGenerator().Generate(fixture)
            .Select(matchEvent => tagger.Tag(fixture, matchEvent))
            .ToList();

        Assert.NotEmpty(tagged);
        Assert.DoesNotContain(tagged, result =>
            result.PlayerName == PlayerTagger.UnknownPlayerName || result.TeamName == PlayerTagger.UnknownTeamName);
        Assert.All(tagged, result =>
        {
            var team = teams.Single(candidate => candidate.Id == result.TeamId);
            Assert.Contains(team.Lineup, player => player.Id == result.PlayerId && player.Name == result.PlayerName);
        });
    }

    private static Fixture GetMatchOneFixture()
    {
        Assert.True(new InMemoryFixtureCatalog().TryGet(1, out var fixture));
        return fixture;
    }

    private static PlayerTagger CreateTagger() => new(NullLogger<PlayerTagger>.Instance);

    private static MatchEvent CreateEvent(string teamId, int playerId) =>
        new("1-0001", 1, 42, EventType.Pass, teamId, playerId, 52.5, 34.0);
}
