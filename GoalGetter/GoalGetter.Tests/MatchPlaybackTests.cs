using GoalGetter.ApiService.Matches;
using GoalGetter.Contracts.Matches;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace GoalGetter.Tests;

public class MatchPlaybackTests
{
    private readonly FakeTimeProvider timeProvider = new();
    private readonly Fixture fixture = LoadMatchOne();
    private readonly MatchEvent[] events =
    [
        Event("1-0001", 10),
        Event("1-0002", 25),
        Event("1-0003", 25),
    ];

    [Fact]
    public async Task PlayAsync_FirstEvent_IsNotDeliveredBeforeItsClockTime()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var enumerator = CreatePlayback(1.0).PlayAsync(fixture, cancellationToken).GetAsyncEnumerator(cancellationToken);
        var delivery = enumerator.MoveNextAsync().AsTask();

        timeProvider.Advance(TimeSpan.FromSeconds(9));
        Assert.False(delivery.IsCompleted);

        timeProvider.Advance(TimeSpan.FromSeconds(1));
        Assert.True(await delivery.WaitAsync(RealTimeout, cancellationToken));
    }

    [Fact]
    public async Task PlayAsync_EventsSharingASecond_AreDeliveredTogetherInOrder()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var enumerator = CreatePlayback(1.0).PlayAsync(fixture, cancellationToken).GetAsyncEnumerator(cancellationToken);
        var firstDelivery = NextEventAsync(enumerator, cancellationToken);

        timeProvider.Advance(TimeSpan.FromSeconds(25));
        await firstDelivery;
        var second = await NextEventAsync(enumerator, cancellationToken);
        var third = await NextEventAsync(enumerator, cancellationToken);

        Assert.Equal(events.Skip(1), new[] { second, third });
    }

    [Fact]
    public async Task PlayAsync_SpeedFactor60_DividesDelayBy60()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var enumerator = CreatePlayback(60.0).PlayAsync(fixture, cancellationToken).GetAsyncEnumerator(cancellationToken);
        var delivery = enumerator.MoveNextAsync().AsTask();

        timeProvider.Advance(TimeSpan.FromSeconds(1.0 / 6));
        await delivery.WaitAsync(RealTimeout, cancellationToken);

        Assert.Equal(events[0], enumerator.Current);
    }

    [Fact]
    public async Task PlayAsync_SlowConsumer_DoesNotDelayLaterEvents()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var enumerator = CreatePlayback(1.0).PlayAsync(fixture, cancellationToken).GetAsyncEnumerator(cancellationToken);
        var firstDelivery = enumerator.MoveNextAsync().AsTask();

        timeProvider.Advance(TimeSpan.FromSeconds(30));
        await firstDelivery.WaitAsync(RealTimeout, cancellationToken);
        var second = await NextEventAsync(enumerator, cancellationToken);
        var third = await NextEventAsync(enumerator, cancellationToken);

        Assert.Equal(events.Skip(1), new[] { second, third });
    }

    [Fact]
    public async Task PlayAsync_Cancelled_StopsWithoutDeliveringFurtherEvents()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await using var enumerator = CreatePlayback(1.0).PlayAsync(fixture, cancellation.Token).GetAsyncEnumerator(cancellation.Token);
        var delivery = enumerator.MoveNextAsync().AsTask();

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => delivery);
    }

    private static TimeSpan RealTimeout => TimeSpan.FromSeconds(5);

    private MatchPlayback CreatePlayback(double speedFactor) => new(
        new StubGenerator(events),
        Options.Create(new MatchPlaybackOptions { SpeedFactor = speedFactor }),
        timeProvider);

    private static async Task<MatchEvent> NextEventAsync(IAsyncEnumerator<MatchEvent> enumerator, CancellationToken cancellationToken)
    {
        await enumerator.MoveNextAsync().AsTask().WaitAsync(RealTimeout, cancellationToken);
        return enumerator.Current;
    }

    private static MatchEvent Event(string id, int clockSeconds) =>
        new(id, 1, clockSeconds, EventType.Pass, "HCF", 1, 50, 34);

    private static Fixture LoadMatchOne()
    {
        new InMemoryFixtureCatalog().TryGet(1, out var fixture);
        return fixture;
    }

    private sealed class StubGenerator(IReadOnlyList<MatchEvent> events) : ISyntheticMatchGenerator
    {
        public IReadOnlyList<MatchEvent> Generate(Fixture fixture) => events;
    }
}
