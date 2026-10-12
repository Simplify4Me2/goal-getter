using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GoalGetter.ApiService.Matches;

public static class MatchServiceCollectionExtensions
{
    // Validation runs at startup so an invalid SpeedFactor stops the host before it serves traffic.
    public static IServiceCollection AddMatchServices(this IServiceCollection services)
    {
        services.AddOptions<MatchPlaybackOptions>()
            .BindConfiguration(MatchPlaybackOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IFixtureCatalog, InMemoryFixtureCatalog>();
        services.AddSingleton<ISyntheticMatchGenerator, SyntheticMatchGenerator>();
        services.AddSingleton<IMatchPlayback, MatchPlayback>();
        services.AddSingleton<IPlayerTagger, PlayerTagger>();

        return services;
    }
}
