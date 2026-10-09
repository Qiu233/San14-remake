using San14.Data;

/// <summary>Joins actual terrain, static and accepted Army submissions of
/// one accepted ordinary source frame. A completion callback or CPU counts
/// cannot create this receipt. It proves device submission, not a GPU fence
/// or visual equivalence to a currently running original game.</summary>
public sealed class NativeSceneGeometryStencilCompletedFrame
{
    private NativeSceneGeometryStencilCompletedFrame(
        MapSourceCreatedNativeSessionPreparedFrame frame,
        NativeMapModelPopulationAcceptedFrame population,
        NativeMapArmyPopulationAcceptedFrame army,
        NativeTerrainStencilPreparedFrame terrain,
        TerrainGeometrySubmittedFrame terrainSubmission)
    {
        Frame = frame; Population = population; Army = army; Terrain = terrain; TerrainSubmission = terrainSubmission;
        GeometrySourceFrame = frame.ActualGeometrySourceFrame ??
            throw new InvalidDataException("Geometry completion needs its original source snapshot.");
        WorldArmyGeometry = army.WorldQueuesCompletedPublication;
    }

    public MapSourceCreatedNativeSessionPreparedFrame Frame { get; }
    public MapSourceCreatedF103CurrentFrame GeometrySourceFrame { get; }
    public ArmyWorldQueuesCompletedPublication? WorldArmyGeometry { get; }
    public NativeMapModelPopulationAcceptedFrame Population { get; }
    public NativeMapArmyPopulationAcceptedFrame Army { get; }
    public NativeTerrainStencilPreparedFrame Terrain { get; }
    public TerrainGeometrySubmittedFrame TerrainSubmission { get; }
    // Retain this complete CPU information; never turn an unrouted primitive
    // into an invented ordinary draw or erase it from mode16 candidates.
    public int UnassignedStaticPrimitiveCount => NativeMapStaticSubmission.UnassignedPrimitiveCount(Population);

    public static NativeSceneGeometryStencilCompletedFrame Complete(
        MapSourceCreatedNativeSessionPreparedFrame frame,
        NativeMapModelPopulationAcceptedFrame population,
        NativeMapArmyPopulationAcceptedFrame army,
        NativeTerrainStencilPreparedFrame terrain,
        TerrainGeometrySubmittedFrame actualTerrainSubmission)
    {
        ArgumentNullException.ThrowIfNull(frame); ArgumentNullException.ThrowIfNull(population);
        ArgumentNullException.ThrowIfNull(army); ArgumentNullException.ThrowIfNull(terrain);
        ArgumentNullException.ThrowIfNull(actualTerrainSubmission);
        var completed = new NativeSceneGeometryStencilCompletedFrame(frame, population, army, terrain, actualTerrainSubmission);
        completed.ValidateCurrent(frame, population, army, actualTerrainSubmission);
        return completed;
    }

    public void ValidateCurrent(MapSourceCreatedNativeSessionPreparedFrame actualFrame,
        NativeMapModelPopulationAcceptedFrame actualPopulation,
        NativeMapArmyPopulationAcceptedFrame actualArmy,
        TerrainGeometrySubmittedFrame actualTerrainSubmission)
    {
        if (!ReferenceEquals(Frame.Context, actualFrame.Context) || !ReferenceEquals(Population, actualPopulation) ||
            (WorldArmyGeometry is null && !ReferenceEquals(Frame, actualFrame)) ||
            !ReferenceEquals(Army, actualArmy) || !ReferenceEquals(TerrainSubmission, actualTerrainSubmission) ||
            !ReferenceEquals(Frame.Context, Population.Context) || !ReferenceEquals(Frame.Context, Army.Prepared.Context) ||
            actualFrame.PreparedSourceFrame is not { } current ||
            !ReferenceEquals(actualFrame.ActualGeometrySourceFrame, GeometrySourceFrame) ||
            !ReferenceEquals(Terrain.Frame, GeometrySourceFrame) ||
            !ReferenceEquals(WorldArmyGeometry, Army.WorldQueuesCompletedPublication) ||
            !ReferenceEquals(Army.NativePrimitiveAdmission?.Frame, GeometrySourceFrame) ||
            Army.NativePrimitiveAdmission is not { } armyAdmission ||
            Army.Prepared.Unresolved.Count != 0 || Terrain.Node51Execution is not { } execution)
            throw new InvalidDataException("Ordinary source geometry completion lacks the exact current terrain/static/Army submissions and node51 activation.");
        var source = GeometrySourceFrame;
        WorldGeometryShadingPhase.RequireSameGeometrySnapshot(source, current);
        execution.RequireSelectedView(source.FrameNumber, source.Lighting.Binding);
        if(execution.SuppressesOrdinaryTerrainF8)
            throw new InvalidDataException("Completed ordinary Terrain cannot come from a suppressed GeneralScene invocation.");
        if (source.ViewFields.Kind1Records.Count(r => r.Mask78 == Terrain.SourceRecipe.RawWorldPQReference78) != 1)
            throw new InvalidDataException("Actual terrain WorldPQ is absent from this same source-created F103 View bank.");
        Terrain.ValidateSubmitted(TerrainSubmission);
        _ = NativeMapStaticSubmission.RequireCurrent(Population, Frame.Context, source);
        if (WorldArmyGeometry is not null)
            _ = NativeArmyWorldAcceptedGeometry.RequireCurrent(Army, Frame.Context, current);
        else
            armyAdmission.ValidateCurrentAccepted(Army);
    }
}
