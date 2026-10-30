namespace San14.Data;

/// <summary>Explicit current node identity, descriptor presence and parent
/// state. Fresh publication runs the proven constructor/bind prefix; current
/// publication retains the caller's established raw quaternion/scale/X/Z.</summary>
public sealed record MapStaticModelPublication(string? CurrentIdentity, bool? PrimaryDescriptorPresent,
    bool? ParentBound, bool FreshConstructorBindPrefix, UnitWorldTransformState? CurrentTransform)
{
    public bool? SourceDefaultLocalPoseIsCurrent { get; init; }
    public IReadOnlyDictionary<int, MapStaticModelPrimitiveLocalRows>? CurrentPrimitiveLocalRows { get; init; }
    public static MapStaticModelPublication FreshUnparented(string identity)
        => new(identity, true, false, true, null) {SourceDefaultLocalPoseIsCurrent = true};
    public static MapStaticModelPublication CurrentUnparented(string identity, UnitWorldTransformState current,
        bool? sourceDefaultLocalPoseIsCurrent)
        => new(identity, true, false, false, current) {SourceDefaultLocalPoseIsCurrent = sourceDefaultLocalPoseIsCurrent};
}
public sealed record MapStaticModelCityInput(CityVisualSelection Selection, MapStaticModelPublication Publication);
public sealed record MapStaticModelGateInput(int PhysicalSlot, int? HexId, int? Category,
    ushort? DefinitionHeading70, MapStaticModelPublication Publication);
public sealed record MapStaticModelFacilityInput(int PhysicalSlot, int? HexId, int? ObjectDefinitionSlot,
    bool? Completed, MapStaticModelPublication Publication);

public static partial class MapStaticModelInstanceFactory
{
    public static MapStaticModelInstanceSet FromNativeScenePlacements(MapStaticModelAssetCatalog assets,
        ScenePlacements placements, Func<ScenePlacement, MapStaticModelPublication?> currentPublication)
    {
        ArgumentNullException.ThrowIfNull(assets); ArgumentNullException.ThrowIfNull(placements);
        ArgumentNullException.ThrowIfNull(currentPublication);
        ValidateScene(assets, placements.Scene);
        var instances = new List<MapStaticModelInstance>(); var failures = new List<MapStaticModelInstanceFailure>();
        var effects = new List<int>(); var excluded = new List<string>();
        foreach (var placement in placements.Instances)
        {
            if (placement.Model is null) {effects.Add(placement.Source.Index); continue;}
            string key = $"scene:{placements.Scene.Scene.Source.ResourceUri}/{placement.Source.Index}/{placement.Source.Object.ObjectId:X8}";
            try
            {
                var publication = currentPublication(placement) ?? throw new InvalidDataException("Current original scene node publication identity is unknown.");
                if (!ValidatePublication(publication)) {excluded.Add(key); continue;}
                var state = publication.FreshConstructorBindPrefix ? UnitWorldTransform.ConstructScenePlacement(placement) :
                    publication.CurrentTransform ?? throw new InvalidDataException("Current original scene world transform fields are unknown.");
                var asset = assets.LoadSceneInstance(placement.Source);
                instances.Add(FromNativeWorldState(key, asset, state, publication.CurrentIdentity!, publication.CurrentPrimitiveLocalRows) with
                    {SourcePlacement = placement, NativePublication = publication});
            }
            catch (InvalidDataException error) {failures.Add(new(key, error.Message));}
        }
        return new(instances.AsReadOnly(), failures.AsReadOnly(), effects.AsReadOnly()) {ExcludedInstanceKeys = excluded.AsReadOnly()};
    }

