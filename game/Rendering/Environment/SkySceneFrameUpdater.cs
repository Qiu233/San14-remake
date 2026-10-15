using San14.Data;

/// <summary>Complete optional producer packets for the next source generation.
/// A configured producer with present resources needs all six packets. Native
/// gate values, scene/contribution branches and Sky2 presence stay explicit.</summary>
public sealed record SkySceneProducerFrame(
    IReadOnlyList<SkyStarFacePacket>? StarFaces,
    SkyStarNativeGate? StarGate,
    IReadOnlyList<SkyCloudColorFacePacket>? CloudFaces,
    bool Sky2Enabled, SkySceneFaceProducer? Scene,
    SkySceneFaceProducer? Contribution);

public sealed record SkySceneFrameUpdateResult(
    SkyScenePreparedFrame PreparedFrame, SkySceneProgress Progress,
    CubeRenderTextureDevice? CompletedSpecular);

/// <summary>Retains source caches and stages one explicit generation through
/// the original base/copy/filter chain. Scene and optional producers are borrowed.
/// Mutate them through this updater while it is in use. It chooses no active
/// owner, clock, generation slot, draw variant or budget and owns no GPU resource.</summary>
public sealed class SkySceneFrameUpdater
{
    public SkyScene Scene { get; }
    public SkyStarProducer? Stars { get; }
    public SkyCloudColorProducer? Cloud { get; }
    private IReadOnlyList<SkySceneSky2FaceCache> _retainedFaces;
    private SkyScenePreparedFrame? _prepared;
    private uint _generationIndex;
    private readonly SemaphoreSlim _gate = new(1, 1);

    internal SkyScenePreparedFrame StagedFrame => _prepared ??
        throw new InvalidOperationException("No source sky frame has been staged.");

    internal void RequireStagedFrame(SkyScenePreparedFrame prepared)
    {
        if (!ReferenceEquals(_prepared, prepared))
            throw new InvalidOperationException("The source sky frame was replaced or its staging failed.");
    }

    public SkySceneFrameUpdater(SkyScene scene,
        IReadOnlyList<SkySceneSky2FaceCache> explicitSixRetainedFaces,
        SkyStarProducer? stars = null, SkyCloudColorProducer? cloud = null)
    {
        ArgumentNullException.ThrowIfNull(scene);
        if ((stars is not null && !ReferenceEquals(stars.Work, scene.PrimaryWork)) ||
            (cloud is not null && !ReferenceEquals(cloud.Work, scene.PrimaryWork)))
            throw new InvalidDataException("Sky updater producers must belong to this scene's primary WORK resource.");
        Scene = scene; Stars = stars; Cloud = cloud;
        _retainedFaces = SnapshotFaces(explicitSixRetainedFaces);
    }

    /// <summary>Prepare and retain the named source update, stage original
    /// producer constants, begin the explicit slot, then submit the caller budget.
    /// Starting while a generation is active fails before changing any producer.
    /// A zero budget still stages the frame but submits no draw.</summary>
    public async Task<SkySceneFrameUpdateResult> SubmitFrameAsync(
        uint explicitGenerationIndex, SkySceneSourceFrame explicitSources,
        SkySceneProducerFrame explicitProducers, uint maximumWorkUnits,
        SkySceneOptionalLightInputs? explicitLight = null)
    {
        ArgumentNullException.ThrowIfNull(explicitSources); ArgumentNullException.ThrowIfNull(explicitProducers);
        if (explicitGenerationIndex >= 3) throw new ArgumentOutOfRangeException(nameof(explicitGenerationIndex));
        await _gate.WaitAsync();
        try
        {
            var current = await Scene.GetProgressAsync();
            if (current.Phase is not (SkyScenePhase.Idle or SkyScenePhase.Faulted))
                throw new InvalidOperationException("Complete the staged sky frame before submitting another source frame.");
            var prepared = SkySceneFramePreparation.Prepare(_retainedFaces, explicitSources, Scene.Sky2Program.PixelBindings.SourceDefinitions);
            var starPackets = SnapshotStars(explicitProducers);
            var cloudPackets = SnapshotCloud(explicitProducers);
            foreach (var branch in new[] { explicitProducers.Scene, explicitProducers.Contribution })
                if (branch is not null && (string.IsNullOrWhiteSpace(branch.SourceResourceUri) || branch.SubmitAsync is null))
                    throw new InvalidDataException("Sky updater branches require explicit source provenance and submission.");
            var generation = Scene.PrepareGeneration(explicitGenerationIndex, prepared.DrawPackets, explicitLight);
            // Validation alone leaves the previous frame intact. Once device
            // staging starts, no failure may resume that previous submission.
            _prepared = null;
            if (Stars is not null) await Stars.UpdateFrameAsync(starPackets, explicitProducers.StarGate!);
            if (Cloud is not null) await Cloud.UpdateFrameAsync(cloudPackets);
            var stages = new SkySceneBaseStages(Stars?.CreateSceneStage(), explicitProducers.Sky2Enabled,
                explicitProducers.Scene, Cloud?.CreateSceneStage(), explicitProducers.Contribution);
            await Scene.BeginGenerationAsync(generation, stages);
            _retainedFaces = SnapshotFaces(prepared.Faces);
            _prepared = SnapshotPrepared(prepared); _generationIndex = explicitGenerationIndex;
            return await AdvanceCoreAsync(maximumWorkUnits);
        }
        finally { _gate.Release(); }
    }

