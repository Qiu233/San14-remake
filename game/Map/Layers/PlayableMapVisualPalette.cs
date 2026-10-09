using Godot;

/// <summary>Provisional client colors shared by territory, bases and units.</summary>
public static class PlayableMapVisualPalette
{
    public static Color Force(int forceId, IReadOnlyDictionary<int, Color>? firstTableCandidates = null)
        => firstTableCandidates is not null &&
           firstTableCandidates.TryGetValue(forceId, out Color candidate)
            ? candidate : forceId == 28
        ? new Color("#eaa52f")
        : Color.FromHsv((forceId * 0.61803398875f) % 1f, 0.66f, 0.84f);
}