    /// <summary>The source constructors, native Hex position/trigonometry,
    /// prepared Field ground query, facility clamp and final matrix/normal
    /// writers are performed here. Inputs carry current selection/identity;
    /// callers do not repeat those proven native preparation prefixes.</summary>
    public static MapStaticModelInstanceSet FromNativePrimaryPopulation(MapStaticModelAssetCatalog assets,
        CityVisualAssetCatalog cityAssets, IEnumerable<MapStaticModelCityInput> cities,
        IEnumerable<MapStaticModelGateInput> gates, IEnumerable<MapStaticModelFacilityInput> facilities,
        UnitGeometry currentGeometry, MapStaticModelGroundHeightSource currentHeight)
    {
        ArgumentNullException.ThrowIfNull(assets); ArgumentNullException.ThrowIfNull(cityAssets);
        ArgumentNullException.ThrowIfNull(cities); ArgumentNullException.ThrowIfNull(gates); ArgumentNullException.ThrowIfNull(facilities);
        ArgumentNullException.ThrowIfNull(currentGeometry); ArgumentNullException.ThrowIfNull(currentHeight);
        ValidateScene(assets, currentHeight.Scene);
        var cityRows = cities.ToArray(); var gateRows = gates.ToArray(); var facilityRows = facilities.ToArray();
        Unique(cityRows.Select(c => c.Selection.Slot), "city"); Unique(gateRows.Select(g => g.PhysicalSlot), "gate");
        Unique(facilityRows.Select(f => f.PhysicalSlot), "facility");
        var instances = new List<MapStaticModelInstance>(); var failures = new List<MapStaticModelInstanceFailure>();
        var excluded = new List<string>();
        foreach (var input in cityRows.OrderBy(c => c.Selection.Slot))
        {
            string key = "city:" + input.Selection.Slot;
            AddPrimary(key, input.Publication, () => assets.LoadCity(cityAssets, input.Selection),
                asset => UnitWorldTransform.CreateCityPrimary(asset.TextureResources, ValidHex(input.Selection.HexId), currentGeometry, currentHeight.Query),
                false, instance => instance with {CitySelection = input.Selection});
        }
        foreach (var input in gateRows.OrderBy(g => g.PhysicalSlot))
        {
            string key = "gate:" + input.PhysicalSlot;
            AddPrimary(key, input.Publication, () => assets.LoadGate(input.Category ?? throw new InvalidDataException("Current original gate category is unknown.")),
                asset => UnitWorldTransform.CreateGatePrimary(asset.TextureResources, ValidHex(input.HexId),
                    input.DefinitionHeading70 ?? throw new InvalidDataException("Current original gate definition heading word70 is unknown."), currentGeometry, currentHeight.Query),
                false, instance => instance);
        }
        foreach (var input in facilityRows.OrderBy(f => f.PhysicalSlot))
        {
            string key = "facility:" + input.PhysicalSlot;
            AddPrimary(key, input.Publication, () => assets.LoadFacility(input.ObjectDefinitionSlot ?? throw new InvalidDataException("Current original facility definition slot is unknown."),
                    input.Completed ?? throw new InvalidDataException("Current original facility model-state bit0 is unknown.")),
                asset => UnitWorldTransform.CreateFacilityPrimary(asset.TextureResources, ValidHex(input.HexId), currentGeometry, currentHeight.Query),
                true, instance => instance);
        }
        return new(instances.AsReadOnly(), failures.AsReadOnly(), Array.Empty<int>()) {ExcludedInstanceKeys = excluded.AsReadOnly()};

        void AddPrimary(string key, MapStaticModelPublication publication, Func<MapStaticModelAsset> load,
            Func<MapStaticModelAsset, UnitWorldTransformState> fresh, bool facilityClamp,
            Func<MapStaticModelInstance, MapStaticModelInstance> provenance)
        {
            try
            {
                if (!ValidatePublication(publication)) {excluded.Add(key); return;}
                var asset = load();
                var state = publication.FreshConstructorBindPrefix ? fresh(asset) :
                    UnitWorldTransform.RefreshGroundHeight(publication.CurrentTransform ??
                        throw new InvalidDataException("Current original primary world transform fields are unknown."), currentHeight.Query, facilityClamp);
                instances.Add(provenance(FromNativeWorldState(key, asset, state, publication.CurrentIdentity!, publication.CurrentPrimitiveLocalRows)) with
                    {NativePublication = publication});
            }
            catch (InvalidDataException error) {failures.Add(new(key, error.Message));}
        }
    }

