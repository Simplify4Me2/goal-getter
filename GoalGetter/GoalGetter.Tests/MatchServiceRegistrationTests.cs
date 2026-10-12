using GoalGetter.ApiService.Matches;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace GoalGetter.Tests;

public class MatchServiceRegistrationTests
{
    [Fact]
    public void AddMatchServices_DefaultOptions_ResolvesPlaybackWithSpeedFactor1()
    {
        using var provider = BuildProvider();

        var playback = provider.GetRequiredService<IMatchPlayback>();
        var options = provider.GetRequiredService<IOptions<MatchPlaybackOptions>>();

        Assert.NotNull(playback);
        Assert.Equal(1.0, options.Value.SpeedFactor);
    }

    [Fact]
    public void AddMatchServices_SpeedFactorZero_ThrowsOptionsValidationException()
    {
        using var provider = BuildProvider(("MatchPlayback:SpeedFactor", "0"));
        var options = provider.GetRequiredService<IOptions<MatchPlaybackOptions>>();

        Assert.Throws<OptionsValidationException>(() => _ = options.Value);
    }

    [Fact]
    public void AddMatchServices_SpeedFactor601_ThrowsOptionsValidationException()
    {
        using var provider = BuildProvider(("MatchPlayback:SpeedFactor", "601"));
        var options = provider.GetRequiredService<IOptions<MatchPlaybackOptions>>();

        Assert.Throws<OptionsValidationException>(() => _ = options.Value);
    }

    [Fact]
    public void AddMatchServices_ResolvesPlayerTagger()
    {
        using var provider = BuildProvider();

        var tagger = provider.GetRequiredService<IPlayerTagger>();

        Assert.IsType<PlayerTagger>(tagger);
    }

    private static ServiceProvider BuildProvider(params (string Key, string? Value)[] settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings.ToDictionary(setting => setting.Key, setting => setting.Value))
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddMatchServices();

        return services.BuildServiceProvider();
    }
}
