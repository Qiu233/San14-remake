using San14.Data.Formats;

namespace San14.Data;

/// <summary>Bank12's real texture-pattern clip, independent of its portrait
/// material and of the caller's animation admission. The source repeat clock
/// uses an inclusive final frame, float32 updates and the authored selector.</summary>
public sealed class StandingPortraitFrameAnimation
{
    public const int FrameCount = 30;
    public LcskAnimationClip Clip { get; }
    public float Frame => playback.Frame;
    private readonly LcskPositivePlayback playback;
    private readonly LcskAnimationCurve selector;

    private StandingPortraitFrameAnimation(LcskAnimationClip clip, LcskAnimationCurve selector)
    { Clip = clip; this.selector = selector; playback = new(clip, true, 1f); }

    public static StandingPortraitFrameAnimation Read(LcskDocument layout, int sourceNode)
    {
        ArgumentNullException.ThrowIfNull(layout);
        var clip = LcskAnimationReader.Read(layout, new HashSet<string> { "parapara_15" })
            .Single(clip => clip.Name == "parapara_15");
        var track = clip.Tracks.Single(track => track.NodeId == sourceNode && track.Channel == 51);
        if (clip.EndFrame - clip.StartFrame < 2 || track.Curve.Keys.Count == 0)
            throw new InvalidDataException("Standing portrait requires its authored repeating texture-pattern clip.");
        return new(clip, track.Curve);
    }

    public int SpriteIndex
    {
        get
        {
            // AC8A90 truncates the double curve value before resolving its
            // texture-pattern slot; do not derive a uniform fifteen-FPS index.
            double value = selector.Sample(Frame);
            if (!double.IsFinite(value) || value < 0 || value >= FrameCount)
                throw new InvalidDataException("Standing portrait texture-pattern slot is outside the loaded bank12 frames.");
            return (int)(long)value;
        }
    }

    public void Advance(float seconds)
    {
        if (!float.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        playback.Advance(seconds);
    }

}