    public static MapStaticModelInstanceSet Combine(params MapStaticModelInstanceSet[] populations)
    {
        ArgumentNullException.ThrowIfNull(populations);
        var instances = populations.SelectMany(p => p.Instances).ToArray();
        if (instances.Select(i => i.InstanceKey).Distinct(StringComparer.Ordinal).Count() != instances.Length)
            throw new InvalidDataException("Original map population has duplicate instance identities.");
        return new(Array.AsReadOnly(instances), Array.AsReadOnly(populations.SelectMany(p => p.Unresolved).ToArray()),
            Array.AsReadOnly(populations.SelectMany(p => p.EffectInstanceIndices).ToArray()))
            {ExcludedInstanceKeys = Array.AsReadOnly(populations.SelectMany(p => p.ExcludedInstanceKeys).ToArray())};
    }

    internal static MapStaticModelInstance FromNativeWorldState(string instanceKey, MapStaticModelAsset asset,
        UnitWorldTransformState sourceWorldState, string currentSourceIdentity,
        IReadOnlyDictionary<int, MapStaticModelPrimitiveLocalRows>? explicitCurrentPrimitiveLocals = null)
    {
        ArgumentNullException.ThrowIfNull(asset); ArgumentNullException.ThrowIfNull(sourceWorldState);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentSourceIdentity);
        var locals = explicitCurrentPrimitiveLocals ?? MapStaticModelPrimitiveLocalSource.Prepare(asset);
        if (!asset.Draws.Select(d => d.Index).Order().SequenceEqual(locals.Keys.Order()))
            throw new InvalidDataException("Current original primitive LOCAL input requires every actual source draw identity.");
        var world = sourceWorldState.ShaderTransform();
        var drawTransforms = asset.Draws.ToDictionary(draw => draw.Index, draw =>
        {
            var local = locals[draw.Index];
            return local.Rows is null ? world.Snapshot() : sourceWorldState.ShaderTransformWithPrimitiveLocalRows(local.Rows,
                local.EvidenceUri, local.EvidenceSha256);
        });
        return new MapStaticModelInstance(instanceKey, asset, world, drawTransforms)
            {SourceWorldState = sourceWorldState, CurrentSourceIdentity = currentSourceIdentity}.Snapshot();
    }

    private static bool ValidatePublication(MapStaticModelPublication publication)
    {
        ArgumentNullException.ThrowIfNull(publication);
        if (publication.PrimaryDescriptorPresent is not bool present)
            throw new InvalidDataException("Current original primary descriptor presence is unknown.");
        if (!present) return false;
        if (string.IsNullOrWhiteSpace(publication.CurrentIdentity))
            throw new InvalidDataException("Current original model node identity is unknown.");
        if (publication.ParentBound is not bool parent)
            throw new InvalidDataException("Current original model parent identity is unknown.");
        if (parent) throw new InvalidDataException("Parented original model matrix preparation requires the current parent source.");
        if (publication.FreshConstructorBindPrefix && publication.CurrentTransform is not null)
            throw new InvalidDataException("Fresh original constructor publication conflicts with an established current transform.");
        if (publication.SourceDefaultLocalPoseIsCurrent is not true && publication.CurrentPrimitiveLocalRows is null)
            throw new InvalidDataException("Current original primitive LOCAL pose identity is unknown; fresh source defaults cannot replace an unobserved current pose.");
        return true;
    }
    private static int ValidHex(int? hex)
    {
        if (hex is not int value || (uint)value >= 220 * 220)
            throw new InvalidDataException("Current original primary physical Hex identity is unknown or invalid.");
        return value;
    }
    private static void ValidateScene(MapStaticModelAssetCatalog assets, LoadedSceneResources scene)
    {
        if (scene.Scene.Source.ResourceUri != assets.Scene.Scene.Source.ResourceUri ||
            scene.Scene.Source.DecodedSha256 != assets.Scene.Scene.Source.DecodedSha256)
            throw new InvalidDataException("Original map population source scene revision differs from its asset catalog.");
    }
    private static void Unique(IEnumerable<int> slots, string kind)
    {
        int[] values = slots.ToArray();
        if (values.Any(v => v < 0) || values.Distinct().Count() != values.Length)
            throw new InvalidDataException($"Original map {kind} population has invalid/duplicate physical identities.");
    }
}
