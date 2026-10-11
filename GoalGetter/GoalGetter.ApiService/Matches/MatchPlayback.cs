using System.Runtime.CompilerServices;
using GoalGetter.Contracts.Matches;
using Microsoft.Extensions.Options;

namespace GoalGetter.ApiService.Matches;

public interface IMatchPlayback
{
    IAsyncEnumerable<MatchEvent> PlayAsync(Fixture fixture, CancellationToken cancellationToken);
}

public sealed class MatchPlayback(
    ISyntheticMatchGenerator generator,
    IOptions<MatchPlaybackOptions> options,
    TimeProvider timeProvider) : IMatchPlayback
{
    public async IAsyncEnumerable<MatchEvent> PlayAsync(
        Fixture fixture, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var speedFactor = options.Value.SpeedFactor;
        var startedAt = timeProvider.GetTimestamp();

        foreach (var matchEvent in generator.Generate(fixture))
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Measured from stream start, so a slow consumer does not push later events back.
            var due = TimeSpan.FromSeconds(matchEvent.ClockSeconds / speedFactor);
            var wait = due - timeProvider.GetElapsedTime(startedAt);
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, timeProvider, cancellationToken);
            }

            yield return matchEvent;
        }
    }
}
