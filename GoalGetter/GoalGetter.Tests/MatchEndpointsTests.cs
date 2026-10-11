using System.Net.Http.Json;
using System.Net.ServerSentEvents;
using System.Text.Json;
using Aspire.Hosting;
using GoalGetter.ApiService.Endpoints;
using GoalGetter.Contracts.Matches;

namespace GoalGetter.Tests;

public class MatchEndpointsTests
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task GetFixture_Match1_ReturnsFixtureJson()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var app = await TestAppHost.StartAsync([], cancellationToken);
        var client = await CreateApiClientAsync(app, cancellationToken);

        var response = await client.GetAsync("/matches/1/fixture", cancellationToken);
        var fixture = await response.Content.ReadFromJsonAsync<Fixture>(JsonSerializerOptions.Web, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(fixture);
        Assert.Equal(11, fixture.Home.Lineup.Count);
        Assert.Equal(11, fixture.Away.Lineup.Count);
    }

    [Fact]
    public async Task GetFixture_UnknownMatch_ReturnsNotFound()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var app = await TestAppHost.StartAsync([], cancellationToken);
        var client = await CreateApiClientAsync(app, cancellationToken);

        var response = await client.GetAsync("/matches/999/fixture", cancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetFixture_ZeroId_ReturnsBadRequest()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var app = await TestAppHost.StartAsync([], cancellationToken);
        var client = await CreateApiClientAsync(app, cancellationToken);

        var response = await client.GetAsync("/matches/0/fixture", cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("Exception", body);
    }

    [Fact]
    public async Task StreamEvents_Match1_DeliversTenEventsInClockOrder()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var app = await TestAppHost.StartAsync(["MatchPlayback:SpeedFactor=600"], cancellationToken);
        var client = await CreateApiClientAsync(app, cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/matches/1/events/stream");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        var parser = SseParser.Create(stream, (_, data) => JsonSerializer.Deserialize<MatchEvent>(data, JsonSerializerOptions.Web)!);
        var items = new List<SseItem<MatchEvent>>();
        await foreach (var item in parser.EnumerateAsync(cancellationToken))
        {
            items.Add(item);
            if (items.Count == 10)
            {
                break;
            }
        }

        var clocks = items.Select(item => item.Data.ClockSeconds).ToArray();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
        Assert.All(items, item => Assert.Equal(MatchEndpoints.MatchEventName, item.EventType));
        Assert.Equal(clocks.Order(), clocks);
        Assert.Equal(Enumerable.Range(1, 10).Select(index => $"1-{index:D4}"), items.Select(item => item.Data.Id));
        Assert.All(items, item => Assert.Equal(item.Data.Id, item.EventId));
    }

    [Fact]
    public async Task StreamEvents_UnknownMatch_ReturnsNotFoundWithoutStream()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var app = await TestAppHost.StartAsync([], cancellationToken);
        var client = await CreateApiClientAsync(app, cancellationToken);

        var response = await client.GetAsync("/matches/999/events/stream", cancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotEqual("text/event-stream", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task StreamEvents_ZeroId_ReturnsBadRequest()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var app = await TestAppHost.StartAsync([], cancellationToken);
        var client = await CreateApiClientAsync(app, cancellationToken);

        var response = await client.GetAsync("/matches/0/events/stream", cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<HttpClient> CreateApiClientAsync(DistributedApplication app, CancellationToken cancellationToken)
    {
        await app.ResourceNotifications.WaitForResourceHealthyAsync("apiservice", cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);
        return app.CreateHttpClient("apiservice");
    }
}
