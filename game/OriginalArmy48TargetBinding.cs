using Godot;
using San14.Data;

public sealed record OriginalArmy48NativeDrawDomain(float Left, float Top, float Width, float Height,
    float MinimumDepth, float MaximumDepth, OriginalFinalEffectScissor? ActualEnabledScissor,
    string SourceEvidence);

/// <summary>Caller-owned actual node48 framebuffer. Its current scope callback
/// must check the bound World/View/target lifetime, including the shared E13.
/// This adapter does not allocate, bind, clear or pop any attachment.</summary>
public sealed class OriginalArmy48TargetBinding
{
    private readonly Action requireActualCurrentScope;
    private readonly Action? requireActualRetainedBorrow;
    private OriginalArmy48TargetBinding(OriginalNativeMapFrameContext context, OriginalRenderTargetFrame frame,
        OriginalArmyWorldGraphicsTargetSource source, OriginalArmy48NativeDrawDomain domain, Action current, Action? retained)
        => (Context, Frame, Source, ActualDrawDomain, requireActualCurrentScope, requireActualRetainedBorrow) = (context, frame, source, domain, current, retained);
    public OriginalNativeMapFrameContext Context { get; }
    public OriginalRenderTargetFrame Frame { get; }
    public OriginalArmyWorldGraphicsTargetSource Source { get; }
    public OriginalArmy48NativeDrawDomain ActualDrawDomain { get; }

    public static OriginalArmy48TargetBinding CaptureActualBound(OriginalNativeMapFrameContext context,
        OriginalRenderTargetFrame actualFrame, OriginalArmyWorldGraphicsTargetSource acceptedSource,
        OriginalArmy48NativeDrawDomain actualPostBindingDrawDomain, Action requireActualCurrentScope,
        Action? requireActualCurrentWorldAndTargetLifetime = null)
    {
        ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(actualFrame);
        ArgumentNullException.ThrowIfNull(acceptedSource); ArgumentNullException.ThrowIfNull(requireActualCurrentScope);
        ArgumentNullException.ThrowIfNull(actualPostBindingDrawDomain);
        var value = new OriginalArmy48TargetBinding(context, actualFrame, acceptedSource, actualPostBindingDrawDomain,
            requireActualCurrentScope, requireActualCurrentWorldAndTargetLifetime);
        value.RequireCurrent(); return value;
    }
    public void RequireCurrent()
    {
        requireActualCurrentScope();
        RequireTargetStructure();
    }
    /// <summary>Successful stage acknowledgements remain usable after the
    /// source node52 attachment pop. The actual owner still checks the same
    /// current World and target resource lifetime, independently of binding.</summary>
    public void RequireRetainedTargetBorrow()
    {
        if (requireActualRetainedBorrow is null)
            throw new InvalidDataException("Army completion needs its actual retained World/target lifetime callback separately from current node48 binding.");
        requireActualRetainedBorrow(); RequireTargetStructure();
    }
    private void RequireTargetStructure()
    {
        if (Context.PreparedCamera is null || Context.FrameNumber <= 0 || Frame.ColorViews.Count != 6 ||
            !Frame.Framebuffer.IsValid || !Frame.DepthView.IsValid || Frame.Source.Node.Index != 48 ||
            Frame.Source.Graph.Source.ResourceUri != Source.Source.Graph.Source.ResourceUri ||
            Frame.Source.Graph.Source.DecodedSha256 != Source.Source.Graph.Source.DecodedSha256 ||
            Context.TerrainFrame.Source.Graph.Source.DecodedSha256 != Source.Source.Graph.Source.DecodedSha256 ||
            !Frame.Source.ColorTargets.Select(t => (t.Slot,t.ObjectNameHash,t.SurfaceId)).SequenceEqual(
                Source.Source.ColorTargets.Select(t => (t.Slot,t.ObjectNameHash,t.SurfaceId))) ||
            Frame.Source.DepthObjectNameHash != 0xE13DDB4F || Frame.Source.DepthSurfaceId != 0 ||
            !Frame.Textures.TryGetValue(0xE13DDB4F, out var armyDepth) ||
            !Context.TerrainFrame.Textures.TryGetValue(0xE13DDB4F, out var currentDepth) ||
            armyDepth.StorageRid != currentDepth.StorageRid || armyDepth.TargetFormat != RenderingDevice.DataFormat.D32SfloatS8Uint)
            throw new InvalidDataException("Army node48 must be the current actual six MRT framebuffer with this World's shared E13 depth/stencil.");
        Vector2I extent = Frame.Textures[Source.Source.ColorTargets[0].ObjectNameHash].ExplicitExtent;
        if (string.IsNullOrWhiteSpace(ActualDrawDomain.SourceEvidence) ||
            ActualDrawDomain is not { Left: 0, Top: 0, MinimumDepth: 0, MaximumDepth: 1, ActualEnabledScissor: null } ||
            ActualDrawDomain.Width != extent.X || ActualDrawDomain.Height != extent.Y)
            throw new NotSupportedException("Army device submission needs the actual source post-Bind whole-target viewport, depth range0..1 and disabled scissor; caller fields are never inferred from target dimensions.");
    }
}

public static class OriginalArmy48GraphicsContract
{
    /// <summary>Keep all six actual source color states. The device subpass
    /// uses each unchanged PS's actual output locations; inactive attachment
    /// states do not cause writes to an undeclared output.</summary>
    public static IReadOnlyList<OriginalModelBlendAttachment> PrepareAttachments(
        IReadOnlyList<OriginalModelBlendAttachment> actualNativeSixAttachments)
    {
        ArgumentNullException.ThrowIfNull(actualNativeSixAttachments);
        if (actualNativeSixAttachments.Count != 6 || actualNativeSixAttachments.Any(a => a is null))
            throw new InvalidDataException("Army node48 needs all six actual native color states.");
        return Array.AsReadOnly(actualNativeSixAttachments.ToArray());
    }
    internal static void RequireProgram(OriginalModelShaderProgram program)
    {
        if (program.Combination.Index != 1 || program.VertexProgramIndex != 1 || program.PixelProgramIndex != 2 ||
            program.ColorOutputLocations.Count==0 || program.ColorOutputLocations.Any(location=>location>=6) ||
            !((program.Source.Source.ResourceUri == GameResourceId.Rdb("system", 0xB90AEDD3).ToString() &&
                program.Source.Source.DecodedSha256 == OriginalArmySkinVertexAdapter.ShaderSha256) ||
              (program.Source.Source.ResourceUri == GameResourceId.Rdb("system", 0xA72FC040).ToString() &&
                program.Source.Source.DecodedSha256 == OriginalArmyFlagSkinVertexAdapter.ShaderSha256)))
            throw new InvalidDataException("Army six MRT source scope requires original B90/A72 combination1 VS1/PS2 and its actual outputs within0..5.");
    }
}
