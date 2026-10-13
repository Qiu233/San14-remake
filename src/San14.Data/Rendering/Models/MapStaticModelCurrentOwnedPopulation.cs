using System.Numerics;
using San14.Core;

namespace San14.Data;

/// <summary>Source definition fields not represented by Core gate/structure
/// state. Bindings cover every current physical slot; unknown values are kept
/// null and fail visibly, rather than selecting a category by a display name.</summary>
public sealed record MapStaticModelOwnedGateDefinition(int PhysicalSlot, int? Category, ushort? Heading70);
public sealed record MapStaticModelOwnedFacilityDefinition(int PhysicalSlot, int? ObjectDefinitionSlot);
public abstract record MapStaticModelOwnedFrameInputs(
    MainMapCameraFrame MainCamera, int CurrentPlayerForceSlot, MapStaticModelGroundHeightSource CurrentGround,
    IReadOnlyDictionary<int, CityVisualDamageInput>? CurrentCityDamage, CityVisualOpeningEvidence? OpeningEvidence,
    bool OpeningEvidenceApplies, IReadOnlyList<MapStaticModelOwnedGateDefinition> GateDefinitions,
    IReadOnlyList<MapStaticModelOwnedFacilityDefinition> FacilityDefinitions,
    IReadOnlyDictionary<string, MapStaticModelPublication> CurrentNodePublications)
{
    /// <summary>Owned descriptor publications, retained across calendar
    /// updates. Explicit CurrentCityDamage still provides the actual current
    /// writer output; missing changed descriptors are never filled from an
    /// earlier source opening.</summary>
    public CityVisualDescriptorOwner? CurrentCityDescriptors { get; init; }
}
public sealed record MapStaticModelCapturedOwnedInputs(MapStaticModelCapturedOwnedWorld CapturedOwnedWorld,
    MainMapCameraFrame MainCamera, int CurrentPlayerForceSlot, MapStaticModelGroundHeightSource CurrentGround,
    IReadOnlyDictionary<int, CityVisualDamageInput>? CurrentCityDamage, CityVisualOpeningEvidence? OpeningEvidence,
    bool OpeningEvidenceApplies, IReadOnlyList<MapStaticModelOwnedGateDefinition> GateDefinitions,
    IReadOnlyList<MapStaticModelOwnedFacilityDefinition> FacilityDefinitions,
    IReadOnlyDictionary<string, MapStaticModelPublication> CurrentNodePublications)
    :MapStaticModelOwnedFrameInputs(MainCamera,CurrentPlayerForceSlot,CurrentGround,CurrentCityDamage,
        OpeningEvidence,OpeningEvidenceApplies,GateDefinitions,FacilityDefinitions,CurrentNodePublications);

public sealed record MapStaticModelOwnedVisibilityRequest(MapStaticModelOwnedFrameInputs Frame,
    MapStaticModelInstance Instance, MapStaticModelCamera Camera);
public sealed record MapStaticModelOwnedViewRequest(MapStaticModelOwnedFrameInputs Frame,
    MapStaticModelCamera Camera);
/// <summary>The real retained source-created node state shared by ordinary
/// visibility and complete shadow candidates. Words are the same owned B0
/// object, not a fresh per-consumer constructor.</summary>
public sealed record MapStaticModelSourceCreatedCurrentNode(
    MapModelNativeFreshVisibilityPrefix Prefix,
    MapModelNativeCurrentVisibilityWords Words)
{
    public MapModelNativeBoundsPublication BoundsPublication => Prefix.BoundsPublication;
}
public sealed record MapStaticModelCurrentOwnedFrame(MainMapCameraFrame MainCamera,
    MapStaticModelInstanceSet Population, MapStaticModelVisibleSet Visibility,
    string SourceRevision)
{
    public long FrameNumber => MainCamera.Camera.FrameNumber;
    public string CurrentOwnedWorldProvenance { get; init; } = "";
    public DeferredCameraInputs PreparedCamera => MainCamera.Camera.SceneInputs;
}

