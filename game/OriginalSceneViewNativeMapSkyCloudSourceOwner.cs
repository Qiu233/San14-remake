using San14.Data;

/// <summary>Concrete current source Cloud variant, complete seven source
/// texture consumers and current six complete native1193A90 CB packets.</summary>
public sealed class OriginalSceneViewNativeMapSkyCloudSourceOwner : IAsyncDisposable
{
    private readonly OriginalSkyAppliedSourceCloudOwner source;
    private readonly OriginalSceneViewNativeMapSkyCloudTextureOwner textures;
    private readonly Vkd3dShaderCompiler compiler;
    private OriginalSceneViewNativeMapSkyCloudPreparedFrame? current;
    private OriginalNativeMapFrameContext? currentContext;
    private (int Vertex,int Pixel)? allocatedProgram;
    public OriginalSkyCloudSourceClipPlane ClipSource => source.ClipSource;

    private OriginalSceneViewNativeMapSkyCloudSourceOwner(OriginalSkyAppliedSourceCloudOwner source,
        OriginalSceneViewNativeMapSkyCloudTextureOwner textures, Vkd3dShaderCompiler compiler)
    { this.source = source; this.textures = textures; this.compiler = compiler; }

    public static OriginalSceneViewNativeMapSkyCloudSourceOwner ReadSource(
        OriginalSceneViewNativeMapLoadInputs loaded, Vkd3dShaderCompiler compiler,
        OriginalSceneViewNativeMapSkyCurrentSelection bootstrap)
    {
        bootstrap.RequireCurrent();
        string locale = loaded.InitialFrame.MainCamera.Camera.ExecutableLocale;
        var groups = OriginalSkyAppliedSeasonCloudTextures.Load(loaded.Repository,
            bootstrap.Present.AcceptedSeasonCamera.Inputs.Source);
        var prototype = OriginalSkyAppliedSourceCloudSelectionOwner.CreateFresh(loaded.Repository, locale)
            .PrepareCurrent(bootstrap.Present, bootstrap.Stages, groups);
        return new(OriginalSkyAppliedSourceCloudOwner.CreateFresh(loaded.Repository, locale,
            bootstrap.Source.Lighting.Binding.ViewIdentity + "/fresh-owned-current-Cloud"),
            OriginalSceneViewNativeMapSkyCloudTextureOwner.ReadSource(prototype, groups), compiler);
    }

    public OriginalSceneViewNativeMapSkyCloudPreparedFrame PrepareBootstrap(
        OriginalSkySceneSourceFramePacket packet, OriginalMapSourceCreatedSeasonLightingFrame present,
        OriginalSkyAppliedSourceStageFrame stages, OriginalMainSceneNode51Execution execution,
        float explicitOwnedClockDelta)
    {
        currentContext = null;
        return Capture(source.PrepareBootstrap(packet, present, stages, execution, explicitOwnedClockDelta,
            ClockProvenance), packet, present);
    }

    public OriginalSceneViewNativeMapSkyCloudPreparedFrame PrepareCurrent(
        OriginalNativeMapFrameContext actualContext,
        OriginalMapOwnedGraphicsMaskPrefix actualPrefix,
        OriginalSkySceneSourceFramePacket packet, OriginalMapSourceCreatedSeasonLightingFrame present,
        OriginalSkyAppliedSourceStageFrame stages, OriginalMainSceneNode51Execution execution,
        float explicitOwnedClockDelta)
    {
        if (actualContext.FrameNumber != packet.FrameNumber || actualPrefix.FrameNumber != actualContext.FrameNumber)
            throw new InvalidDataException("Cloud clip source preparation requires the actual same-context source attempt.");
        source.BindCurrentClipScope(actualPrefix);
        var prepared = Capture(source.PrepareCurrent(packet, present, stages, execution, explicitOwnedClockDelta,
            ClockProvenance), packet, present);
        currentContext = actualContext;
        return prepared;
    }

    private const string ClockProvenance =
        "explicit-owned-SceneView-presentation-delta;not-original-Clock254;candidate-from-last-complete-Host";

