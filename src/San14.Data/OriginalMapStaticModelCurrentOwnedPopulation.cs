using System.Numerics;
using San14.Core;

namespace San14.Data;

/// <summary>Source definition fields not represented by Core gate/structure
/// state. Bindings cover every current physical slot; unknown values are kept
/// null and fail visibly, rather than selecting a category by a display name.</summary>
public sealed record OriginalMapStaticModelOwnedGateDefinition(int PhysicalSlot, int? Category, ushort? Heading70);
public sealed record OriginalMapStaticModelOwnedFacilityDefinition(int PhysicalSlot, int? ObjectDefinitionSlot);
public abstract record OriginalMapStaticModelOwnedFrameInputs(
    OriginalMainMapCameraFrame MainCamera, int CurrentPlayerForceSlot, OriginalMapStaticModelGroundHeightSource CurrentGround,
    IReadOnlyDictionary<int, CityVisualDamageInput>? CurrentCityDamage, CityVisualOpeningEvidence? OpeningEvidence,
    bool OpeningEvidenceApplies, IReadOnlyList<OriginalMapStaticModelOwnedGateDefinition> GateDefinitions,
    IReadOnlyList<OriginalMapStaticModelOwnedFacilityDefinition> FacilityDefinitions,
    IReadOnlyDictionary<string, OriginalMapStaticModelPublication> CurrentNodePublications)
{
    /// <summary>Owned descriptor publications, retained across calendar
    /// updates. Explicit CurrentCityDamage still provides the actual current
    /// writer output; missing changed descriptors are never filled from an
    /// earlier source opening.</summary>
    public OriginalCityVisualDescriptorOwner? CurrentCityDescriptors { get; init; }
}
public sealed record OriginalMapStaticModelCurrentOwnedInputs(OriginalTerritoryOwnedWorldSnapshot OwnedWorld,
    OriginalMainMapCameraFrame MainCamera, int CurrentPlayerForceSlot, OriginalMapStaticModelGroundHeightSource CurrentGround,
    IReadOnlyDictionary<int, CityVisualDamageInput>? CurrentCityDamage, CityVisualOpeningEvidence? OpeningEvidence,
    bool OpeningEvidenceApplies, IReadOnlyList<OriginalMapStaticModelOwnedGateDefinition> GateDefinitions,
    IReadOnlyList<OriginalMapStaticModelOwnedFacilityDefinition> FacilityDefinitions,
    IReadOnlyDictionary<string, OriginalMapStaticModelPublication> CurrentNodePublications)
    :OriginalMapStaticModelOwnedFrameInputs(MainCamera,CurrentPlayerForceSlot,CurrentGround,CurrentCityDamage,
        OpeningEvidence,OpeningEvidenceApplies,GateDefinitions,FacilityDefinitions,CurrentNodePublications);
public sealed record OriginalMapStaticModelCapturedOwnedInputs(OriginalMapStaticModelCapturedOwnedWorld CapturedOwnedWorld,
    OriginalMainMapCameraFrame MainCamera, int CurrentPlayerForceSlot, OriginalMapStaticModelGroundHeightSource CurrentGround,
    IReadOnlyDictionary<int, CityVisualDamageInput>? CurrentCityDamage, CityVisualOpeningEvidence? OpeningEvidence,
    bool OpeningEvidenceApplies, IReadOnlyList<OriginalMapStaticModelOwnedGateDefinition> GateDefinitions,
    IReadOnlyList<OriginalMapStaticModelOwnedFacilityDefinition> FacilityDefinitions,
    IReadOnlyDictionary<string, OriginalMapStaticModelPublication> CurrentNodePublications)
    :OriginalMapStaticModelOwnedFrameInputs(MainCamera,CurrentPlayerForceSlot,CurrentGround,CurrentCityDamage,
        OpeningEvidence,OpeningEvidenceApplies,GateDefinitions,FacilityDefinitions,CurrentNodePublications);

public sealed record OriginalMapStaticModelOwnedVisibilityRequest(OriginalMapStaticModelOwnedFrameInputs Frame,
    OriginalMapStaticModelInstance Instance, OriginalMapStaticModelCamera Camera);
public sealed record OriginalMapStaticModelOwnedViewRequest(OriginalMapStaticModelOwnedFrameInputs Frame,
    OriginalMapStaticModelCamera Camera);