    /// <summary>Resume the staged source frame. The result exposes the borrowed
    /// specular target only when its complete generation is ready.</summary>
    public async Task<SkySceneFrameUpdateResult> AdvanceAsync(uint maximumWorkUnits)
    {
        await _gate.WaitAsync();
        try
        {
            if (_prepared is null) throw new InvalidOperationException("No source sky frame has been staged.");
            return await AdvanceCoreAsync(maximumWorkUnits);
        }
        finally { _gate.Release(); }
    }

    private async Task<SkySceneFrameUpdateResult> AdvanceCoreAsync(uint maximumWorkUnits)
    {
        try { return Result(await Scene.AdvanceAsync(maximumWorkUnits)); }
        catch { _prepared = null; throw; }
    }

    private SkySceneFrameUpdateResult Result(SkySceneProgress progress)
    {
        var complete = progress.Phase == SkyScenePhase.Idle &&
            progress.LastCompletedIndex == _generationIndex && progress.GenerationIndex == _generationIndex;
        return new(SnapshotPrepared(_prepared!), progress,
            complete ? Scene.GetCompletedSpecular(_generationIndex) : null);
    }

    private SkyStarFacePacket[] SnapshotStars(SkySceneProducerFrame source)
    {
        if (Stars is null)
        {
            if (source.StarFaces is not null || source.StarGate is not null)
                throw new InvalidDataException("Star packets require an attached original Star producer.");
            return [];
        }
        ArgumentNullException.ThrowIfNull(source.StarGate); _ = source.StarGate.AllowsDraw;
        if (!Stars.HasGeometry)
        {
            if (source.StarFaces is { Count: > 0 }) throw new InvalidDataException("Absent star geometry has no source face packets.");
            return [];
        }
        if (source.StarFaces is null || source.StarFaces.Count != 6)
            throw new InvalidDataException("Present star geometry needs six complete source face packets.");
        return source.StarFaces.Select(p =>
        {
            var packet = SkyStarConstants.Read(Stars.Program.VertexDefinitions, Stars.Program.GeometryDefinitions,
                Stars.Program.PixelBindings.SourceDefinitions, p.VertexConstants.Span, p.GeometryConstants.Span, p.PixelConstants.Span);
            return new SkyStarFacePacket(packet.VertexBytes, packet.GeometryBytes, packet.PixelBytes);
        }).ToArray();
    }

    private SkyCloudColorFacePacket[] SnapshotCloud(SkySceneProducerFrame source)
    {
        if (Cloud is null)
        {
            if (source.CloudFaces is not null) throw new InvalidDataException("Cloud packets require an attached original Cloud producer.");
            return [];
        }
        if (!Cloud.NoiseGroupPresent)
        {
            if (source.CloudFaces is { Count: > 0 }) throw new InvalidDataException("Absent cloud noise has no source face packets.");
            return [];
        }
        if (source.CloudFaces is null || source.CloudFaces.Count != 6)
            throw new InvalidDataException("Present cloud noise needs six complete source face packets.");
        return source.CloudFaces.Select(p =>
        {
            var packet = SkyCloudConstants.ReadColor(Cloud.Program.VertexBindings.SourceDefinitions,
                Cloud.Program.PixelBindings.SourceDefinitions, p.VertexConstants.Span, p.PixelConstants.Span, p.MaximumRaySamples);
            return new SkyCloudColorFacePacket(packet.VertexBytes, packet.PixelBytes, packet.MaximumRaySamples);
        }).ToArray();
    }

    private static IReadOnlyList<SkySceneSky2FaceCache> SnapshotFaces(
        IReadOnlyList<SkySceneSky2FaceCache> faces)
    {
        ArgumentNullException.ThrowIfNull(faces);
        if (faces.Count != 6 || faces.Any(f => f is null || f.Draw is null || f.Draw.Pixel.Scattering is null))
            throw new InvalidDataException("Sky updater requires six retained complete source caches.");
        return Array.AsReadOnly(faces.Select(f => f with { Draw = f.Draw with {
            Pixel = f.Draw.Pixel with { Scattering = f.Draw.Pixel.Scattering.ToArray() } } }).ToArray());
    }

    private static SkyScenePreparedFrame SnapshotPrepared(SkyScenePreparedFrame frame)
        => new(SnapshotFaces(frame.Faces), Array.AsReadOnly(frame.SkyTransform.ToArray()),
            Array.AsReadOnly(frame.SourceProvenance.ToArray()));
}
