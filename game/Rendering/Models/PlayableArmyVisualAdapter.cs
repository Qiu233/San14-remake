using System.Collections.ObjectModel;
using Godot;
using San14.Core;
using San14.Data;

/// <summary>An active owned type2 receiver's current formation bytes. This is
/// not an ordinary sortie writer result or a model field in simulation state.</summary>
public sealed record PlayableTransportArmySource(int NativeSlot, ArmyFormationBytes Bytes,
    string FormationDecodedSha256);

/// <summary>Ordinary creation selection or explicitly owned current transport
/// selection, joined to actual terrain, formation definitions and descriptors.</summary>
public sealed record PlayableArmySelection(int UnitId, int HexId,
    int ExplicitLandFormationSlot, ArmyFormationWriteResult? Creation,
    ArmyTerrainSelection Terrain, ArmyFormationSelection Formation,
    ArmyContextIndependentDescriptors Descriptors)
{
    public PlayableTransportArmySource? Transport { get; init; }
    public ArmyFormationBytes CurrentBytes => Transport?.Bytes ?? Creation?.Bytes
        ?? throw new InvalidDataException("Army visual selection has no formation source.");
    public bool CurrentNativeArmyBytesVerified => false;
    public bool InstancePosesFidelityVerified => false;
    public bool DynamicFlagsFidelityVerified => false;
}

public sealed record PlayableArmyVisualFailure(int UnitId, string Reason);

/// <summary>The provider selects individual components and supplies their exact
/// caller-owned poses. Resource request counts do not generate instances.</summary>
public delegate IReadOnlyList<ArmyComponentVisualBinding>? PlayableArmyPoseProvider(
    UnitState unit, PlayableArmySelection sourceSelection);

public sealed class PlayableArmyVisualPlan
{
    public IReadOnlyDictionary<int, PlayableArmySelection> Selections { get; }
    public IReadOnlyDictionary<int, IReadOnlyList<ArmyComponentVisualBinding>> Bindings { get; }
    public IReadOnlyList<PlayableArmyVisualFailure> Unresolved { get; }
    internal PlayableArmyVisualAdapter? SourceAdapter { get; }
    internal GameState? SourceWorld { get; }

    // Detached callers may prepare an explicit subset, but only the adapter
    // can publish a bound plan for its accepted world/resource lifetime.
    public PlayableArmyVisualPlan(IReadOnlyDictionary<int, PlayableArmySelection> selections,
        IReadOnlyDictionary<int, IReadOnlyList<ArmyComponentVisualBinding>> bindings,
        IReadOnlyList<PlayableArmyVisualFailure> unresolved)
        : this(selections, bindings, unresolved, null, null) { }

    internal PlayableArmyVisualPlan(IReadOnlyDictionary<int, PlayableArmySelection> selections,
        IReadOnlyDictionary<int, IReadOnlyList<ArmyComponentVisualBinding>> bindings,
        IReadOnlyList<PlayableArmyVisualFailure> unresolved,
        PlayableArmyVisualAdapter? sourceAdapter, GameState? sourceWorld)
    {
        (Selections, Bindings, Unresolved, SourceAdapter, SourceWorld) =
            (selections, bindings, unresolved, sourceAdapter, sourceWorld);
    }

    public bool PreserveUnitMarkers => true;
    public bool CompleteAssemblyFidelityVerified => false;
}

/// <summary>Projects the verified ordinary land creation chain for source-bound
/// playable units, then delegates individual poses to the caller. Owns no meshes,
/// simulation state or resource cache; OriginalArmyVisualLayer owns the display
/// lifecycle and reloads asset overrides when that layer is recreated.</summary>
public sealed class PlayableArmyVisualAdapter
{
    private readonly LoadedMap _map;
    private readonly LoadedTerrainTypes _terrainTypes;
    private readonly LoadedSkillDefinitions _definitions;
    private readonly ArmyFormationWriterSource _writer;

