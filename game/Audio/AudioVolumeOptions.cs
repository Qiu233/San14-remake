using System.Text.Json.Serialization;

/// <summary>The five independent source mixer gains, in integer percent.
/// A playback caller supplies its group explicitly; channel names and sample
/// names do not decide which preference controls a voice.</summary>
public enum AudioVolumeGroup { Music, Ambient, Effects, Voice, Movie }

public sealed record AudioVolumeOptions(
    [property: JsonRequired] int Music = 100,
    [property: JsonRequired] int Ambient = 100,
    [property: JsonRequired] int Effects = 100,
    [property: JsonRequired] int Voice = 100,
    [property: JsonRequired] int Movie = 100)
{
    internal void Validate()
    {
        if (Music is < 0 or > 100 || Ambient is < 0 or > 100 || Effects is < 0 or > 100 ||
            Voice is < 0 or > 100 || Movie is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(AudioVolumeOptions));
    }

    internal int Percent(AudioVolumeGroup group) => group switch
    {
        AudioVolumeGroup.Music => Music, AudioVolumeGroup.Ambient => Ambient,
        AudioVolumeGroup.Effects => Effects, AudioVolumeGroup.Voice => Voice,
        AudioVolumeGroup.Movie => Movie, _ => throw new ArgumentOutOfRangeException(nameof(group))
    };

    internal AudioVolumeOptions WithPercent(AudioVolumeGroup group, int percent)
    {
        if (percent is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(percent));
        return group switch
        {
            AudioVolumeGroup.Music => this with { Music = percent },
            AudioVolumeGroup.Ambient => this with { Ambient = percent },
            AudioVolumeGroup.Effects => this with { Effects = percent },
            AudioVolumeGroup.Voice => this with { Voice = percent },
            AudioVolumeGroup.Movie => this with { Movie = percent },
            _ => throw new ArgumentOutOfRangeException(nameof(group))
        };
    }
}