/// <summary>The real retained source-created node state shared by ordinary
/// visibility and complete shadow candidates. Words are the same owned B0
/// object, not a fresh per-consumer constructor.</summary>
public sealed record OriginalMapStaticModelSourceCreatedCurrentNode(
    OriginalMapModelNativeFreshVisibilityPrefix Prefix,
    OriginalMapModelNativeCurrentVisibilityWords Words)
{
    public OriginalMapModelNativeBoundsPublication BoundsPublication => Prefix.BoundsPublication;
}
public sealed record OriginalMapStaticModelCurrentOwnedFrame(OriginalMainMapCameraFrame MainCamera,
    OriginalMapStaticModelInstanceSet Population, OriginalMapStaticModelVisibleSet Visibility,
    string OwnedWorldSnapshotSha256, string SourceRevision)
{
    public long FrameNumber => MainCamera.Camera.FrameNumber;
    public string CurrentOwnedWorldProvenance { get; init; } = "";
    public OriginalDeferredCameraInputs PreparedCamera => OriginalPreparedCameraSceneSource.ReadCompletePrefix(MainCamera.Camera.Prefix.Span);
}

/// <summary>Stable source-created population factory. It owns asset/placement
/// caches, automatically joins detached current Core city/gate/facility state
/// with the proved native WorldPQ/LOCAL/prepared Field prefixes, and binds the
/// native visibility publication to the already advanced current Main Camera.
/// It does not tick the Camera again or invent current eligibility/custom gates.</summary>
public sealed class OriginalMapStaticModelCurrentOwnedPopulation
{
    private readonly string _revision;
    private readonly ScenePlacements _placements;
    private readonly OriginalCeCityVariantTable _variants;
    private readonly CityVisualAssetCatalog _cities;
    private readonly OriginalUnitGeometry _geometry;
    private readonly OriginalMapModelNativeVisibilityPlanner _visibility;
    private readonly object _gate = new();
    private OriginalMapStaticModelOwnedFrameInputs? _preparing;
    private Action? _invalidateSourceCreated;
    private Action? _beginSourceCreatedFrame;
    private Func<OriginalMapStaticModelOwnedVisibilityRequest, OriginalMapStaticModelSourceCreatedCurrentNode>? _borrowSourceCreatedNode;
    private OriginalMapStaticModelCurrentOwnedFrame? _sourceCreatedCurrentFrame;
    private OriginalMapStaticModelOwnedFrameInputs? _sourceCreatedCurrentInputs;
    private IReadOnlyDictionary<string, OriginalMapStaticModelInstance>? _sourceCreatedCurrentInstances;
    public OriginalMapStaticModelAssetCatalog Assets { get; }

