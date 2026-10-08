using Godot;
using San14.Data;

/// <summary>Device submission for one selected original node68 program.
/// Borrows the real node67 RS3/E13 frame and every current input. Selection,
/// input production and graph ordering remain with the normal source owner.
/// This component supports a full target viewport and the common deferred
/// no-depth / Equal-or-NotEqual Keep stencil recipe; it grants no source readiness.</summary>
public sealed class OriginalDeferredEarlyIndirectOwner : IAsyncDisposable
{
    private readonly OriginalDeferredDraw draw;
    private Action requireCurrentSource;
    private Task? disposal;
    public OriginalDeferredEarlyIndirectProgram Source { get; }
    public OriginalRenderTargetFrame Target { get; }
    public OriginalDeferredPixelProgram Program => draw.Program;
    public IReadOnlyList<OriginalDeferredDeviceBinding> Inputs => draw.Inputs;

    private OriginalDeferredEarlyIndirectOwner(OriginalDeferredEarlyIndirectProgram source,
        OriginalRenderTargetFrame target, OriginalDeferredDraw actualDraw, Action current)
        => (Source, Target, draw, requireCurrentSource) = (source, target, actualDraw, current);

    /// <summary>Translate the source's actual per-group stencil and raster
    /// writers. Target/input producers and inherited viewport remain explicit;
    /// this does not infer a group from the final F103 stencil convention.</summary>
    public static Task<OriginalDeferredEarlyIndirectOwner> BuildFromCurrentSourceGroupAsync(
        OriginalDeferredEarlyIndirectConstantPacket actualPacket,
        OriginalDeferredEarlyIndirectCurrentRecord actualRecord,
        OriginalDeferredEarlyIndirectCurrentGroup actualGroup,
        OriginalRenderTargetFrame actualNode67Frame,OriginalWorldGeometryDrawDomain actualDomain,
        IReadOnlyDictionary<uint,ReadOnlyMemory<byte>> actualAdditionalConstants,
        IReadOnlyList<OriginalDeferredDeviceBinding> actualInputs,
        Action requireActualCurrentSource,Vkd3dShaderCompiler compiler)
    {
        ArgumentNullException.ThrowIfNull(actualPacket);
        if(!ReferenceEquals(actualPacket.Camera.Frame,actualRecord.Frame))
            throw new InvalidDataException("Early Indirect group parameters belong to another current World frame.");
        var recipe=OriginalDeferredEarlyIndirectRasterSource.Prepare(actualPacket.Program.Owner,actualRecord,actualGroup);
        var stencil=new OriginalDeferredStencilState(recipe.StencilReference,recipe.StencilReadMask)
        {
            Compare=recipe.NativeStencilCompareCode switch
            {
                2=>RenderingDevice.CompareOperator.Equal,
                5=>RenderingDevice.CompareOperator.NotEqual,
                _=>throw new NotSupportedException("Early Indirect group needs its actual source stencil comparison.")
            }
        };
        return BuildAsync(actualPacket.Program,actualPacket,actualNode67Frame,actualDomain,actualAdditionalConstants,
            actualInputs,new(RenderingDevice.PolygonCullMode.Disabled,false),requireActualCurrentSource,compiler,stencil);
    }

