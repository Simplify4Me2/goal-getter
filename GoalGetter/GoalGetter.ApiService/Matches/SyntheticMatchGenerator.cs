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
