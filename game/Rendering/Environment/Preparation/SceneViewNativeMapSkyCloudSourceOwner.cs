using San14.Data;

/// <summary>Concrete current source Cloud variant, complete seven source
/// texture consumers and current six complete native1193A90 CB packets.</summary>
public sealed class SceneViewNativeMapSkyCloudSourceOwner : IAsyncDisposable
{
    private readonly SkyAppliedSourceCloudOwner source;
    private readonly SceneViewNativeMapSkyCloudTextureOwner textures;
    private readonly Vkd3dShaderCompiler compiler;
    private SceneViewNativeMapSkyCloudPreparedFrame? current;
    private NativeMapFrameContext? currentContext;
    private (int Vertex,int Pixel)? allocatedProgram;
    public SkyCloudSourceClipPlane ClipSource => source.ClipSource;

    private SceneViewNativeMapSkyCloudSourceOwner(SkyAppliedSourceCloudOwner source,
        SceneViewNativeMapSkyCloudTextureOwner textures, Vkd3dShaderCompiler compiler)
    { this.source = source; this.textures = textures; this.compiler = compiler; }

    public static SceneViewNativeMapSkyCloudSourceOwner ReadSource(
        SceneViewNativeMapLoadInputs loaded, Vkd3dShaderCompiler compiler,
        SceneViewNativeMapSkyCurrentSelection bootstrap)
    {
        bootstrap.RequireCurrent();
        string locale = loaded.InitialFrame.MainCamera.Camera.ExecutableLocale;
        var programs = SkyCloudProgramResources.Load(loaded.Repository);
        var groups = bootstrap.Present.AcceptedSeasonCamera.Inputs.Source.CloudTextures;
        var prototype = SkyAppliedSourceCloudSelectionOwner.CreateFresh(loaded.Repository, locale, programs)
            .PrepareCurrent(bootstrap.Present, bootstrap.Stages, groups);
        return new(SkyAppliedSourceCloudOwner.CreateFresh(loaded.Repository, locale,
            bootstrap.Source.Lighting.Binding.ViewIdentity + "/fresh-owned-current-Cloud", programs),
            SceneViewNativeMapSkyCloudTextureOwner.ReadSource(prototype, groups), compiler);
    }

    public SceneViewNativeMapSkyCloudPreparedFrame PrepareBootstrap(
        SkySceneSourceFramePacket packet, MapSourceCreatedSeasonLightingFrame present,
        SkyAppliedSourceStageFrame stages, MainSceneNode51Execution execution,
        float explicitOwnedClockDelta)
    {
        currentContext = null;
        return Capture(source.PrepareBootstrap(packet, present, stages, execution, explicitOwnedClockDelta,
            ClockProvenance), packet, present);
    }

    public SceneViewNativeMapSkyCloudPreparedFrame PrepareCurrent(
        NativeMapFrameContext actualContext,
        MapOwnedGraphicsMaskPrefix actualPrefix,
        SkySceneSourceFramePacket packet, MapSourceCreatedSeasonLightingFrame present,
        SkyAppliedSourceStageFrame stages, MainSceneNode51Execution execution,
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

    private SceneViewNativeMapSkyCloudPreparedFrame Capture(SkyAppliedSourceCloudFrame native,
        SkySceneSourceFramePacket packet, MapSourceCreatedSeasonLightingFrame present)
    {
        native.ValidateCandidate(packet, present);
        textures.ValidateCurrent(native.Selection);
        var selection=native.Selection;
        if (native.SixPackets.Count != 6)
            throw new InvalidDataException("Cloud current packet requires all six WORK faces.");
        if(allocatedProgram is { } allocated&&(allocated.Vertex!=selection.ColorVertexProgram||allocated.Pixel!=selection.ColorPixelProgram))
            throw new NotSupportedException("Cloud quality changed its original shader pair; retire and rebuild this owned Cloud producer before accepting the frame.");
        var packets = native.SixPackets.Select(p => new SkyCloudColorFacePacket(
            p.VertexBytes.ToArray(), p.PixelBytes.ToArray(), p.MaximumRaySamples)).ToArray();
        if (packets.Any(p => p.VertexConstants.Length != 160 || p.PixelConstants.Length != 512))
            throw new InvalidDataException("Cloud six faces require complete source VS160/PS512 packets.");
        return current = new(native, packet, present, Array.AsReadOnly(packets));
    }

    public async Task<SkyCloudColorProducer> BuildAsync(SkyScene scene,
        SceneViewNativeMapSkyCloudPreparedFrame prepared, uint actualColorWriteMask)
    {
        ValidateCandidate(prepared);
        var inputs = await textures.GetCurrentInputsAsync(prepared.Native.Selection);
        ValidateCandidate(prepared);
        var producer=await SkyCloudColorProducer.BuildAsync(prepared.Native.Selection.ProgramSelection,
            compiler, scene.PrimaryWork, inputs,
            prepared.SixPackets, actualColorWriteMask);
        try
        {
            ValidateCandidate(prepared);
            allocatedProgram=(prepared.Native.Selection.ColorVertexProgram,prepared.Native.Selection.ColorPixelProgram);
            return producer;
        }
        catch(Exception failure)
        {
            var partial=new NativeMapBorrowerRetirementPackage("Cloud-source-partial/"+Guid.NewGuid().ToString("N"),producer);
            try{await partial.DisposeAsync();}
            catch(Exception cleanup){partial.RetainFailure();throw new NativeMapBorrowerConstructionException(failure,cleanup,partial);}
            throw;
        }
    }

    public void ValidateCandidate(SceneViewNativeMapSkyCloudPreparedFrame candidate)
    {
        if (!ReferenceEquals(current, candidate)) throw new InvalidDataException("Cloud candidate is stale or foreign.");
        candidate.Native.ValidateCandidate(candidate.SourceFrame, candidate.Present);
        textures.ValidateCurrent(candidate.Native.Selection);
    }

    public void CommitCompleteFrame(SceneViewNativeMapSkyCloudPreparedFrame candidate)
    { ValidateCandidate(candidate); source.CommitCompleteFrame(candidate.Native); }
    public SkyCloudSourceClipPlanePacket RequirePreparedClipPacket(NativeMapFrameContext actualContext)
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

public sealed class SceneViewNativeMapSkyCloudPreparedFrame
{
    internal SceneViewNativeMapSkyCloudPreparedFrame(SkyAppliedSourceCloudFrame native,
        SkySceneSourceFramePacket source, MapSourceCreatedSeasonLightingFrame present,
        IReadOnlyList<SkyCloudColorFacePacket> sixPackets)
    { Native = native; SourceFrame = source; Present = present; SixPackets = sixPackets; }
    public SkyAppliedSourceCloudFrame Native { get; }
    public SkySceneSourceFramePacket SourceFrame { get; }
    public MapSourceCreatedSeasonLightingFrame Present { get; }
    public IReadOnlyList<SkyCloudColorFacePacket> SixPackets { get; }
}