/// <summary>Stable source-created population factory. It owns asset/placement
/// caches, automatically joins detached current Core city/gate/facility state
/// with the proved native WorldPQ/LOCAL/prepared Field prefixes, and binds the
/// native visibility publication to the already advanced current Main Camera.
/// It does not tick the Camera again or invent current eligibility/custom gates.</summary>
public sealed class MapStaticModelCurrentOwnedPopulation
{
    private readonly string _revision;
    private readonly ScenePlacements _placements;
    private readonly CeCityVariantTable _variants;
    private readonly CityVisualAssetCatalog _cities;
    private readonly UnitGeometry _geometry;
    private readonly MapModelNativeVisibilityPlanner _visibility;
    private readonly object _gate = new();
    private MapStaticModelOwnedFrameInputs? _preparing;
    private Action? _invalidateSourceCreated;
    private Action? _beginSourceCreatedFrame;
    private Func<MapStaticModelOwnedVisibilityRequest, MapStaticModelSourceCreatedCurrentNode>? _borrowSourceCreatedNode;
    private MapStaticModelCurrentOwnedFrame? _sourceCreatedCurrentFrame;
    private MapStaticModelOwnedFrameInputs? _sourceCreatedCurrentInputs;
    private IReadOnlyDictionary<string, MapStaticModelInstance>? _sourceCreatedCurrentInstances;
    public MapStaticModelAssetCatalog Assets { get; }

