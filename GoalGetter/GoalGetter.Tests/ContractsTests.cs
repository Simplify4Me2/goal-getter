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

    [Fact]
    public void TaggedMatchEvent_SerializesNamesAndTypeAsString()
    {
        var taggedEvent = CreateTaggedEvent();

        var json = JsonSerializer.Serialize(taggedEvent, JsonSerializerOptions.Web);

        Assert.Contains("\"type\":\"Pass\"", json);
        Assert.Contains("\"playerName\":\"Tomas Brandt\"", json);
        Assert.Contains("\"teamName\":\"Harbor City FC\"", json);
    }

    [Fact]
    public void TaggedMatchEvent_PayloadDeserializesAsMatchEvent()
    {
        var taggedEvent = CreateTaggedEvent();

        var json = JsonSerializer.Serialize(taggedEvent, JsonSerializerOptions.Web);
        var result = JsonSerializer.Deserialize<MatchEvent>(json, JsonSerializerOptions.Web);

        Assert.Equal(new MatchEvent("1-0001", 1, 42, EventType.Pass, "HCF", 1, 52.5, 34.0), result);
    }

    private static MatchEvent CreateEvent(EventType type) =>
        new("1-0001", 1, 42, type, "HCF", 1, 52.5, 34.0);

    private static TaggedMatchEvent CreateTaggedEvent() =>
        new("1-0001", 1, 42, EventType.Pass, "HCF", "Harbor City FC", 1, "Tomas Brandt", 52.5, 34.0);
}
