namespace San14.Data;

/// <summary>Opcode2's actual post-bind rectangles. The backend uses the
/// primary surface extent when the serialized flag byte is nonzero; native
/// frontend depth endpoints and scissor enable remain current caller state.</summary>
public sealed record OriginalDeferredEarlyShadowTargetRectangles(
    OriginalWorldMainDisplayViewport Viewport, int ScissorX, int ScissorY,
    int ScissorWidth, int ScissorHeight, bool ScissorEnabled, string SourceEvidence);

public sealed class LoadedOriginalDeferredEarlyShadowTargetRectangleSource
{
    internal LoadedOriginalDeferredEarlyShadowTargetRectangleSource(string locale, string evidence)
        => (Locale, SourceEvidence) = (locale, evidence);
    public string Locale { get; }
    public string SourceEvidence { get; }

    /// <summary>The node17 Flag5 command supplies no serialized rectangles.
    /// Its real decoder writes XY=0 and WH=row50.surface50/54, then publishes
    /// viewport and scissor through the current device. This is not the later
    /// queue19 cascade atlas rectangle and does not claim a GPU submission.</summary>
    public OriginalDeferredEarlyShadowTargetRectangles PrepareNode17CurrentDomain(
        OriginalWorldGeometryBindNode node17, int actualPrimarySurfaceWidth, int actualPrimarySurfaceHeight,
        OriginalWorldMainDisplayViewport currentFrontendViewport, bool currentScissorEnabled)
    {
        ArgumentNullException.ThrowIfNull(node17);
        if (node17.NodeIndex != 17 || node17.SourceFlag != 5 || node17.CpuFlags20 != 0xC0000000 ||
            node17.Colors.Count != 0 || node17.Depth.ObjectNameHash != 0xEA36C27D)
            throw new InvalidDataException("This rectangle source requires the original depth-only node17 Flag5 command.");
        if (actualPrimarySurfaceWidth <= 0 || actualPrimarySurfaceHeight <= 0)
            throw new InvalidDataException("Node17 needs its actual primary surface extent.");
        return new(currentFrontendViewport with
        {
            X = 0, Y = 0, Width = actualPrimarySurfaceWidth, Height = actualPrimarySurfaceHeight
        }, 0, 0, actualPrimarySurfaceWidth, actualPrimarySurfaceHeight,
            currentScissorEnabled, SourceEvidence);
    }
}

public static class OriginalDeferredEarlyShadowTargetRectangleSource
{
    public static LoadedOriginalDeferredEarlyShadowTargetRectangleSource Load(OriginalGameRepository repository, string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return OriginalDeferredSamplerSources.LoadEarlyShadowTargetRectangleSource(repository.SourceDirectory, locale);
    }
}
