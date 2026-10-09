using System.Buffers.Binary;
using San14.Core;
using San14.Data;

/// <summary>Loaded Field and detached owned campaign to the complete current
/// static CPU population. Normal initial descriptors use this locale's actual
/// postload source and remain owned across dates. A changed city or facility without its writer fails explicitly;
/// this owner never fills the population with arbitrary undamaged models.</summary>
public sealed class SceneViewNativeMapCpuSourceOwner
{
    private readonly SceneViewNativeMapLoadInputs loaded;
    private readonly LoadedScenario scenario;
    private readonly LoadedMap map;
    private readonly MapStaticModelCurrentOwnedPopulation population;
    private readonly MapStaticModelGroundHeightSource ground;
    private readonly CityVisualOpeningEvidence? opening;
    private readonly CityVisualDescriptorOwner? cityDescriptors;
    private readonly MapStaticModelOwnedGateDefinition[] gates;
    private bool valid = true;
    public string SourceRevision { get; }
    public MapStaticModelAssetCatalog Assets => population.Assets;

    private SceneViewNativeMapCpuSourceOwner(SceneViewNativeMapLoadInputs loaded,
        string viewIdentity, string revision, string? openingDescriptorJson)
    {
        this.loaded = loaded;
        var repository = loaded.Repository;
        SourceRevision = revision;
        string locale = loaded.InitialFrame.MainCamera.Camera.ExecutableLocale;
        var metadata = repository.EffectiveScenarios.Single(s =>
            GameResourceId.File(s.SourcePath).ToString() == loaded.InitialFrame.World.ScenarioKey && s.Locale == locale);
        scenario = repository.LoadScenario(metadata);
        map = repository.LoadCeMap();
        var variants = CeCityVariantTable.LoadCurrent(locale);
        if(openingDescriptorJson is not null)opening = CityVisualOpeningEvidence.LoadJson(openingDescriptorJson,
            "caller://native-city-opening-descriptors", loaded.Scenario,
            scenario.ScenarioSource.Sha256, loaded.InitialFrame.World.NewGameOptions, variants) ??
            throw new InvalidDataException("Native source population lacks matching original opening city descriptors.");
        else cityDescriptors=CityOpeningPostLoadSource.LoadSourceCreatedOrdinary(repository,scenario,map,
            loaded.InitialFrame.World.NewGameOptions??throw new InvalidDataException("City postload requires actual new-game settings."),
            variants).CreateCurrentDescriptorOwner(variants);
        ground = MapStaticModelGroundHeightSource.PrepareCurrentField(repository, loaded.Scene,
            UnitWorldGroundHeightFieldPreparationBranch.RetainedWrapperCreated, true, []);
        var geometry = new UnitGeometry();
        geometry.MaterializeOriginXImageSeed(); geometry.InitializeOriginZ();
        population = MapStaticModelCurrentOwnedPopulation.CreateSourceCreated(repository, loaded.Scene,
            variants, geometry, revision, repository.LoadObjects("KIDSSystemResource", 0x6D011726),
            0x899F101B, viewIdentity, 0, _ => null,
            _ => MapModelNativeCurrentVisibilitySources.PrepareCurrentNullCustomSelection(
                revision + "/owned-fresh-null-custom-selection", false, null, null));
        gates = loaded.Scenario.Gates.Where(g => g.Slot > 0).Select(g => {
            var area = loaded.Scenario.Areas.Single(a => a.Name == g.Name && a.HexId == g.HexId);
            byte[] remainder = Convert.FromHexString(area.StateRemainingBytesHex);
            if (remainder.Length != 4) throw new InvalidDataException("Current source gate heading row is unresolved.");
            return new MapStaticModelOwnedGateDefinition(g.LocationSlot, 55,
                BinaryPrimitives.ReadUInt16LittleEndian(remainder.AsSpan(1, 2)));
        }).ToArray();
    }

    public static SceneViewNativeMapCpuSourceOwner CreateFresh(
        SceneViewNativeMapLoadInputs loaded, string currentViewIdentity, string sourceRevision)
    {
        ArgumentNullException.ThrowIfNull(loaded);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentViewIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRevision);
        return new(loaded, currentViewIdentity, sourceRevision, null);
    }

    /// <summary>Identical concrete constructor for a saved descriptor document
    /// in CPU-only integration checks. The normal client derives its own source descriptors.</summary>
    public static SceneViewNativeMapCpuSourceOwner CreateFreshFromDescriptorDocument(
        SceneViewNativeMapLoadInputs loaded, string currentViewIdentity, string sourceRevision,
        string completeOpeningDescriptorJson) => new(loaded, currentViewIdentity, sourceRevision, completeOpeningDescriptorJson);

    public MapStaticModelCurrentOwnedFrame PrepareCurrent(SceneViewNativeMapFrameCapture frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (!valid || frame.SceneGeneration != loaded.InitialFrame.SceneGeneration ||
            frame.MainCamera.Camera.FrameNumber < 1)
            throw new InvalidDataException("Native static CPU preparation needs its retained source generation and current Main Camera.");
        if (frame.World.Structures is not { } structures || structures.Count != 0)
            throw new NotSupportedException("Current facility definition bindings are not yet produced by this initial source factory.");
        // SceneView already owns this detached world for the render attempt.
        // Static models need its current complete city/gate/facility/force
        // records, not another whole campaign JSON round trip or fingerprint.
        var current=MapStaticModelCapturedOwnedWorld.FromCapturedOwnedWorld(frame.World,
            frame.SourceProvenance+";actual-captured-World/request="+frame.RequestNumber);
        return population.PrepareCapturedOwnedWorld(new(current, frame.MainCamera, loaded.PlayerForceSlot, ground,
            null, opening, frame.World.PeriodNumber == 0, gates, [],
            new Dictionary<string, MapStaticModelPublication>()){CurrentCityDescriptors=cityDescriptors});
    }
    public void Invalidate(string actualWriter)
    { ArgumentException.ThrowIfNullOrWhiteSpace(actualWriter); valid = false; population.Invalidate(); }

    public MapStaticModelSourceCreatedCurrentNode BorrowCurrentSourceNode(
        MapStaticModelCurrentOwnedFrame actualFrame, MapStaticModelInstance actualInstance)
    {
        if (!valid) throw new InvalidOperationException("Static source population was retired.");
        return population.BorrowSourceCreatedCurrentNode(actualFrame,actualInstance);
    }
}
