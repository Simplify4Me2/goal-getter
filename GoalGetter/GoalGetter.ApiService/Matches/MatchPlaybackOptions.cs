using System.ComponentModel.DataAnnotations;

namespace GoalGetter.ApiService.Matches;

public sealed class MatchPlaybackOptions
{
    public const string SectionName = "MatchPlayback";

    // Bounded so a bad value fails at startup instead of producing a zero or negative delay.
    [Range(1.0, 600.0)]
    public double SpeedFactor { get; set; } = 1.0;
}