    public PlayableArmyVisualAdapter(LoadedMap map, LoadedTerrainTypes terrainTypes,
        LoadedSkillDefinitions definitions, string language)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(terrainTypes);
        ArgumentNullException.ThrowIfNull(definitions);
        if (terrainTypes.Catalog is null || terrainTypes.Catalog.FormatMarker != 0x66 ||
            terrainTypes.Catalog.RecordOffset != 182121 || terrainTypes.Catalog.RecordStride != 51)
            throw new InvalidDataException("Playable Army terrain selection requires the CE source 31x51 fixed table.");
        // Validate the complete fixed-size source profile before accepting a
        // playable Hex; an expanded map cannot be truncated to native ushort.
        _ = ArmyNativeFormationQueries.Terrain(map, terrainTypes, 0);
        _writer = ArmyFormationWriter.ReadSource(definitions, language);
        if (definitions.Source.ResourceUri != GameResourceId.File($"0010_{language}/fixdataexce.s14").ToString() ||
            terrainTypes.Source.ResourceUri != definitions.Source.ResourceUri ||
            !SameHash(terrainTypes.Source.Sha256, definitions.Source.Sha256) ||
            !SameHash(terrainTypes.Catalog.DecodedSha256, definitions.Formations.DecodedSha256))
            throw new InvalidDataException("Playable Army terrain and formation definitions require the same fixed-data source.");
        (_map, _terrainTypes, _definitions) = (map, terrainTypes, definitions);
    }

    public static PlayableArmyVisualAdapter Load(GameResourceRepository repository,
        LoadedMap map, string language)
    {
        ArgumentNullException.ThrowIfNull(repository);
        if (language is not ("SC" or "TC"))
            throw new InvalidDataException("Playable Army source language must be SC or TC.");
        string path = $"0010_{language}/fixdataexce.s14";
        return new(map, repository.LoadTerrainTypes(path), repository.LoadSkillDefinitions(path), language);
    }

    /// <summary>Current owned native formation bytes select the source table
    /// and descriptors. FormationId does not substitute for byte1B/1C. Water
    /// models and special effect context retain their separate producers.</summary>
    public PlayableArmyVisualPlan Resolve(GameState world,
        PlayableArmyPoseProvider? poseProvider = null)
    {
        ValidateWorld(world);
        var selections = new Dictionary<int, PlayableArmySelection>();
        var unresolved = new List<PlayableArmyVisualFailure>();
        var lifecycle = world.ReadArmyLifecycle();
        foreach (var (id, unit) in world.Units.OrderBy(item => item.Key))
        {
            if (unit is null || id != unit.Id || id < 0 ||
                (uint)unit.HexId >= (uint)_map.Map.Cells.Length)
                throw new InvalidDataException("Playable Army state has a mismatched unit identity or invalid source Hex.");
            bool transport = unit.NativeTacticRuntime?.Status10 == 2;
            int transportSlot = -1;
            if (transport)
            {
                if (unit.NativeArmyOrigin?.NativeSlotIndex is not int physical || physical is < 1 or > 500 ||
                    lifecycle is null || (uint)physical >= (uint)lifecycle.Slots.Count ||
                    lifecycle.Slots[physical].PhysicalSlot != physical ||
                    lifecycle.Receivers.Find(lifecycle.Slots[physical].ReceiverId) is not
                        { Phase: ArmyReceiverPhase.Active, ActiveUnitId: int active } || active != unit.Id)
                {
                    unresolved.Add(new(unit.Id, "Transport current active Army receiver is unavailable."));
                    continue;
                }
                transportSlot = physical;
            }
            else if (unit.Mission?.Kind == UnitMissionKind.Transport)
            {
                unresolved.Add(new(unit.Id, "Transport current type2 receiver is unavailable."));
                continue;
            }
            if (ArmyConfigurationSourceAdapter.ReadCurrentLand(world, unit) is not byte landByte)
            {
                unresolved.Add(new(unit.Id, "Current native Army.byte1B is unavailable."));
                continue;
            }
            int slot=landByte<=20?landByte:0;
            if(!world.Formations.TryGetValue(slot,out FormationDefinition? formation)||formation.Id!=slot||!MatchesSourceFormation(formation,slot))
                throw new InvalidDataException($"Playable Army {unit.Id} formation differs from the selected source table.");
            ArmyTerrainSelection terrain = ArmyNativeFormationQueries.Terrain(
                _map, _terrainTypes, checked((ushort)unit.HexId));
            if (transport && !terrain.UsesArmyByte1C)
            {
                unresolved.Add(new(unit.Id, "Transport land model context requires the actual persistent tactic-effect manager; modes0/3 remain unresolved."));
                continue;
            }
            if (terrain.UsesArmyByte1C && ArmyConfigurationSourceAdapter.ReadNaval(world, unit) is null)
            {
                unresolved.Add(new(unit.Id, "Water terrain requires the current native naval byte1C; the unit marker remains visible."));
                continue;
            }
            if (!terrain.UsesArmyByte1C && _writer.NativeFields7E[slot] == 2)
            {
                unresolved.Add(new(unit.Id, "A naval formation choice does not establish an ordinary land sortie; the unit marker remains visible."));
                continue;
            }
            var bytes = new ArmyFormationBytes(landByte, ArmyConfigurationSourceAdapter.ReadNaval(world, unit));
            //Type2 was created by its separate real creator. Do not manufacture
            //an ordinary LandWritten/NavalWritten receipt for its current bytes.
            ArmyFormationWriteResult? creation = transport ? null : new(bytes,
                true,false,_writer.Source,_writer.DecodedSha256,_writer.CodeSource);
            ArmyFormationSelection selected = ArmyNativeFormationQueries.Formation(
                terrain, _definitions, bytes.Byte1B, bytes.Byte1C);
            var descriptors = ArmyModelSelection.DescribeContextIndependent(
                terrain, bytes.Byte1B, bytes.Byte1C);
            var selection = new PlayableArmySelection(unit.Id, unit.HexId, slot, creation,
                terrain, selected, descriptors)
                { Transport = transport ? new(transportSlot, bytes, _definitions.Formations.DecodedSha256) : null };
            selections.Add(unit.Id, selection);
        }
        return BindPosesCore(world, new ReadOnlyDictionary<int, PlayableArmySelection>(selections),
            unresolved, poseProvider);
    }

    /// <summary>Bind current poses to the already selected source descriptors.
    /// Accepted game worlds are replaced by transactions; callers retain this
    /// plan only for that world and adapter, just like the native frame cache.</summary>
    public PlayableArmyVisualPlan BindPoses(GameState world, PlayableArmyVisualPlan sourcePlan,
        PlayableArmyPoseProvider? poseProvider)
    {
        RequireCurrentPlan(world, sourcePlan);
        return BindPosesCore(world, sourcePlan.Selections,
            sourcePlan.Unresolved.Where(item => !sourcePlan.Selections.ContainsKey(item.UnitId)), poseProvider);
    }

    private PlayableArmyVisualPlan BindPosesCore(GameState world,
        IReadOnlyDictionary<int, PlayableArmySelection> selections,
        IEnumerable<PlayableArmyVisualFailure> sourceFailures, PlayableArmyPoseProvider? poseProvider)
    {
        var bindings = new Dictionary<int, IReadOnlyList<ArmyComponentVisualBinding>>();
        var unresolved = new List<PlayableArmyVisualFailure>(sourceFailures);
        foreach (var (id, selection) in selections)
        {
            IReadOnlyList<ArmyComponentVisualBinding>? components = poseProvider?.Invoke(world.Units[id], selection);
            if (components is null || components.Count == 0)
            {
                unresolved.Add(new(id, "Source land descriptors are known; individual world pose and scale are unavailable, so the unit marker remains visible."));
                continue;
            }
            ArmyComponentVisualBinding[] snapshot = components.ToArray();
            ValidateComponents(selection, snapshot);
            bindings.Add(id, Array.AsReadOnly(snapshot));
        }
        return new(selections,
            new ReadOnlyDictionary<int, IReadOnlyList<ArmyComponentVisualBinding>>(bindings),
            unresolved.AsReadOnly(), this, world);
    }

    private void RequireCurrentPlan(GameState world, PlayableArmyVisualPlan plan)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(plan);
        if (!ReferenceEquals(plan.SourceAdapter, this) || !ReferenceEquals(plan.SourceWorld, world))
            throw new InvalidDataException("Playable Army visual plan belongs to another world or source adapter.");
    }

    /// <summary>Explicit opt-in display policy: one source geometry at a caller
    /// marker pose, retaining the existing sign. This does not reconstruct a
    /// formation or assign a native instance identity. Ordinary infantry category0
    /// and siege category4..6 are the only representatives selected here.</summary>
    public static PlayableArmyPoseProvider CreateRepresentativeMarkerProvider(
        Quaternion rotation, Vector3 scale, Vector3 offset, string callerPoseSource)
    {
        ValidatePose(rotation, scale, offset, callerPoseSource);
        return (_, selection) =>
        {
            ArmyDescriptorRequest? descriptor = selection.Descriptors.Descriptors.FirstOrDefault(
                item => !item.Cleared && item.Category is 0 or 4 or 5 or 6);
            if (descriptor?.Category is not int category) return [];
            return [new ArmyComponentVisualBinding("source-representative-marker",
                new(category, rotation, scale,
                    $"ordinary-created-land-source-descriptor/{descriptor.DescriptorOffset:X}; caller-marker-pose: {callerPoseSource}"), offset)
                { SourceDescriptorOffset = descriptor.DescriptorOffset }];
        };
    }

    /// <summary>Apply the prepared components for this adapter's accepted world.
    /// Resource selection and component validation belong to Resolve/BindPoses;
    /// applying their immutable publication does not repeat that work.</summary>
    public IReadOnlyList<ArmyVisualStatus> Apply(ArmyVisualLayer layer,
        MapWorldProjection projection, TerrainMeshResult terrain, GameState world,
        PlayableArmyVisualPlan plan)
    {
        ArgumentNullException.ThrowIfNull(layer);
        RequireCurrentPlan(world, plan);
        IReadOnlyList<ArmyVisualStatus> statuses = layer.UpdateComponents(projection, terrain, world, plan.Bindings);
        foreach ((int id, IReadOnlyList<ArmyComponentVisualBinding> components) in plan.Bindings)
            foreach (ArmyComponentVisualBinding component in components)
            {
                MeshInstance3D? model = layer.FindComponentModel(id, component.ComponentId);
                if (model is null) continue;
                PlayableArmySelection selection = plan.Selections[id];
                ArmyDescriptorRequest request = selection.Descriptors.Descriptors.Single(
                    item => item.DescriptorOffset == component.SourceDescriptorOffset);
                model.SetMeta("source_ordinary_land_creation_projection", selection.Transport is null);
                model.SetMeta("source_owned_transport_current_selection", selection.Transport is not null);
                model.SetMeta("source_selected_land_slot", selection.ExplicitLandFormationSlot);
                model.SetMeta("source_created_army_byte1b", selection.Transport is null ? (int)selection.CurrentBytes.Byte1B!.Value : -1);
                model.SetMeta("source_current_army_byte1b", (int)selection.CurrentBytes.Byte1B!.Value);
                model.SetMeta("source_current_army_byte1c", selection.CurrentBytes.Byte1C is byte currentNaval ? (int)currentNaval : -1);
                model.SetMeta("source_formation_uri", _definitions.Source.ResourceUri);
                model.SetMeta("source_formation_sha256", _definitions.Source.Sha256);
                model.SetMeta("source_formation_decoded_sha256", selection.Transport?.FormationDecodedSha256 ?? _writer.DecodedSha256);
                model.SetMeta("source_formation_is_override", _definitions.Source.IsOverride);
                model.SetMeta("source_selection_rule_version", selection.Transport is null
                    ? _writer.CodeSource.RuleVersion : world.Units[id].NativeArmyOrigin!.RuleVersion);
                model.SetMeta("source_selection_code_language", selection.Transport is null ? _writer.CodeSource.Language : world.Units[id].NativeArmyOrigin!.Language);
                model.SetMeta("source_hex_surface_byte", (int)selection.Terrain.RawHexTerrainByte10);
                model.SetMeta("source_descriptor_requested_resource_count", request.RequestedResourceCount);
                model.SetMeta("source_descriptor_static_flags", (long)request.Flags);
                model.SetMeta("source_selection_context_independent", true);
                model.SetMeta("current_native_army_bytes_verified", false);
                model.SetMeta("source_unit_marker_retained", true);
                model.SetMeta("original_dynamic_descriptor_flags_verified", false);
                model.SetMeta("original_instance_count_verified", false);
            }
        layer.SetMeta("source_playable_land_selection_count", plan.Selections.Count);
        layer.SetMeta("source_unit_markers_retained", true);
        return statuses;
    }

    private void ValidateWorld(GameState world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (!PlayableSkeleton.IsPlayableRuleset(world.RulesetId) ||
            world.MapWidth != _map.Map.Width || world.MapHeight != _map.Map.Height ||
            !SameHash(world.MapSha256, _map.Source.Sha256) ||
            !world.ContentHashes.TryGetValue(_map.Source.ResourceUri, out string? mapHash) ||
            !SameHash(mapHash, _map.Source.Sha256) ||
            !world.ContentHashes.TryGetValue(_definitions.Source.ResourceUri, out string? fixedHash) ||
            !SameHash(fixedHash, _definitions.Source.Sha256))
            throw new InvalidDataException("Playable Army selection requires the active playable ruleset and matching map/fixed-data source hashes.");
    }

    private bool MatchesSourceFormation(FormationDefinition formation, int slot)
    {
        byte[] raw = _definitions.Formations.Formations[slot].RawParameterBytes;
        return formation.SourceBase == new FormationBaseCoefficients(raw[1], raw[5], raw[6], raw[7], raw[8]);
    }

    private static void ValidateComponents(PlayableArmySelection selection,
        IReadOnlyList<ArmyComponentVisualBinding> components)
    {
        if (components is null) throw new InvalidDataException("Playable Army component list is unknown.");
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (ArmyComponentVisualBinding component in components)
        {
            if (component is null || component.Model is null || string.IsNullOrWhiteSpace(component.ComponentId) ||
                !keys.Add(component.ComponentId) || component.SourceInstanceIndex is < 0 ||
                !selection.Descriptors.Descriptors.Any(request => !request.Cleared &&
                    request.DescriptorOffset == component.SourceDescriptorOffset && request.Category == component.Model.Category))
                throw new InvalidDataException("Playable Army components require unique caller identities and one matching source descriptor/category.");
            ValidatePose(component.Model.Rotation, component.Model.Scale,
                component.WorldPositionOffset, component.Model.SelectionSource);
        }
    }

    private static void ValidatePose(Quaternion rotation, Vector3 scale, Vector3 offset, string source)
    {
        if (!rotation.IsFinite() || MathF.Abs(rotation.LengthSquared() - 1) > 0.0001f ||
            !scale.IsFinite() || scale.X <= 0 || scale.Y <= 0 || scale.Z <= 0 ||
            !offset.IsFinite() || string.IsNullOrWhiteSpace(source))
            throw new InvalidDataException("Playable Army poses require an explicit normalized rotation, positive finite scale, finite offset and caller source.");
    }

    private static bool SameHash(string? first, string second) =>
        string.Equals(first, second, StringComparison.OrdinalIgnoreCase);
}
