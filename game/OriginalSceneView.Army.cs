using System.Security.Cryptography;
using System.Text;
using Godot;
using San14.Core;
using San14.Data;

public partial class OriginalSceneView
{
    private sealed record ArmySceneSource(string ScenarioUri, string ScenarioSha256,
        string MapSha256, string FixedUri, string FixedSha256, string Language,
        string SceneUri, string SceneSha256, string OverrideFingerprint);

    private OriginalPlayableArmyVisualAdapter? _armyVisualAdapter;
    private OriginalArmyVisualLayer? _armyVisualLayer;
    private OriginalPlayableArmyPoseProvider? _armyPoseProvider;
    private OriginalGameRepository? _armyRepository;
    private ArmySceneSource? _armySceneSource;
    private OriginalRemakeArmySceneVisuals? _remakeArmyVisuals;
    private OriginalRemakeArmyNativeRenderer? _remakeArmyNativeRenderer;
    private OriginalRemakeArmyFlagVisuals? _remakeArmyFlagVisuals;
    private OriginalRemakeArmyFlagNativeRenderer? _remakeArmyFlagNativeRenderer;
    private OriginalRemakeArmyFlagMaterialProvider? _armyFlagMaterialProvider;
    private bool _armyFlagMaterialOverride;
    private OriginalRemakeArmyFlagCurrentSource? _armyFlagCurrentSource;
    private OriginalRemakeArmyFlagCurrentFrame? _armyFlagCurrentFrame;
    private bool _armyPoseOverride;
    private float _armyPresentationSeconds;
    private OriginalRemakeArmyClock? _armyClock;
    private readonly List<Task> _retiringArmyRenderers = [];

    public long OriginalArmyNativeAcceptedFrameCount => _remakeArmyNativeRenderer?.AcceptedFrameCount ?? 0;
    public int OriginalArmyNativeDrawCount => _remakeArmyNativeRenderer?.DrawCount ?? 0;
    public long OriginalArmyFlagNativeAcceptedFrameCount => _remakeArmyFlagNativeRenderer?.AcceptedFrameCount ?? 0;
    public int OriginalArmyFlagNativeDrawCount => _remakeArmyFlagNativeRenderer?.DrawCount ?? 0;
    public IReadOnlyList<OriginalRemakeArmyFlagRenderStatus> OriginalArmyFlagNativeStatuses =>
        _remakeArmyFlagNativeRenderer?.Statuses ?? [];

    public IReadOnlyDictionary<int, OriginalRemakeArmyVisualFrame> RemakeArmyFrames =>
        _remakeArmyVisuals?.Frames ?? new Dictionary<int, OriginalRemakeArmyVisualFrame>();
    public IReadOnlyDictionary<int, OriginalRemakeArmyFlagFrame> RemakeArmyFlagFrames =>
        _remakeArmyFlagVisuals?.Frames ?? new Dictionary<int, OriginalRemakeArmyFlagFrame>();

    /// <summary>Real native G-buffer submission under the parent native scene's
    /// accepted frame gate, after terrain and before deferred. Current pose is
    /// remake-created source state; the accepted source camera is mandatory.</summary>
    public async Task RenderOriginalArmyNativeAsync(OriginalNativeMapFrameContext context)
    {
        if (_remakeArmyVisuals is null || _armyPoseOverride) return;
        _remakeArmyNativeRenderer ??= new(_repository);
        await _remakeArmyNativeRenderer.RenderAsync(context, _remakeArmyVisuals, _armyPresentationSeconds);
        if (_remakeArmyFlagVisuals is not null)
        {
            _remakeArmyFlagNativeRenderer ??= new(_repository);
            _remakeArmyFlagNativeRenderer.SourceMaterialProvider=CurrentArmyFlagMaterialProvider();
            await _remakeArmyFlagNativeRenderer.RenderAsync(context, _remakeArmyFlagVisuals, _armyPresentationSeconds);
        }
    }

    /// <summary>Explicit source force variant, Force+49 and native selected or
    /// carried text route for the owned fresh flag producer. Null retains the
    /// complete pole consumer and unavailable cloth status. This does not infer
    /// original force fields from a Core ID/name or alter the existing signs.</summary>
    public void SetOriginalArmyFlagMaterialProvider(OriginalRemakeArmyFlagMaterialProvider? provider)
    {
        _armyFlagMaterialOverride=true;
        _armyFlagMaterialProvider=provider;
        if(_remakeArmyFlagNativeRenderer is not null)_remakeArmyFlagNativeRenderer.SourceMaterialProvider=provider;
    }