    public OriginalMapStaticModelCurrentOwnedPopulation(OriginalGameRepository repository, LoadedSceneResources scene,
        OriginalCeCityVariantTable variants, OriginalUnitGeometry currentGeometry, string sourceRevision,
        Func<OriginalMapStaticModelOwnedVisibilityRequest, OriginalMapModelNativeVisibilityPublication> currentVisibility,
        Func<OriginalMapStaticModelOwnedViewRequest, OriginalMapModelNativeViewPublication> currentView)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(scene); ArgumentNullException.ThrowIfNull(variants);
        ArgumentNullException.ThrowIfNull(currentGeometry); ArgumentException.ThrowIfNullOrWhiteSpace(sourceRevision);
        ArgumentNullException.ThrowIfNull(currentVisibility); ArgumentNullException.ThrowIfNull(currentView);
        _revision = sourceRevision; Assets = new(repository, scene); _cities = new(repository, scene, variants);
        _placements = OriginalScenePlacements.Read(scene); _variants = variants; _geometry = currentGeometry;
        _visibility = new((instance, camera) => currentVisibility(new(_preparing ?? throw new InvalidOperationException("No owned model frame is being prepared."), instance, camera)),
            camera => currentView(new(_preparing ?? throw new InvalidOperationException("No owned model frame is being prepared."), camera)));
    }

    /// <summary>Owns the proven source-created visibility state. The complete
    /// constructor establishes sixteen1f slots. Explicit current publications
    /// override that state; current custom results are still required. Matrix
    /// edits retain the owner's visibility/flags unless separately published.</summary>
    public static OriginalMapStaticModelCurrentOwnedPopulation CreateSourceCreated(OriginalGameRepository repository,
        LoadedSceneResources scene, OriginalCeCityVariantTable variants, OriginalUnitGeometry currentGeometry,
        string sourceRevision, LoadedObjects explicitlyPublishedCommonViewSource, uint commonViewObjectId,
        string currentViewIdentity, int currentViewIndex,
        Func<OriginalMapStaticModelOwnedVisibilityRequest, OriginalMapModelNativeVisibilityPublication?> explicitCurrentVisibilityOverride,
        Func<OriginalMapStaticModelOwnedVisibilityRequest, IReadOnlyList<bool>?> completeCurrentAdditionalCullingRejects)
    {
        ArgumentNullException.ThrowIfNull(explicitCurrentVisibilityOverride); ArgumentNullException.ThrowIfNull(completeCurrentAdditionalCullingRejects);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentViewIdentity);
        if ((uint)currentViewIndex >= 16) throw new InvalidDataException("Owned Model source View index must be explicitly0..15.");
        var common = OriginalMapModelNativeVisibilitySources.ReadCommonViewTemplate(explicitlyPublishedCommonViewSource, commonViewObjectId);
        var nodes = new Dictionary<string, (OriginalMapStaticModelAsset Asset,
            OriginalMapStaticModelSourceCreatedCurrentNode Current)>(StringComparer.Ordinal);
        OriginalMapStaticModelOwnedFrameInputs? overrideFrame = null;
        var currentOverrides = new Dictionary<string, OriginalMapModelNativeVisibilityPublication?>(StringComparer.Ordinal);
        OriginalMapModelNativeVisibilityPublication? ReadCurrentOverride(OriginalMapStaticModelOwnedVisibilityRequest request)
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
        OriginalMapStaticModelSourceCreatedCurrentNode GetOrCreateNode(OriginalMapStaticModelInstance instance)
        {
            string identity = instance.CurrentSourceIdentity ?? throw new InvalidDataException("Current owned Model identity is unresolved.");
            if (!nodes.TryGetValue(identity, out var node) || !ReferenceEquals(node.Asset, instance.Asset))
            {
                if (instance.NativePublication is not {FreshConstructorBindPrefix: true, ParentBound: false})
                    throw new InvalidDataException("An unowned/current replacement Model needs its explicit current visibility publication.");
                var prefix = OriginalMapModelNativeVisibilitySources.PrepareFreshSingleNode(instance, new(identity, false, true, true));
                var words = OriginalMapModelNativeCurrentVisibilitySources.ConstructFresh(prefix,
                    sourceRevision + "/source-created-visibility/" + instance.InstanceKey);
                nodes[identity] = node = (instance.Asset, new(prefix, words));
            }
            return node.Current;
        }
        var owner = new OriginalMapStaticModelCurrentOwnedPopulation(repository, scene, variants, currentGeometry, sourceRevision, request =>
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
    public OriginalMapStaticModelSourceCreatedCurrentNode BorrowSourceCreatedCurrentNode(
        OriginalMapStaticModelCurrentOwnedFrame actualCurrentFrame, OriginalMapStaticModelInstance actualInstance)
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

    public OriginalMapStaticModelCurrentOwnedFrame Prepare(OriginalMapStaticModelCurrentOwnedInputs input)
    {
        ArgumentNullException.ThrowIfNull(input);ArgumentNullException.ThrowIfNull(input.OwnedWorld);
        // The public Territory snapshot keeps its independent-copy semantics.
        return PrepareCurrent(input,OriginalMapStaticModelCapturedOwnedWorld.FromCapturedOwnedWorld(
            input.OwnedWorld.CreateWorldCopy(),"public-independent-Territory-snapshot"),input.OwnedWorld.WorldSnapshotSha256);
    }

    /// <summary>Normal SceneView has already detached the campaign when its
    /// GameState changed. Consume that attempt's actual Model projection;
    /// current placements, visual categories, matrices and visibility are
    /// still prepared below on every request.</summary>
    public OriginalMapStaticModelCurrentOwnedFrame PrepareCapturedOwnedWorld(OriginalMapStaticModelCapturedOwnedInputs input)
    {
        ArgumentNullException.ThrowIfNull(input);ArgumentNullException.ThrowIfNull(input.CapturedOwnedWorld);
        return PrepareCurrent(input,input.CapturedOwnedWorld,"");
    }

    private OriginalMapStaticModelCurrentOwnedFrame PrepareCurrent(OriginalMapStaticModelOwnedFrameInputs input,
        OriginalMapStaticModelCapturedOwnedWorld world,string independentlyCapturedWorldSha256)
    {
        ArgumentNullException.ThrowIfNull(input.MainCamera);
        ArgumentNullException.ThrowIfNull(input.CurrentGround); ArgumentNullException.ThrowIfNull(input.GateDefinitions);
        ArgumentNullException.ThrowIfNull(input.FacilityDefinitions); ArgumentNullException.ThrowIfNull(input.CurrentNodePublications);
        lock (_gate)
        {
            if (_preparing is not null) throw new InvalidOperationException("Owned model preparation cannot reenter itself.");
            if (input.MainCamera.Camera.Source.Camera.ObjectId != OriginalMainMapCameraAdapter.MainCameraObjectId ||
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

            OriginalMapStaticModelPublication Publication(string key) => input.CurrentNodePublications.TryGetValue(key, out var current)
                ? current : OriginalMapStaticModelPublication.FreshUnparented(_revision + "/owned-node/" + key);
            var scenePopulation = OriginalMapStaticModelInstanceFactory.FromNativeScenePlacements(Assets, _placements, p =>
                Publication($"scene:{Assets.Scene.Scene.Source.ResourceUri}/{p.Source.Index}/{p.Source.Object.ObjectId:X8}"));
            var cityInputs = new List<OriginalMapStaticModelCityInput>(); var failures = new List<OriginalMapStaticModelInstanceFailure>();
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
            var gates = input.GateDefinitions.OrderBy(d => d.PhysicalSlot).Select(d => new OriginalMapStaticModelGateInput(
                d.PhysicalSlot, world.Gates[d.PhysicalSlot].HexId, d.Category, d.Heading70, Publication("gate:" + d.PhysicalSlot))).ToArray();
            var facilities = input.FacilityDefinitions.OrderBy(d => d.PhysicalSlot).Select(d =>
            {
                var structure = structures[d.PhysicalSlot];
                return new OriginalMapStaticModelFacilityInput(d.PhysicalSlot, structure.HexId, d.ObjectDefinitionSlot,
                    structure.Completed, Publication("facility:" + d.PhysicalSlot));
            }).ToArray();
            var primary = OriginalMapStaticModelInstanceFactory.FromNativePrimaryPopulation(Assets, _cities,
                cityInputs, gates, facilities, _geometry, input.CurrentGround);
            var population = OriginalMapStaticModelInstanceFactory.Combine(scenePopulation, primary,
                new(Array.Empty<OriginalMapStaticModelInstance>(), failures.AsReadOnly(), Array.Empty<int>()));
            var knownKeys = _placements.Instances.Where(p => p.Model is not null).Select(p =>
                $"scene:{Assets.Scene.Scene.Source.ResourceUri}/{p.Source.Index}/{p.Source.Object.ObjectId:X8}")
                .Concat(world.Cities.Keys.Select(id => "city:" + id)).Concat(world.Gates.Keys.Select(id => "gate:" + id))
                .Concat(structures.Keys.Select(id => "facility:" + id)).ToHashSet(StringComparer.Ordinal);
            if (input.CurrentNodePublications.Keys.Any(key => !knownKeys.Contains(key)))
                throw new InvalidDataException("Current owned Model edit names an absent or unsupported node identity.");
            if (!population.Complete)
                throw new InvalidDataException("Owned source model population is unresolved: " + string.Join("; ", population.Unresolved.Select(f => f.InstanceKey + ": " + f.Reason)));
            var deferred = OriginalPreparedCameraSceneSource.ReadCompletePrefix(input.MainCamera.Camera.Prefix.Span);
            var camera = OriginalMapStaticModelCamera.FromExplicitRows(Rows(deferred.SourceWorldToProjection),
                Rows(Matrix4x4.Transpose(deferred.SourceWorldToView))[..12]);
            _preparing = input;
            _beginSourceCreatedFrame?.Invoke();
            _sourceCreatedCurrentFrame = null; _sourceCreatedCurrentInputs = null; _sourceCreatedCurrentInstances = null;
            try
            {
                var current = new OriginalMapStaticModelCurrentOwnedFrame(input.MainCamera, population,
                    _visibility.Select(population, camera), independentlyCapturedWorldSha256, _revision)
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
            _visibility.Invalidate(); Assets.Invalidate(); _cities.Invalidate(); _invalidateSourceCreated?.Invoke();
        }
    }
}
