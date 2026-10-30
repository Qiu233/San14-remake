using San14.Data.Formats;

namespace San14.Data;

public readonly record struct StandingPortraitPropertyValue(int NodeId, byte Channel, double Value);

/// <summary>Source standing-pose motion and material requests. Instances keep
/// their resource order, independently of the order in which clips restart.</summary>
public sealed class StandingPortraitMotion
{
    private sealed class Slot(LcskAnimationClip clip, LcskAnimationTrack[] tracks)
    {
        internal readonly LcskAnimationClip Clip = clip;
        internal readonly LcskAnimationTrack[] Tracks = tracks;
        internal LcskPositivePlayback? Playback;
        internal bool Dirty;
        internal bool Active => Playback?.IsPlaying == true;
    }
    private readonly Slot[] slots;
    private readonly List<StandingPortraitPropertyValue> writes = [];
    private readonly Dictionary<string, Slot> named;
    private readonly bool ordinary;
    private readonly int? appearance;
    public bool IsDark { get; private set; }
    public bool NeedsUpdate => slots.Any(slot => slot.Active) || IsDark;

    private StandingPortraitMotion(Slot[] slots, bool ordinary, int? appearance)
    { this.slots = slots; named = slots.ToDictionary(slot => slot.Clip.Name); this.ordinary = ordinary; this.appearance = appearance; }

    public static StandingPortraitMotion Read(LcskDocument source, IReadOnlyCollection<int> renderedNodes,
        bool ordinary, int? currentAppearance)
    {
        var wanted = new HashSet<string>(StringComparer.Ordinal) { "dark_in", "dark_loop", "dark_out" };
        if (ordinary) wanted.Add("normal_roop");
        var clips = LcskAnimationReader.Read(source, wanted);
        if (clips.Count != wanted.Count || clips.Select(clip => clip.Name).Distinct().Count() != wanted.Count)
            throw new InvalidDataException("standing.motion.source_clips");
        var nodes = renderedNodes.ToHashSet();
        // Reader retains descriptor order. Source loader creates the instance
        // array in that same order; neither Play nor Stop reorders it.
        var slots = clips.Select(clip =>
        {
            bool motion = clip.Name == "normal_roop";
            if (clip.Flags != (motion ? 1u : 8u)) throw new InvalidDataException("standing.motion.clip_property_mask");
            var tracks = clip.Tracks.Where(track => nodes.Contains(track.NodeId) && track.Curve.Keys.Count != 0 &&
                (motion ? track.Channel is 0 or 1 or 2 or 6 or 7 : track.Channel is >= 16 and <= 19)).ToArray();
            return new Slot(clip, tracks);
        }).ToArray();
        return new(slots, ordinary, currentAppearance);
    }

    public IReadOnlyList<StandingPortraitPropertyValue> SetAnimationEnabled(bool enabled)
    {
        if (!ordinary) return Array.Empty<StandingPortraitPropertyValue>();
        var slot = named["normal_roop"];
        if (enabled && appearance is int value && !BreathingExcluded(value))
        {
            if (!slot.Active) Play(slot, true, 0.5f);
            return Array.Empty<StandingPortraitPropertyValue>();
        }
        // Source disabled/exception paths seek the normal clip's authored
        // start and apply it; empty-key bindings still retain static values.
        slot.Playback = null; slot.Dirty = false;
        writes.Clear(); Sample(slot, slot.Clip.StartFrame, writes); return writes;
    }

    public IReadOnlyList<StandingPortraitPropertyValue> SetDark(bool dark, bool immediate = false)
    {
        var slot = named[dark ? "dark_in" : "dark_out"];
        if (immediate)
        {
            slot.Playback = null; slot.Dirty = false; IsDark = dark;
            writes.Clear(); Sample(slot, slot.Clip.EndFrame, writes); return writes;
        }
        if (IsDark == dark) return Array.Empty<StandingPortraitPropertyValue>();
        Play(slot, false, 1f);
        var loop = named["dark_loop"]; loop.Playback = null; loop.Dirty = false;
        // The opposite once clip is not stopped. If both are dirty, the fixed
        // resource instance order decides which material write is last.
        IsDark = dark;
        return Array.Empty<StandingPortraitPropertyValue>();
    }

    /// <summary>Returned writes borrow this view-owned buffer until its next
    /// operation. The renderer consumes them synchronously; no World is copied.</summary>
    public IReadOnlyList<StandingPortraitPropertyValue> Update(float elapsedSeconds)
    {
        if (!float.IsFinite(elapsedSeconds) || elapsedSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        writes.Clear();
        foreach (var slot in slots)
        {
            if (slot.Playback is not { IsPlaying: true } playback) continue;
            // Layout update applies the current dirty sample before advancing
            // its clock. Advancing first would display a future source frame.
            if (slot.Dirty) Sample(slot, playback.Frame, writes);
            slot.Dirty = playback.Advance(elapsedSeconds);
            if (!playback.IsPlaying) slot.Playback = null;
        }
        if (IsDark && !named["dark_in"].Active && !named["dark_loop"].Active)
            Play(named["dark_loop"], true, 1f);
        return writes;
    }

    private static void Play(Slot slot, bool repeat, float speed)
    { slot.Playback = new(slot.Clip, repeat, speed); slot.Dirty = true; }

    private static void Sample(Slot slot, float frame, List<StandingPortraitPropertyValue> output)
    {
        foreach (var track in slot.Tracks)
            output.Add(new(track.NodeId, track.Channel, track.Curve.Sample(frame)));
    }

    private static bool BreathingExcluded(int appearance) => appearance is
        275 or 364 or 431 or 563 or 564 or 565 or 914 or 2198 or 2199 || unchecked((uint)(appearance - 7000)) < 1000;
}
