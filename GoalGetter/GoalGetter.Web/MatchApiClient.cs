using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text.Json;
using GoalGetter.Contracts.Matches;

namespace GoalGetter.Web;

public class MatchApiClient(HttpClient httpClient)
{
    // Returns null only when the API responds with a JSON null body.
    public Task<Fixture?> GetFixtureAsync(int matchId, CancellationToken cancellationToken = default) =>
        httpClient.GetFromJsonAsync<Fixture>($"/matches/{matchId}/fixture", cancellationToken);

    public async IAsyncEnumerable<MatchEvent> StreamEventsAsync(
        int matchId, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/matches/{matchId}/events/stream");
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var parser = SseParser.Create(stream, (_, data) => JsonSerializer.Deserialize<MatchEvent>(data, JsonSerializerOptions.Web)!);

        await foreach (var item in parser.EnumerateAsync(cancellationToken))
        {
            yield return item.Data;
        }
    }
}