    public void UseOriginalArmyFlagSourceInputs()
    {
        _armyFlagMaterialOverride=false;
        if(_logicalWorld is GameState world)RefreshOriginalArmyFlagMaterialInputs(world);
        if(_remakeArmyFlagNativeRenderer is not null)
            _remakeArmyFlagNativeRenderer.SourceMaterialProvider=CurrentArmyFlagMaterialProvider();
    }

    private OriginalRemakeArmyFlagMaterialProvider? CurrentArmyFlagMaterialProvider()=>
        _armyFlagMaterialOverride?_armyFlagMaterialProvider:
        _armyFlagCurrentFrame is { } frame?frame.Select:null;

    public async Task RetireOriginalArmyNativeTargetAsync()
    {
        if (_remakeArmyNativeRenderer is not null) await _remakeArmyNativeRenderer.RetireTargetAsync();
        if (_remakeArmyFlagNativeRenderer is not null) await _remakeArmyFlagNativeRenderer.RetireTargetAsync();
        Task[] pending = _retiringArmyRenderers.ToArray();
        if (pending.Length != 0) await Task.WhenAll(pending);
        _retiringArmyRenderers.RemoveAll(task => task.IsCompleted);
    }

    public OriginalPlayableArmyVisualPlan? OriginalArmyPlan { get; private set; }
    public IReadOnlyList<OriginalArmyVisualStatus> OriginalArmyStatuses { get; private set; } = [];
    public OriginalArmyVisualLayer? ArmyVisualLayer => _armyVisualLayer;

    /// <summary>Attach an explicit caller pose source for individual verified
    /// descriptors, overriding the owned remake-created ordinary route. Null
    /// disables geometry and retains source selections and the existing signs.
    /// Current original process instance state remains unavailable.
    /// A provider receives fresh source selections on every playable refresh.</summary>
    public void SetOriginalArmyPoseProvider(OriginalPlayableArmyPoseProvider? provider)
    {
        if (_armyVisualAdapter is not null && _armyVisualLayer is not null &&
            _logicalWorld is GameState world && _projection is not null && _terrainMeshes is not null)
        {
            // Validate a replacement before changing the stored provider or
            // any live component. The layer also validates the complete pose set.
            OriginalPlayableArmyVisualPlan plan = _armyVisualAdapter.Resolve(world, provider);
            OriginalArmyStatuses = _armyVisualAdapter.Apply(_armyVisualLayer,
                _projection, _terrainMeshes, world, plan);
            OriginalArmyPlan = plan;
            _armyPoseProvider = provider;
            _armyPoseOverride = true;
            RetireRemakeArmyConstruction();
            PublishOriginalArmyState(world);
        }
        _armyPoseProvider = provider;
        _armyPoseOverride = true;
        RetireRemakeArmyConstruction();
    }

