namespace San14.Data;

/// <summary>The actual Q80 Sky CPU returned by Queue+50/Host.v1A8. These are
/// current CPU values and nullable wrapper references, rather than STA tables
/// or GPU completeness. Matrix180 is read only when the selected wrapper exists.</summary>
public sealed record OriginalDeferredEarlyIndirectPatternSkyPalette(uint PackedSelector168,
    IReadOnlyList<object?> Wrappers40, ReadOnlyMemory<byte> WorldToPatternMatrix180);

/// <summary>The caller's actual current Host/Sky owner. Unsupported production
/// must throw; null represents its actual v1A8 result, never an absent adapter.</summary>
public interface IOriginalDeferredEarlyIndirectCurrentPatternSky
{
    void RequireCurrent(OriginalMapSourceCreatedF103CurrentFrame actualGeometry);
    OriginalDeferredEarlyIndirectPatternSkyPalette? ReadCurrentSkyPalette(
        OriginalMapSourceCreatedF103CurrentFrame actualGeometry);
    string SourceEvidence { get; }
}

public static class OriginalDeferredEarlyIndirectPatternSpecialSource
{
    public static bool UsesSpecialBranch(OriginalDeferredEarlyIndirectPatternCameraTail actualTail,
        IOriginalDeferredEarlyIndirectCurrentPatternResources actualResources)
    {
        ArgumentNullException.ThrowIfNull(actualTail); ArgumentNullException.ThrowIfNull(actualResources);
        actualTail.RequireCurrent(actualTail.Frame); actualResources.RequireCurrent(actualTail.Frame);
        return actualResources.PatternMap1680Present && actualTail.CameraKind3Ac != 5;
    }
    internal static OriginalSkyAtmoTableCpuOwner RequireActualSta(
        OriginalMapSourceCreatedF103CurrentFrame actualGeometry,
        IOriginalDeferredEarlyIndirectCurrentPatternResources actualResources)
    {
        actualResources.RequireCurrent(actualGeometry);
        var actual = actualResources.PatternMap1680StaData ??
            throw new InvalidDataException("Special PatternMap requires the actual ready named STA CPU, not a presence Boolean.");
        actual.RequireCurrent();
        if (actual.Source.Locale != actualGeometry.Camera.ExecutableLocale)
            throw new InvalidDataException("Current STA CPU and prepared Camera use different native profiles.");
        return actual;
    }
}
