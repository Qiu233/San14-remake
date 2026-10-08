namespace San14.Data;

/// <summary>EBCD70's actual per-group frontend modifications. Viewport,
/// scissor, depth endpoints and depth comparison remain the current caller's
/// actual values. No target, depth transport or GPU submission is supplied.</summary>
public sealed class OriginalDeferredEarlyIndirectRasterRecipe
{
    internal OriginalDeferredEarlyIndirectRasterRecipe(LoadedOriginalDeferredEarlyIndirectSource source,
        OriginalDeferredEarlyIndirectCurrentGroup group)
        => (Source, Group) = (source, group);
    public LoadedOriginalDeferredEarlyIndirectSource Source { get; }
    public OriginalDeferredEarlyIndirectCurrentGroup Group { get; }
    public uint RenderTarget0WriteMask => Source.ColorWriteMask;
    public bool DepthTest => false;
    public bool DepthWrite => false;
    public bool AlphaTest => false;
    public bool BlendEnabled => false;
    public uint SourceCullMode => 1;
    public bool StencilEnabled => true;
    public uint NativeStencilCompareCode => Group.NativeStencilCompareCode;
    public uint StencilReference => Group.StencilReference;
    public uint StencilReadMask => Group.StencilReadMask;
    public bool StencilFailKeeps => true;
    public bool StencilDepthFailKeeps => true;
    public bool StencilPassKeeps => true;
    public bool NativeStencilWriteMaskRetained => true;
    public string SourceEvidence => Source.SourceEvidence +
        ";E77220-graph23-loader/E69C70-338-colorMask-shift17;node68-59BD6FC4=8;" +
        "EBD077-counted-blend-off/EC6F10-RT0-alpha;EBD0EB-write-off/EBD110-alphaTest-off;" +
        "EBD4D8-E726E0-group-stencil;EBD61F-depthTest-off/EBD644-sourceCull1;viewport/scissor-current-caller";
    public uint SourcePassExitRenderTarget0WriteMask => 15;
    public bool SourcePassExitStencilEnabled => false;
}

public static class OriginalDeferredEarlyIndirectRasterSource
{
    public static OriginalDeferredEarlyIndirectRasterRecipe Prepare(
        LoadedOriginalDeferredEarlyIndirectSource source, OriginalDeferredEarlyIndirectCurrentRecord record,
        OriginalDeferredEarlyIndirectCurrentGroup actualGroup)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(record);
        if (source.SourceProcessMode != 0 || source.ColorWriteMask != 8 || !record.Groups.Any(g => ReferenceEquals(g, actualGroup)))
            throw new InvalidDataException("Early68 raster requires its actual ordinary source group and alpha target-mask writer.");
        return new(source, actualGroup);
    }
}