    private void RefreshOriginalArmyVisuals(GameState world)
    {
        if (_acceptHex is null || _projection is null || _terrainMeshes is null ||
            !PlayableSkeleton.IsPlayableRuleset(world.RulesetId))
        {
            ReleaseOriginalArmyVisuals();
            return;
        }
        string language = _text.SourceLocale;
        string fixedUri = GameResourceId.File($"0010_{language}/fixdataexce.s14").ToString();
        world.ContentHashes.TryGetValue(world.ScenarioKey, out string? scenarioHash);
        world.ContentHashes.TryGetValue(fixedUri, out string? fixedHash);
        var source = new ArmySceneSource(world.ScenarioKey, scenarioHash ?? "", world.MapSha256,
            fixedUri, fixedHash ?? "", language, _loadedSceneResources?.Scene.Source.ResourceUri ?? "",
            _loadedSceneResources?.Scene.Source.DecodedSha256 ?? "", ArmyOverrideFingerprint());
        if (_armySceneSource != source || !ReferenceEquals(_armyRepository, _repository))
        {
            ReleaseOriginalArmyVisuals();
            try
            {
                LoadedMap map = _repository.LoadCeMap();
                var adapter = OriginalPlayableArmyVisualAdapter.Load(_repository, map, language);
                // Source identity changes retire old assets before a replacement
                // is admitted. Unsupported map/table profiles keep unit signs.
                _remakeArmyVisuals = new(_repository, language, _projection, _terrainMeshes.Source.HeightField);
                OriginalPlayableArmyPoseProvider? poses = _armyPoseOverride ? _armyPoseProvider :
                    _remakeArmyVisuals.Prepare(world, adapter.Resolve(world));
                OriginalPlayableArmyVisualPlan plan = adapter.Resolve(world, poses);
                var layer = new OriginalArmyVisualLayer();
                layer.Initialize(_repository, _texturePolicy);
                _previewWorld.AddChild(layer);
                _armyClock = new()
                {
                    Name = "RemakeArmyAnimationClock",
                    IsRunning = () => _loaded && !_detailsOpen,
                    Advance = seconds => _armyPresentationSeconds += seconds
                };
                layer.AddChild(_armyClock);
                _armyVisualAdapter = adapter;
                _armyVisualLayer = layer;
                _armyRepository = _repository;
                _armySceneSource = source;
                OriginalArmyStatuses = adapter.Apply(layer, _projection, _terrainMeshes, world, plan);
                OriginalArmyPlan = plan;
                RemoveMeta("original_army_source_unavailable_reason");
            }
            catch (Exception error) when (error is InvalidDataException or IOException or
                                          KeyNotFoundException or ArgumentException or OverflowException)
            {
                ReleaseOriginalArmyVisuals();
                SetMeta("original_army_source_unavailable_reason", error.Message);
                SetMeta("original_army_unit_markers_retained", true);
                SetMeta("original_army_source_selection_count", 0);
                SetMeta("original_army_model_component_count", 0);
                SetMeta("original_army_unresolved_unit_count", world.Units.Count);
                return;
            }
        }
        else
        {
            OriginalPlayableArmyPoseProvider? poses = _armyPoseOverride ? _armyPoseProvider :
                _remakeArmyVisuals!.Prepare(world, _armyVisualAdapter!.Resolve(world));
            OriginalPlayableArmyVisualPlan plan = _armyVisualAdapter!.Resolve(world, poses);
            OriginalArmyStatuses = _armyVisualAdapter.Apply(_armyVisualLayer!, _projection, _terrainMeshes, world, plan);
            OriginalArmyPlan = plan;
        }
        RefreshOriginalArmyFlagVisuals(world);
        PublishOriginalArmyState(world);
    }

    private void RefreshOriginalArmyFlagVisuals(GameState world)
    {
        if (_armyPoseOverride || _remakeArmyVisuals is null)
        {
            RetireRemakeArmyFlags();
            return;
        }
        try
        {
            _remakeArmyFlagVisuals ??= new(_repository);
            _remakeArmyFlagVisuals.Prepare(_remakeArmyVisuals.Frames);
            RemoveMeta("original_army_flag_source_unavailable_reason");
        }
        catch (Exception error) when (error is InvalidDataException or IOException or
                                      KeyNotFoundException or ArgumentException or OverflowException)
        {
            RetireRemakeArmyFlags();
            SetMeta("original_army_flag_source_unavailable_reason", error.Message);
            return;
        }
        RefreshOriginalArmyFlagMaterialInputs(world);
    }

    private void RefreshOriginalArmyFlagMaterialInputs(GameState world)
    {
        _armyFlagCurrentFrame=null;
        if(_armyFlagMaterialOverride||_remakeArmyFlagVisuals is null||_remakeArmyFlagVisuals.Frames.Count==0)
        {
            RemoveMeta("original_army_flag_material_source_unavailable_reason");
            return;
        }
        try
        {
            _armyFlagCurrentSource??=OriginalRemakeArmyFlagCurrentSource.Load(_repository,world,_text.SourceLocale);
            _armyFlagCurrentFrame=_armyFlagCurrentSource.Prepare(world,_remakeArmyFlagVisuals.Frames.Keys);
            RemoveMeta("original_army_flag_material_source_unavailable_reason");
        }
        catch(Exception error)when(error is InvalidDataException or IOException or KeyNotFoundException or ArgumentException or OverflowException)
        {
            // Source text/color failure leaves the complete pole source owner
            // and unit signs admitted. No substitute primary is constructed.
            SetMeta("original_army_flag_material_source_unavailable_reason",error.Message);
        }
    }