    public static async Task<OriginalDeferredEarlyIndirectOwner> BuildAsync(
        OriginalDeferredEarlyIndirectProgram actualSelectedProgram,
        OriginalDeferredEarlyIndirectConstantPacket actualPacket,
        OriginalRenderTargetFrame actualNode67Frame, OriginalWorldGeometryDrawDomain actualDomain,
        IReadOnlyDictionary<uint, ReadOnlyMemory<byte>> actualAdditionalConstants,
        IReadOnlyList<OriginalDeferredDeviceBinding> actualInputs, OriginalDeferredRasterState actualRaster,
        Action requireActualCurrentSource, Vkd3dShaderCompiler compiler,
        OriginalDeferredStencilState? actualKeepStencil = null)
    {
        ArgumentNullException.ThrowIfNull(actualSelectedProgram);
        ArgumentNullException.ThrowIfNull(actualNode67Frame);
        ArgumentNullException.ThrowIfNull(actualDomain);
        ArgumentNullException.ThrowIfNull(requireActualCurrentSource);
        requireActualCurrentSource();
        actualSelectedProgram.Owner.RequireProgram(actualSelectedProgram);
        var constants = CaptureConstants(actualSelectedProgram, actualPacket, actualAdditionalConstants);
        var source = actualNode67Frame.Source;
        if (source.Node.Index != 67 || source.Node.Call.LookupKey != 0x325ADB59 ||
            source.Flag != 0x12 || source.ColorTargets.Count != 1 ||
            source.ColorTargets[0].ObjectNameHash != 0x521C7DED || source.ColorTargets[0].SurfaceId != 3 ||
            source.DepthObjectNameHash != 0xE13DDB4F || source.DepthSurfaceId != 0 ||
            source.Graph.Source.ResourceUri != actualSelectedProgram.Owner.Graph.Source.ResourceUri ||
            source.Graph.Source.DecodedSha256 != actualSelectedProgram.Owner.Graph.Source.DecodedSha256)
            throw new InvalidDataException("Early Indirect requires its actual node67 RS3/E13 target, not the final163 scope.");
        actualDomain.Validate();
        var extent = actualNode67Frame.Textures[0x521C7DED].ExplicitExtent;
        if (actualDomain.Left != 0 || actualDomain.Top != 0 || actualDomain.Width != extent.X ||
            actualDomain.Height != extent.Y || actualDomain.ScissorEnabled)
            throw new NotSupportedException("Early Indirect device component requires its actual full target viewport without scissor.");
        var program = await OriginalDeferredPixelProgram.BuildAsync(actualSelectedProgram.Shader,
            actualSelectedProgram.CombinationIndex, compiler);
        try
        {
            var target = new OriginalDeferredDrawTarget(actualNode67Frame.Framebuffer,
                actualNode67Frame.Textures.Values.Select(t => t.StorageRid).Distinct().ToArray(),
                source.Graph.Source.ResourceUri)
            { DepthStencilTexture = actualNode67Frame.Textures[0xE13DDB4F].StorageRid };
            var draw = await OriginalDeferredDraw.BuildAsync(program, target, constants,
                actualInputs, actualRaster, actualKeepStencil, OriginalDeferredColorWriteMask.AlphaOnly);
            return new(actualSelectedProgram, actualNode67Frame, draw, requireActualCurrentSource);
        }
        catch { await program.DisposeAsync(); throw; }
    }

    public async Task UpdateCurrentAsync(OriginalDeferredEarlyIndirectConstantPacket actualPacket,
        IReadOnlyDictionary<uint, ReadOnlyMemory<byte>> actualAdditionalConstants,
        Action requireActualCurrentSource)
    {
        ObjectDisposedException.ThrowIf(disposal is not null, this);
        ArgumentNullException.ThrowIfNull(requireActualCurrentSource);
        requireActualCurrentSource();
        var constants = CaptureConstants(Source, actualPacket, actualAdditionalConstants);
        await draw.UpdateConstantsAsync(constants);
        requireCurrentSource = requireActualCurrentSource;
    }

    /// <summary>Source owner calls only for its actual admitted group. The
    /// callback is checked on the rendering thread before any target write.</summary>
    public Task SubmitAsync()
    {
        ObjectDisposedException.ThrowIf(disposal is not null, this);
        return draw.DrawAsync(requireCurrentSource: requireCurrentSource);
    }

    private static IReadOnlyDictionary<uint, ReadOnlyMemory<byte>> CaptureConstants(OriginalDeferredEarlyIndirectProgram program,
        OriginalDeferredEarlyIndirectConstantPacket packet,
        IReadOnlyDictionary<uint, ReadOnlyMemory<byte>> constants)
    {
        ArgumentNullException.ThrowIfNull(packet); ArgumentNullException.ThrowIfNull(constants);
        if (!ReferenceEquals(packet.Program, program))
            throw new InvalidDataException("Early Indirect current packet belongs to another selected program.");
        packet.Camera.RequireCurrent(packet.Camera.Frame);
        var globals = program.PixelDefinitions.Bindings.Single(b => b.InputType == 0 && b.Name.Text == "$Globals");
        var complete = constants.ToDictionary(p => p.Key, p => p.Value);
        // The current packet is the globals input, so the caller cannot supply
        // a second conflicting copy which would need runtime fingerprinting.
        complete.Add(globals.BindPoint, packet.Bytes);
        return complete;
    }

    public ValueTask DisposeAsync() => new(disposal ??= DisposeCoreAsync());
    private async Task DisposeCoreAsync()
    {
        await draw.DisposeAsync();
        await Program.DisposeAsync();
    }
}
