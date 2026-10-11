namespace San14.Data;

/// <summary>How Sky and Star draws combine their alpha with the current target.</summary>
public enum SkyAlphaBlendMode { Replace, Add, Maximum }

/// <summary>The Common object's policy needed by drawing. Null suppression
/// means that Common is known to be absent, rather than a present object with
/// an unknown policy.</summary>
public readonly record struct CommonRenderPolicy(bool? SuppressOceanDrawing, bool EnableSkyDrawing,
    SkyAlphaBlendMode SkyAlphaBlend)
{
    public static CommonRenderPolicy FromAuxiliary(CommonAuxiliaryState? auxiliary)
    {
        if (auxiliary is null) return new(null, false, SkyAlphaBlendMode.Maximum);
        return new(auxiliary.SuppressOceanDrawing ?? throw new NotSupportedException(
            "Present Common has no ocean drawing policy producer."), (auxiliary.Flags & 0x20u) != 0,
            auxiliary.SkyAlphaBlend ?? throw new NotSupportedException("Present Common has no sky alpha blend producer."));
    }
}