    private void PublishOriginalArmyState(GameState world)
    {
        if (OriginalArmyPlan is not OriginalPlayableArmyVisualPlan plan) return;
        SetMeta("original_army_source_selection_count", plan.Selections.Count);
        SetMeta("original_army_model_component_count", _armyVisualLayer?.ModelCount ?? 0);
        SetMeta("original_army_unresolved_unit_count", plan.Unresolved.Select(item => item.UnitId)
            .Concat(OriginalArmyStatuses.Where(item => !item.HasSourceModel).Select(item => item.UnitId)).Distinct().Count());
        SetMeta("original_army_pose_provider_present", _armyPoseProvider is not null || plan.Bindings.Count != 0);
        SetMeta("original_army_unit_markers_retained", plan.PreserveUnitMarkers);
        SetMeta("original_army_complete_assembly_verified", false);
        SetMeta("original_army_world_pose_verified", false);
        SetMeta("original_army_current_animation_verified", false);
        SetMeta("original_army_world_period", world.PeriodNumber);
        SetMeta("original_army_remake_created_prefix", !_armyPoseOverride);
        SetMeta("original_army_source_ordinary_actor_count", RemakeArmyFrames.Values.Sum(frame => frame.Publications.Count));
        SetMeta("original_army_source_flag_actor_count", RemakeArmyFlagFrames.Values.Sum(frame => frame.Actors.Count));
        SetMeta("original_army_flag_source_force_binding_count",_armyFlagCurrentFrame?.Bindings.Count??0);
        SetMeta("original_army_complete_flag_assembly_verified", false);
        var selections = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        foreach (OriginalPlayableArmySelection selection in plan.Selections.Values.OrderBy(item => item.UnitId))
        {
            var descriptors = new Godot.Collections.Array<Godot.Collections.Dictionary>();
            foreach (OriginalArmyDescriptorRequest request in selection.Descriptors.Descriptors)
                descriptors.Add(new()
                {
                    ["descriptor_offset"] = request.DescriptorOffset,
                    ["source_category"] = request.Category ?? -1,
                    ["requested_resource_count"] = request.RequestedResourceCount,
                    ["static_flags"] = (long)request.Flags
                });
            selections.Add(new()
            {
                ["unit_id"] = selection.UnitId,
                ["hex_id"] = selection.HexId,
                ["explicit_land_slot"] = selection.ExplicitLandFormationSlot,
                ["created_byte1b"] = (int)selection.Creation.Bytes.Byte1B!.Value,
                ["formation_uri"] = selection.Creation.FormationSource.ResourceUri,
                ["formation_sha256"] = selection.Creation.FormationSource.Sha256,
                ["formation_decoded_sha256"] = selection.Creation.FormationDecodedSha256,
                ["source_rule_version"] = selection.Creation.CodeSource.RuleVersion,
                ["possible_modes"] = selection.Descriptors.PossibleModes.ToArray(),
                ["descriptors"] = descriptors,
                ["current_native_army_bytes_verified"] = false,
                ["original_instance_poses_verified"] = false
            });
        }
        SetMeta("original_army_source_selections", selections);
        if (_armySceneSource is ArmySceneSource source)
        {
            SetMeta("original_army_fixed_source_uri", source.FixedUri);
            SetMeta("original_army_fixed_source_sha256", source.FixedSha256);
            SetMeta("original_army_override_fingerprint", source.OverrideFingerprint);
        }
    }

