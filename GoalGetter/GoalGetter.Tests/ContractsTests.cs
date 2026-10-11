using System.Text.Json;

namespace GoalGetter.Tests;

public class ContractsTests
{
    [Fact]
    public void MatchEvent_SerializesTypeAsString()
    {
        var matchEvent = CreateEvent(EventType.PossessionChange);

        var json = JsonSerializer.Serialize(matchEvent, JsonSerializerOptions.Web);

        Assert.Contains("\"type\":\"PossessionChange\"", json);
    }

    [Fact]
    public void MatchEvent_RoundTripsThroughJson()
    {
        var matchEvent = CreateEvent(EventType.Shot);

        var json = JsonSerializer.Serialize(matchEvent, JsonSerializerOptions.Web);
        var result = JsonSerializer.Deserialize<MatchEvent>(json, JsonSerializerOptions.Web);

        Assert.Equal(matchEvent, result);
    }

    private static MatchEvent CreateEvent(EventType type) =>
        new("1-0001", 1, 42, type, "HCF", 1, 52.5, 34.0);
}