    public MapStaticModelCurrentOwnedPopulation(GameResourceRepository repository, LoadedSceneResources scene,
        CeCityVariantTable variants, UnitGeometry currentGeometry, string sourceRevision,
        Func<MapStaticModelOwnedVisibilityRequest, MapModelNativeVisibilityPublication> currentVisibility,
        Func<MapStaticModelOwnedViewRequest, MapModelNativeViewPublication> currentView)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(scene); ArgumentNullException.ThrowIfNull(variants);
        ArgumentNullException.ThrowIfNull(currentGeometry); ArgumentException.ThrowIfNullOrWhiteSpace(sourceRevision);
        ArgumentNullException.ThrowIfNull(currentVisibility); ArgumentNullException.ThrowIfNull(currentView);
        _revision = sourceRevision; Assets = new(repository, scene); _cities = new(repository, scene, variants);
        _placements = ScenePlacementReader.Read(scene); _variants = variants; _geometry = currentGeometry;
        _visibility = new((instance, camera) => currentVisibility(new(_preparing ?? throw new InvalidOperationException("No owned model frame is being prepared."), instance, camera)),
            camera => currentView(new(_preparing ?? throw new InvalidOperationException("No owned model frame is being prepared."), camera)));
    }

    /// <summary>Owns the proven source-created visibility state. The complete
    /// constructor establishes sixteen1f slots. Explicit current publications
    /// override that state; current custom results are still required. Matrix
    /// edits retain the owner's visibility/flags unless separately published.</summary>
    public static MapStaticModelCurrentOwnedPopulation CreateSourceCreated(GameResourceRepository repository,
        LoadedSceneResources scene, CeCityVariantTable variants, UnitGeometry currentGeometry,
        string sourceRevision, LoadedObjects explicitlyPublishedCommonViewSource, uint commonViewObjectId,
        string currentViewIdentity, int currentViewIndex,
        Func<MapStaticModelOwnedVisibilityRequest, MapModelNativeVisibilityPublication?> explicitCurrentVisibilityOverride,
        Func<MapStaticModelOwnedVisibilityRequest, IReadOnlyList<bool>?> completeCurrentAdditionalCullingRejects)
    {
        ArgumentNullException.ThrowIfNull(explicitCurrentVisibilityOverride); ArgumentNullException.ThrowIfNull(completeCurrentAdditionalCullingRejects);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentViewIdentity);
        if ((uint)currentViewIndex >= 16) throw new InvalidDataException("Owned Model source View index must be explicitly0..15.");
        var common = MapModelNativeVisibilitySources.ReadCommonViewTemplate(explicitlyPublishedCommonViewSource, commonViewObjectId);
        var nodes = new Dictionary<string, (MapStaticModelAsset Asset,
            MapStaticModelSourceCreatedCurrentNode Current)>(StringComparer.Ordinal);
        MapStaticModelOwnedFrameInputs? overrideFrame = null;
        var currentOverrides = new Dictionary<string, MapModelNativeVisibilityPublication?>(StringComparer.Ordinal);
        MapModelNativeVisibilityPublication? ReadCurrentOverride(MapStaticModelOwnedVisibilityRequest request)
        {
            if (!ReferenceEquals(overrideFrame, request.Frame))
            {
                overrideFrame = request.Frame; currentOverrides.Clear();
            }
            if (!currentOverrides.TryGetValue(request.Instance.InstanceKey, out var overridden))
            {
                overridden = explicitCurrentVisibilityOverride(request);
                currentOverrides.Add(request.Instance.InstanceKey, overridden);
            }
            return overridden;
        }
        MapStaticModelSourceCreatedCurrentNode GetOrCreateNode(MapStaticModelInstance instance)
        {
            string identity = instance.CurrentSourceIdentity ?? throw new InvalidDataException("Current owned Model identity is unresolved.");
            if (!nodes.TryGetValue(identity, out var node) || !ReferenceEquals(node.Asset, instance.Asset))
            {
                if (instance.NativePublication is not {FreshConstructorBindPrefix: true, ParentBound: false})
                    throw new InvalidDataException("An unowned/current replacement Model needs its explicit current visibility publication.");
                var prefix = MapModelNativeVisibilitySources.PrepareFreshSingleNode(instance, new(identity, false, true, true));
                var words = MapModelNativeCurrentVisibilitySources.ConstructFresh(prefix,
                    sourceRevision + "/source-created-visibility/" + instance.InstanceKey);
                nodes[identity] = node = (instance.Asset, new(prefix, words));
            }
            return node.Current;
        }
        var owner = new MapStaticModelCurrentOwnedPopulation(repository, scene, variants, currentGeometry, sourceRevision, request =>
        {
            var overridden = ReadCurrentOverride(request);
            if (overridden is not null) return overridden;
            var node = GetOrCreateNode(request.Instance);
            return new(request.Instance.CurrentSourceIdentity!, node.Words.IsDrawEligible(currentViewIndex), node.Prefix.ConstructorFlags44,
                node.Prefix.ConstructorFrustumCullingType33C, node.BoundsPublication,
                completeCurrentAdditionalCullingRejects(request));
        }, _ => new(currentViewIdentity, common.Value));
        owner._invalidateSourceCreated = () =>
        {
            nodes.Clear(); currentOverrides.Clear(); overrideFrame = null;
        };
        owner._beginSourceCreatedFrame = () =>
        {
            currentOverrides.Clear(); overrideFrame = null;
        };
        owner._borrowSourceCreatedNode = request =>
        {
            if (ReadCurrentOverride(request) is not null)
                throw new NotSupportedException("The explicit current visibility override does not publish its actual64-byte B0 owner for shadow candidates.");
            // Ordinary visibility may have skipped a disabled node before it
            // reached GetOrCreateNode. Its actual registered fresh node runs
            // the same constructor here once and thereafter shares the cache.
            return GetOrCreateNode(request.Instance);
        };
        return owner;
    }

    /// <summary>Borrow the same current node cache used by ordinary visibility.
    /// The instance comes from this owner's complete current Population, not
    /// its Visible subset. Later explicit visibility publications need their
    /// actual B0 owner instead of receiving the retained fresh words.</summary>
    public MapStaticModelSourceCreatedCurrentNode BorrowSourceCreatedCurrentNode(
        MapStaticModelCurrentOwnedFrame actualCurrentFrame, MapStaticModelInstance actualInstance)
    {
        ArgumentNullException.ThrowIfNull(actualCurrentFrame); ArgumentNullException.ThrowIfNull(actualInstance);
        lock (_gate)
        {
            if (_borrowSourceCreatedNode is null)
                throw new NotSupportedException("This population owner was not constructed with the original source-created node state.");
            if (!ReferenceEquals(actualCurrentFrame, _sourceCreatedCurrentFrame) || _sourceCreatedCurrentInputs is null ||
                _sourceCreatedCurrentInstances is null ||
                !_sourceCreatedCurrentInstances.TryGetValue(actualInstance.InstanceKey, out var current) ||
                !ReferenceEquals(current, actualInstance))
                throw new InvalidOperationException("Source-created node borrow requires this owner's complete current Population instance.");
            return _borrowSourceCreatedNode(new(_sourceCreatedCurrentInputs, actualInstance, actualCurrentFrame.Visibility.Camera));
        }
    }

    /// <summary>Normal SceneView has already detached the campaign when its
    /// GameState changed. Consume that attempt's actual Model projection;
    /// current placements, visual categories, matrices and visibility are
    /// still prepared below on every request.</summary>
    public MapStaticModelCurrentOwnedFrame PrepareCapturedOwnedWorld(MapStaticModelCapturedOwnedInputs input)
    {
        ArgumentNullException.ThrowIfNull(input);ArgumentNullException.ThrowIfNull(input.CapturedOwnedWorld);
        return PrepareCurrent(input,input.CapturedOwnedWorld);
    }

    private MapStaticModelCurrentOwnedFrame PrepareCurrent(MapStaticModelOwnedFrameInputs input,
        MapStaticModelCapturedOwnedWorld world)
    {
        ArgumentNullException.ThrowIfNull(input.MainCamera);
        ArgumentNullException.ThrowIfNull(input.CurrentGround); ArgumentNullException.ThrowIfNull(input.GateDefinitions);
        ArgumentNullException.ThrowIfNull(input.FacilityDefinitions); ArgumentNullException.ThrowIfNull(input.CurrentNodePublications);
        lock (_gate)
        {
            if (_preparing is not null) throw new InvalidOperationException("Owned model preparation cannot reenter itself.");
            if (input.MainCamera.Camera.Source.Camera.ObjectId != MainMapCameraAdapter.MainCameraObjectId ||
                input.MainCamera.Camera.FrameNumber <= 0 || input.CurrentPlayerForceSlot != input.MainCamera.InitialFocus.PlayerForcePhysicalSlot)
                throw new InvalidDataException("Owned source population requires the current Main Camera frame and its explicitly selected player owner.");
            if (input.CurrentGround.Scene.Scene.Source.ResourceUri != Assets.Scene.Scene.Source.ResourceUri ||
                input.CurrentGround.Scene.Scene.Source.DecodedSha256 != Assets.Scene.Scene.Source.DecodedSha256)
                throw new InvalidDataException("Current source model ground and population belong to different scene revisions.");
            var structures = world.Structures;
            if (!world.Forces.TryGetValue(input.CurrentPlayerForceSlot, out var player) || !player.IsActive ||
                world.MapWidth != 220 || world.MapHeight != 220)
                throw new InvalidDataException("Owned source model frame requires its active current player and native physical map grid.");
            if (input.GateDefinitions.Select(d => d.PhysicalSlot).Distinct().Count() != input.GateDefinitions.Count ||
                !input.GateDefinitions.Select(d => d.PhysicalSlot).Order().SequenceEqual(world.Gates.Keys.Order()) ||
                input.FacilityDefinitions.Select(d => d.PhysicalSlot).Distinct().Count() != input.FacilityDefinitions.Count ||
                !input.FacilityDefinitions.Select(d => d.PhysicalSlot).Order().SequenceEqual(structures.Keys.Order()))
                throw new InvalidDataException("Owned source gate/facility definition bindings must cover every current physical slot exactly once.");

            MapStaticModelPublication Publication(string key) => input.CurrentNodePublications.TryGetValue(key, out var current)
                ? current : MapStaticModelPublication.FreshUnparented(_revision + "/owned-node/" + key);
            var scenePopulation = MapStaticModelInstanceFactory.FromNativeScenePlacements(Assets, _placements, p =>
                Publication($"scene:{Assets.Scene.Scene.Source.ResourceUri}/{p.Source.Index}/{p.Source.Object.ObjectId:X8}"));
            var cityInputs = new List<MapStaticModelCityInput>(); var failures = new List<MapStaticModelInstanceFailure>();
            foreach (var city in world.Cities.Values.OrderBy(c => c.Id))
            {
                CityVisualDamageInput? damage = null; input.CurrentCityDamage?.TryGetValue(city.Id, out damage);
                var cityState=CityVisualState.FromCity(city);
                var selected = damage is null&&input.CurrentCityDescriptors is { } descriptors
                    ? descriptors.SelectCurrentDescriptor(_variants,cityState)
                    : CityVisualSelector.Select(_variants,cityState,damage,input.OpeningEvidence,input.OpeningEvidenceApplies);
                if (selected.Selection is null) {failures.Add(new("city:" + city.Id, selected.UnavailableReason ?? "Current source city category is unresolved.")); continue;}
                cityInputs.Add(new(selected.Selection, Publication("city:" + city.Id)));
            }
            var gates = input.GateDefinitions.OrderBy(d => d.PhysicalSlot).Select(d => new MapStaticModelGateInput(
                d.PhysicalSlot, world.Gates[d.PhysicalSlot].HexId, d.Category, d.Heading70, Publication("gate:" + d.PhysicalSlot))).ToArray();
            var facilities = input.FacilityDefinitions.OrderBy(d => d.PhysicalSlot).Select(d =>
            {
                var structure = structures[d.PhysicalSlot];
                return new MapStaticModelFacilityInput(d.PhysicalSlot, structure.HexId, d.ObjectDefinitionSlot,
                    structure.Completed, Publication("facility:" + d.PhysicalSlot));
            }).ToArray();
            var primary = MapStaticModelInstanceFactory.FromNativePrimaryPopulation(Assets, _cities,
                cityInputs, gates, facilities, _geometry, input.CurrentGround);
            var population = MapStaticModelInstanceFactory.Combine(scenePopulation, primary,
                new(Array.Empty<MapStaticModelInstance>(), failures.AsReadOnly(), Array.Empty<int>()));
            var knownKeys = _placements.Instances.Where(p => p.Model is not null).Select(p =>
                $"scene:{Assets.Scene.Scene.Source.ResourceUri}/{p.Source.Index}/{p.Source.Object.ObjectId:X8}")
                .Concat(world.Cities.Keys.Select(id => "city:" + id)).Concat(world.Gates.Keys.Select(id => "gate:" + id))
                .Concat(structures.Keys.Select(id => "facility:" + id)).ToHashSet(StringComparer.Ordinal);
            if (input.CurrentNodePublications.Keys.Any(key => !knownKeys.Contains(key)))
                throw new InvalidDataException("Current owned Model edit names an absent or unsupported node identity.");
            if (!population.Complete)
                throw new InvalidDataException("Owned source model population is unresolved: " + string.Join("; ", population.Unresolved.Select(f => f.InstanceKey + ": " + f.Reason)));
            var deferred = input.MainCamera.Camera.SceneInputs;
            var camera = MapStaticModelCamera.FromExplicitRows(Rows(deferred.SourceWorldToProjection),
                Rows(Matrix4x4.Transpose(deferred.SourceWorldToView))[..12]);
            _preparing = input;
            _beginSourceCreatedFrame?.Invoke();
            _sourceCreatedCurrentFrame = null; _sourceCreatedCurrentInputs = null; _sourceCreatedCurrentInstances = null;
            try
            {
                var current = new MapStaticModelCurrentOwnedFrame(input.MainCamera, population,
                    _visibility.Select(population, camera), _revision)
                    {CurrentOwnedWorldProvenance=world.SourceProvenance};
                if (_borrowSourceCreatedNode is not null)
                {
                    _sourceCreatedCurrentFrame = current; _sourceCreatedCurrentInputs = input;
                    _sourceCreatedCurrentInstances = population.Instances.ToDictionary(instance => instance.InstanceKey, StringComparer.Ordinal);
                }
                return current;
            }
            finally {_preparing = null;}
        }
    }

    private static float[] Rows(Matrix4x4 m) => [m.M11,m.M12,m.M13,m.M14,m.M21,m.M22,m.M23,m.M24,
        m.M31,m.M32,m.M33,m.M34,m.M41,m.M42,m.M43,m.M44];
    public void Invalidate()
    {
        lock (_gate)
        {
            _sourceCreatedCurrentFrame = null; _sourceCreatedCurrentInputs = null; _sourceCreatedCurrentInstances = null;
            Assets.Invalidate(); _cities.Invalidate(); _invalidateSourceCreated?.Invoke();
        }
    }
}