    private string ArmyOverrideFingerprint()
    {
        string? root = _repository.OverrideDirectory;
        if (root is null || !Directory.Exists(root)) return "";
        var paths = new List<string>();
        foreach (string archive in new[] { "CharacterEditor", "MaterialEditor", "RRPreview" })
        {
            string directory = Path.Combine(root, "rdb", archive);
            if (Directory.Exists(directory)) paths.AddRange(Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories));
        }
        foreach (GameResourceId id in new[]
        {
            GameResourceId.Rdb("KIDSSystemResource", OriginalCeArmyModels.CatalogId),
            GameResourceId.Rdb("KIDSSystemResource", OriginalCeArmyModels.DictionaryId),
            GameResourceId.Rdb("KIDSSystemResource", OriginalArmyAnimationResources.SingletonId),
            GameResourceId.Rdb("KIDSSystemResource", OriginalArmyFlagMaterialVariations.MaterialDictionaryId),
            GameResourceId.Rdb("KIDSSystemResource", 0x208BE13A),
            GameResourceId.Rdb("system", OriginalArmyAnimatedGbufferSource.GbufferShaderFileId),
            GameResourceId.Rdb("system", OriginalArmyFlagMaterialVariations.GbufferShaderId),
            GameResourceId.Rdb("system", 0x718CCB8B),
            GameResourceId.Rdb("system", 0x024496EE),
            GameResourceId.File("0000/MapExCe.bin"),
            GameResourceId.File($"0010_{_text.SourceLocale}/fixdataexce.s14")
        })
        {
            string path = Path.Combine(root, id.OverrideRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(path)) paths.Add(path);
        }
        if(_logicalWorld is GameState current)
        {
            var scenarioId=GameResourceId.Parse(current.ScenarioKey);
            if(scenarioId.Kind=="file")
            {
                string scenarioPath=Path.Combine(root,scenarioId.OverrideRelativePath.Replace('/',Path.DirectorySeparatorChar));
                if(System.IO.File.Exists(scenarioPath))paths.Add(scenarioPath);
            }
        }
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (string path in paths.Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase))
        {
            hash.AppendData(Encoding.UTF8.GetBytes(Path.GetRelativePath(root, path).Replace('\\', '/')));
            using FileStream file = System.IO.File.OpenRead(path);
            hash.AppendData(SHA256.HashData(file));
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private void ReleaseOriginalArmyVisuals()
    {
        RetireRemakeArmyConstruction();
        ReleaseOriginalArmyLayer();
    }

    private void RetireRemakeArmyConstruction()
    {
        RetireRemakeArmyFlags();
        _armyFlagCurrentSource=null;
        _remakeArmyVisuals?.Dispose(); _remakeArmyVisuals = null;
        if (_remakeArmyNativeRenderer is not null)
        {
            _retiringArmyRenderers.Add(RetireRemakeArmyRendererAsync(_remakeArmyNativeRenderer));
            _remakeArmyNativeRenderer = null;
        }
    }

    private void RetireRemakeArmyFlags()
    {
        _armyFlagCurrentFrame=null;
        _remakeArmyFlagVisuals?.Dispose(); _remakeArmyFlagVisuals = null;
        if (_remakeArmyFlagNativeRenderer is not null)
        {
            _retiringArmyRenderers.Add(RetireRemakeArmyFlagRendererAsync(_remakeArmyFlagNativeRenderer));
            _remakeArmyFlagNativeRenderer = null;
        }
    }

    private void ReleaseOriginalArmyLayer()
    {
        if (_armyVisualLayer is not null)
        {
            _armyVisualLayer.ReleaseResources();
            _armyVisualLayer.GetParent()?.RemoveChild(_armyVisualLayer);
            _armyVisualLayer.Free();
            _armyVisualLayer = null;
        }
        _armyClock = null;
        _armyVisualAdapter = null;
        _armyRepository = null;
        _armySceneSource = null;
        OriginalArmyPlan = null;
        OriginalArmyStatuses = [];
        foreach (string name in new[] { "original_army_source_selection_count", "original_army_model_component_count",
            "original_army_unresolved_unit_count", "original_army_pose_provider_present", "original_army_unit_markers_retained",
            "original_army_complete_assembly_verified", "original_army_world_pose_verified", "original_army_current_animation_verified",
            "original_army_world_period", "original_army_source_selections", "original_army_fixed_source_uri",
            "original_army_fixed_source_sha256", "original_army_override_fingerprint", "original_army_source_unavailable_reason" })
            if (HasMeta(name)) RemoveMeta(name);
        RemoveMeta("original_army_remake_created_prefix");
        RemoveMeta("original_army_source_ordinary_actor_count");
        RemoveMeta("original_army_source_flag_actor_count");
        RemoveMeta("original_army_complete_flag_assembly_verified");
        RemoveMeta("original_army_flag_source_unavailable_reason");
        RemoveMeta("original_army_flag_material_source_unavailable_reason");
        RemoveMeta("original_army_flag_source_force_binding_count");
    }

    private static async Task RetireRemakeArmyRendererAsync(OriginalRemakeArmyNativeRenderer renderer)
    {
        try { await renderer.DisposeAsync(); }
        catch (Exception error) { GD.PushError($"Army renderer retirement failed: {error}"); throw; }
    }

    private static async Task RetireRemakeArmyFlagRendererAsync(OriginalRemakeArmyFlagNativeRenderer renderer)
    {
        try { await renderer.DisposeAsync(); }
        catch (Exception error) { GD.PushError($"Army flag renderer retirement failed: {error}"); throw; }
    }
}
