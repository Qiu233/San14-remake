using San14.Data.Formats;

namespace San14.Data;

/// <summary>The finite set of clips consumed by the poem event.</summary>
public static class PoemEventAnimationReader
{
    private static readonly HashSet<string> Names =
        ["loop", "fade_out", "09_ev_p_01_all", "09_ev_p_01_all_jian", "txt_scrolling"];

    public static IReadOnlyList<LcskAnimationClip> Read(LcskDocument layout)
        => LcskAnimationReader.Read(layout, Names);
}
