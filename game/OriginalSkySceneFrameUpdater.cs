using San14.Data;

/// <summary>Complete optional producer packets for the next source generation.
/// A configured producer with present resources needs all six packets. Native
/// gate values, scene/contribution branches and Sky2 presence stay explicit.</summary>
public sealed record OriginalSkySceneProducerFrame(
    IReadOnlyList<OriginalSkyStarFacePacket>? StarFaces,
    OriginalSkyStarNativeGate? StarGate,
    IReadOnlyList<OriginalSkyCloudColorFacePacket>? CloudFaces,
    bool Sky2Enabled, OriginalSkySceneFaceProducer? Scene,
    OriginalSkySceneFaceProducer? Contribution);

public sealed record OriginalSkySceneFrameUpdateResult(
    OriginalSkyScenePreparedFrame PreparedFrame, OriginalSkySceneProgress Progress,
    OriginalCubeRenderTextureDevice? CompletedSpecular);

/// <summary>Retains source caches and stages one explicit generation through
/// the original base/copy/filter chain. Scene and optional producers are borrowed.
/// Mutate them through this updater while it is in use. It chooses no active
/// owner, clock, generation slot, draw variant or budget and owns no GPU resource.</summary>
public sealed class OriginalSkySceneFrameUpdater
{
    public OriginalSkyScene Scene { get; }
    public OriginalSkyStarProducer? Stars { get; }
    public OriginalSkyCloudColorProducer? Cloud { get; }
    private IReadOnlyList<OriginalSkySceneSky2FaceCache> _retainedFaces;
    private OriginalSkyScenePreparedFrame? _prepared;
    private uint _generationIndex;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public OriginalSkySceneFrameUpdater(OriginalSkyScene scene,
        IReadOnlyList<OriginalSkySceneSky2FaceCache> explicitSixRetainedFaces,
        OriginalSkyStarProducer? stars = null, OriginalSkyCloudColorProducer? cloud = null)
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
    public async Task<OriginalSkySceneFrameUpdateResult> SubmitFrameAsync(
        uint explicitGenerationIndex, OriginalSkySceneSourceFrame explicitSources,
        OriginalSkySceneProducerFrame explicitProducers, uint maximumWorkUnits,
        OriginalSkySceneOptionalLightInputs? explicitLight = null)
    {
        ArgumentNullException.ThrowIfNull(explicitSources); ArgumentNullException.ThrowIfNull(explicitProducers);
        if (explicitGenerationIndex >= 3) throw new ArgumentOutOfRangeException(nameof(explicitGenerationIndex));
        await _gate.WaitAsync();
        try
        {
            var current = await Scene.GetProgressAsync();
            if (current.Phase is not (OriginalSkyScenePhase.Idle or OriginalSkyScenePhase.Faulted))
                throw new InvalidOperationException("Complete the staged sky frame before submitting another source frame.");
            var prepared = OriginalSkySceneFramePreparation.Prepare(_retainedFaces, explicitSources);
            var starPackets = SnapshotStars(explicitProducers);
            var cloudPackets = SnapshotCloud(explicitProducers);
            foreach (var branch in new[] { explicitProducers.Scene, explicitProducers.Contribution })
                if (branch is not null && (string.IsNullOrWhiteSpace(branch.SourceResourceUri) || branch.SubmitAsync is null))
                    throw new InvalidDataException("Sky updater branches require explicit source provenance and submission.");
            if (Stars is not null) await Stars.UpdateFrameAsync(starPackets, explicitProducers.StarGate!);
            if (Cloud is not null) await Cloud.UpdateFrameAsync(cloudPackets);
            var stages = new OriginalSkySceneBaseStages(Stars?.CreateSceneStage(), explicitProducers.Sky2Enabled,
                explicitProducers.Scene, Cloud?.CreateSceneStage(), explicitProducers.Contribution);
            await Scene.BeginGenerationAsync(explicitGenerationIndex, prepared, stages, explicitLight);
            _retainedFaces = SnapshotFaces(prepared.Faces);
            _prepared = SnapshotPrepared(prepared); _generationIndex = explicitGenerationIndex;
            return Result(await Scene.AdvanceAsync(maximumWorkUnits));
        }
        finally { _gate.Release(); }
    }

    /// <summary>Resume the staged source frame. The result exposes the borrowed
    /// specular target only when its complete generation is ready.</summary>
    public async Task<OriginalSkySceneFrameUpdateResult> AdvanceAsync(uint maximumWorkUnits)
    {
        await _gate.WaitAsync();
        try
        {
            if (_prepared is null) throw new InvalidOperationException("No source sky frame has been staged.");
            return Result(await Scene.AdvanceAsync(maximumWorkUnits));
        }
        finally { _gate.Release(); }
    }

    private OriginalSkySceneFrameUpdateResult Result(OriginalSkySceneProgress progress)
    {
        var complete = progress.Phase == OriginalSkyScenePhase.Idle &&
            progress.LastCompletedIndex == _generationIndex && progress.GenerationIndex == _generationIndex;
        return new(SnapshotPrepared(_prepared!), progress,
            complete ? Scene.GetCompletedSpecular(_generationIndex) : null);
    }

    private OriginalSkyStarFacePacket[] SnapshotStars(OriginalSkySceneProducerFrame source)
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
            var packet = OriginalSkyStarConstants.Read(Stars.Program.VertexDefinitions, Stars.Program.GeometryDefinitions,
                Stars.Program.PixelBindings.OriginalDefinitions, p.VertexConstants.Span, p.GeometryConstants.Span, p.PixelConstants.Span);
            return new OriginalSkyStarFacePacket(packet.VertexBytes, packet.GeometryBytes, packet.PixelBytes);
        }).ToArray();
    }

    private OriginalSkyCloudColorFacePacket[] SnapshotCloud(OriginalSkySceneProducerFrame source)
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
            var packet = OriginalSkyCloudConstants.ReadColor(Cloud.Program.VertexBindings.OriginalDefinitions,
                Cloud.Program.PixelBindings.OriginalDefinitions, p.VertexConstants.Span, p.PixelConstants.Span, p.MaximumRaySamples);
            return new OriginalSkyCloudColorFacePacket(packet.VertexBytes, packet.PixelBytes, packet.MaximumRaySamples);
        }).ToArray();
    }

    private static IReadOnlyList<OriginalSkySceneSky2FaceCache> SnapshotFaces(
        IReadOnlyList<OriginalSkySceneSky2FaceCache> faces)
    {
        ArgumentNullException.ThrowIfNull(faces);
        if (faces.Count != 6 || faces.Any(f => f is null || f.Draw is null || f.Draw.Pixel.Scattering is null))
            throw new InvalidDataException("Sky updater requires six retained complete source caches.");
        return Array.AsReadOnly(faces.Select(f => f with { Draw = f.Draw with {
            Pixel = f.Draw.Pixel with { Scattering = f.Draw.Pixel.Scattering.ToArray() } } }).ToArray());
    }

    private static OriginalSkyScenePreparedFrame SnapshotPrepared(OriginalSkyScenePreparedFrame frame)
        => new(SnapshotFaces(frame.Faces), Array.AsReadOnly(frame.SkyTransform.ToArray()),
            Array.AsReadOnly(frame.SourceProvenance.ToArray()));
}
