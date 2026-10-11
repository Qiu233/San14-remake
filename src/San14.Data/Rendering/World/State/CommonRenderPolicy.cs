namespace San14.Data;

/// <summary>The Common object's policy needed by drawing. Null suppression
/// means that Common is known to be absent, rather than a present object with
/// an unknown policy.</summary>
public readonly record struct CommonRenderPolicy(bool? SuppressOceanDrawing)
{
    public static CommonRenderPolicy FromAuxiliary(CommonAuxiliaryState? auxiliary)
    {
        if (auxiliary is null) return default;
        return new(auxiliary.SuppressOceanDrawing ?? throw new NotSupportedException(
            "Present Common has no ocean drawing policy producer."));
    }
}