    private OriginalSceneViewNativeMapSkyCloudPreparedFrame Capture(OriginalSkyAppliedSourceCloudFrame native,
        OriginalSkySceneSourceFramePacket packet, OriginalMapSourceCreatedSeasonLightingFrame present)
    {
        native.ValidateCandidate(packet, present);
        textures.ValidateCurrent(native.Selection);
        var selection=native.Selection;
        var expected=OriginalSkyCloudColorProgramSelection.ReadExplicitAppliedCombination(selection.Shader,selection.ColorCombination);
        if (native.SixPackets.Count != 6 || selection.ColorVertexProgram != expected.VertexProgram ||
            selection.ColorPixelProgram != expected.PixelProgram || selection.PixelConstantByteLength != expected.PixelConstantByteLength)
            throw new InvalidDataException("Cloud current packet differs from its actual selected complete WORK program.");
        if(allocatedProgram is { } allocated&&(allocated.Vertex!=selection.ColorVertexProgram||allocated.Pixel!=selection.ColorPixelProgram))
            throw new NotSupportedException("Cloud quality changed its original shader pair; retire and rebuild this owned Cloud producer before accepting the frame.");
        var packets = native.SixPackets.Select(p => new OriginalSkyCloudColorFacePacket(
            p.VertexBytes.ToArray(), p.PixelBytes.ToArray(), p.MaximumRaySamples)).ToArray();
        if (packets.Any(p => p.VertexConstants.Length != 160 || p.PixelConstants.Length != 512))
            throw new InvalidDataException("Cloud six faces require complete source VS160/PS512 packets.");
        return current = new(native, packet, present, Array.AsReadOnly(packets));
    }

    public async Task<OriginalSkyCloudColorProducer> BuildAsync(OriginalSkyScene scene,
        OriginalSceneViewNativeMapSkyCloudPreparedFrame prepared, uint actualColorWriteMask)
    {
        ValidateCandidate(prepared);
        var inputs = await textures.GetCurrentInputsAsync(prepared.Native.Selection);
        ValidateCandidate(prepared);
        var producer=await OriginalSkyCloudColorProducer.BuildAsync(prepared.Native.Selection.Shader,
            prepared.Native.Selection.ColorCombination, compiler, scene.PrimaryWork, inputs,
            prepared.SixPackets, actualColorWriteMask);
        try
        {
            ValidateCandidate(prepared);
            allocatedProgram=(prepared.Native.Selection.ColorVertexProgram,prepared.Native.Selection.ColorPixelProgram);
            return producer;
        }
        catch{await producer.DisposeAsync();throw;}
    }

    public void ValidateCandidate(OriginalSceneViewNativeMapSkyCloudPreparedFrame candidate)
    {
        if (!ReferenceEquals(current, candidate)) throw new InvalidDataException("Cloud candidate is stale or foreign.");
        candidate.Native.ValidateCandidate(candidate.SourceFrame, candidate.Present);
        textures.ValidateCurrent(candidate.Native.Selection);
    }

    public void CommitCompleteFrame(OriginalSceneViewNativeMapSkyCloudPreparedFrame candidate)
    { ValidateCandidate(candidate); source.CommitCompleteFrame(candidate.Native); }
    public OriginalSkyCloudSourceClipPlanePacket RequirePreparedClipPacket(OriginalNativeMapFrameContext actualContext)
    {
        if (!ReferenceEquals(currentContext, actualContext) || current is not { } actual ||
            actual.Native.Clip.IsConstructorPlan || actual.Native.Clip.FrameNumber != actualContext.FrameNumber ||
            actual.Native.Clip.ViewIdentity != actual.SourceFrame.Binding.ViewIdentity ||
            !ReferenceEquals(actual.Native.Clip.Source, source.ClipSource))
            throw new InvalidDataException("Cloud final clip acknowledgement requires its private actual current context and nonconstructor packet.");
        ValidateCandidate(actual);
        var clip = source.RequireCurrentClipPacket();
        if (!ReferenceEquals(clip, actual.Native.Clip))
            throw new InvalidDataException("Cloud current clip source packet changed during its actual Host attempt.");
        return clip;
    }
    public ValueTask DisposeAsync() => textures.DisposeAsync();
}

public sealed class OriginalSceneViewNativeMapSkyCloudPreparedFrame
{
    internal OriginalSceneViewNativeMapSkyCloudPreparedFrame(OriginalSkyAppliedSourceCloudFrame native,
        OriginalSkySceneSourceFramePacket source, OriginalMapSourceCreatedSeasonLightingFrame present,
        IReadOnlyList<OriginalSkyCloudColorFacePacket> sixPackets)
    { Native = native; SourceFrame = source; Present = present; SixPackets = sixPackets; }
    public OriginalSkyAppliedSourceCloudFrame Native { get; }
    public OriginalSkySceneSourceFramePacket SourceFrame { get; }
    public OriginalMapSourceCreatedSeasonLightingFrame Present { get; }
    public IReadOnlyList<OriginalSkyCloudColorFacePacket> SixPackets { get; }
}
