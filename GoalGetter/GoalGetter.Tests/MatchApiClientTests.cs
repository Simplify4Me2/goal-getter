using System.Text;
using System.Text.Json;
using GoalGetter.Web;

namespace GoalGetter.Tests;

public class MatchApiClientTests
{
    [Fact]
    public async Task StreamEventsAsync_ParsesSseBody_YieldsEventsInOrder()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var sent = new[]
        {
            CreateEvent(10, "1-0001", EventType.Pass),
            CreateEvent(25, "1-0002", EventType.Shot),
            CreateEvent(40, "1-0003", EventType.PossessionChange),
        };
        var body = string.Concat(sent.Select(matchEvent =>
            $"event: matchEvent\ndata: {JsonSerializer.Serialize(matchEvent, JsonSerializerOptions.Web)}\n\n"));
        using var httpClient = CreateHttpClient(HttpStatusCode.OK, body, "text/event-stream");
        var client = new MatchApiClient(httpClient);

        // Act
        var received = await ReadEventsAsync(client, 1, cancellationToken);

        // Assert
        Assert.Equal(sent, received);
    }

    [Fact]
    public async Task StreamEventsAsync_NotFound_ThrowsHttpRequestException()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        using var httpClient = CreateHttpClient(HttpStatusCode.NotFound, string.Empty, "text/plain");
        var client = new MatchApiClient(httpClient);

        // Act and Assert
        await Assert.ThrowsAsync<HttpRequestException>(() => ReadEventsAsync(client, 999, cancellationToken));
    }

    [Fact]
    public async Task GetFixtureAsync_ReturnsDeserializedFixture()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var fixture = new Fixture(
            1,
            new DateTimeOffset(2026, 10, 11, 15, 0, 0, TimeSpan.Zero),
            CreateTeam("HCF", "Harbor City FC", firstPlayerId: 1),
            CreateTeam("NGR", "Northgate Rovers", firstPlayerId: 12));
        var body = JsonSerializer.Serialize(fixture, JsonSerializerOptions.Web);
        using var httpClient = CreateHttpClient(HttpStatusCode.OK, body, "application/json");
        var client = new MatchApiClient(httpClient);

        // Act
        var result = await client.GetFixtureAsync(1, cancellationToken);

        // Assert
        Assert.Equal(("Harbor City FC", 11), (result?.Home.Name, result?.Home.Lineup.Count));
    }

    private static MatchEvent CreateEvent(int clockSeconds, string id, EventType type) =>
        new(id, 1, clockSeconds, type, "HCF", 1, 50, 34);

    private static Team CreateTeam(string id, string name, int firstPlayerId) =>
        new(id, name, Enumerable.Range(0, 11)
            .Select(index => new Player(firstPlayerId + index, $"{name} {index + 1}", id, index + 1, "CM"))
            .ToArray());

    private static HttpClient CreateHttpClient(HttpStatusCode statusCode, string body, string contentType) =>
        new(new StubHandler(statusCode, body, contentType)) { BaseAddress = new Uri("http://apiservice") };

    private static async Task<List<MatchEvent>> ReadEventsAsync(
        MatchApiClient client, int matchId, CancellationToken cancellationToken)
    {
        var events = new List<MatchEvent>();
        await foreach (var matchEvent in client.StreamEventsAsync(matchId, cancellationToken))
        {
            events.Add(matchEvent);
        }
        return events;
    }

    private sealed class StubHandler(HttpStatusCode statusCode, string body, string contentType) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, contentType),
            });
    }
}
