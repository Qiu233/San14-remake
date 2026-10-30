namespace San14.Data;

/// <summary>The audio library's cue-selection stream. Its lifetime belongs to
/// the playback service, independently of a campaign or a map scene.</summary>
public sealed class AudioCueSelectionRandom
{
    private ulong state = 1;

    private int Next15()
    {
        state = unchecked(state * 0x343FDul + 0x269EC3ul);
        return (int)((state >> 16) & 0x7FFF);
    }

    internal int SelectUniformChild(int count)
        => (int)((Next15() * (1f / 32768f)) * count);

    internal float ReadSingleContinuousRange(float origin, float width)
    {
        // The source selects a range even when its table has only one entry.
        _ = Next15();
        if (width <= 0) return origin;
        float fraction = Next15() / 32767f;
        float end = width + origin;
        float extent = end - origin;
        return fraction * extent + origin;
    }
}
