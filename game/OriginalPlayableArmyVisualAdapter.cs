using System.Collections.ObjectModel;
using Godot;
using San14.Core;
using San14.Data;

/// <summary>Source-proven ordinary land creation projected from an explicit
/// playable sortie choice. These bytes are not a current native Army receiver.</summary>
public sealed record OriginalPlayableArmySelection(int UnitId, int HexId,
    int ExplicitLandFormationSlot, OriginalArmyFormationWriteResult Creation,
    OriginalArmyTerrainSelection Terrain, OriginalArmyFormationSelection Formation,
    OriginalArmyContextIndependentDescriptors Descriptors)
{
    public bool CurrentNativeArmyBytesVerified => false;
    public bool OriginalInstancePosesVerified => false;
    public bool OriginalDynamicFlagsVerified => false;
}

public sealed record OriginalPlayableArmyVisualFailure(int UnitId, string Reason);

/// <summary>The provider selects individual components and supplies their exact
/// caller-owned poses. Resource request counts do not generate instances.</summary>
public delegate IReadOnlyList<OriginalArmyComponentVisualBinding>? OriginalPlayableArmyPoseProvider(
    UnitState unit, OriginalPlayableArmySelection sourceSelection);

public sealed record OriginalPlayableArmyVisualPlan(
    IReadOnlyDictionary<int, OriginalPlayableArmySelection> Selections,
    IReadOnlyDictionary<int, IReadOnlyList<OriginalArmyComponentVisualBinding>> Bindings,
    IReadOnlyList<OriginalPlayableArmyVisualFailure> Unresolved)
{
    // A source component may use an explicit marker pose while native instance
    // layout, world scale and current animation remain unavailable.
    public bool PreserveUnitMarkers => true;
    public bool OriginalCompleteAssemblyVerified => false;
}

/// <summary>Projects the verified ordinary land creation chain for source-bound
/// playable units, then delegates individual poses to the caller. Owns no meshes,
/// simulation state or resource cache; OriginalArmyVisualLayer owns the display
/// lifecycle and reloads asset overrides when that layer is recreated.</summary>
public sealed class OriginalPlayableArmyVisualAdapter
{
    private readonly LoadedMap _map;
    private readonly LoadedTerrainTypes _terrainTypes;
    private readonly LoadedSkillDefinitions _definitions;
    private readonly OriginalArmyFormationWriterSource _writer;

    public OriginalPlayableArmyVisualAdapter(LoadedMap map, LoadedTerrainTypes terrainTypes,
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
        _ = OriginalArmyNativeFormationQueries.Terrain(map, terrainTypes, 0);
        _writer = OriginalArmyFormationWriter.ReadSource(definitions, language);
        if (definitions.Source.ResourceUri != GameResourceId.File($"0010_{language}/fixdataexce.s14").ToString() ||
            terrainTypes.Source.ResourceUri != definitions.Source.ResourceUri ||
            !SameHash(terrainTypes.Source.Sha256, definitions.Source.Sha256) ||
            !SameHash(terrainTypes.Catalog.DecodedSha256, definitions.Formations.DecodedSha256))
            throw new InvalidDataException("Playable Army terrain and formation definitions require the same fixed-data source.");
        (_map, _terrainTypes, _definitions) = (map, terrainTypes, definitions);
    }

    public static OriginalPlayableArmyVisualAdapter Load(OriginalGameRepository repository,
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
    public OriginalPlayableArmyVisualPlan Resolve(GameState world,
        OriginalPlayableArmyPoseProvider? poseProvider = null)
    {
        ValidateWorld(world);
        var selections = new Dictionary<int, OriginalPlayableArmySelection>();
        var bindings = new Dictionary<int, IReadOnlyList<OriginalArmyComponentVisualBinding>>();
        var unresolved = new List<OriginalPlayableArmyVisualFailure>();
        foreach (UnitState unit in world.Units.Values.OrderBy(item => item.Id))
        {
            if (unit.Mission?.Kind == UnitMissionKind.Transport)
            {
                unresolved.Add(new(unit.Id, "Transport creation uses a separate native path; the source unit marker remains visible."));
                continue;
            }
            if (unit.NativeArmyConfiguration?.Formation1B is not byte landByte)
            {
                unresolved.Add(new(unit.Id, "Current native Army.byte1B is unavailable."));
                continue;
            }
            int slot=landByte<=20?landByte:0;
            if(!world.Formations.TryGetValue(slot,out FormationDefinition? formation)||formation.Id!=slot||!MatchesSourceFormation(formation,slot))
                throw new InvalidDataException($"Playable Army {unit.Id} formation differs from the selected source table.");
            OriginalArmyTerrainSelection terrain = OriginalArmyNativeFormationQueries.Terrain(
                _map, _terrainTypes, checked((ushort)unit.HexId));
            if (terrain.UsesArmyByte1C && unit.NativeArmyConfiguration.Formation1C is null)
            {
                unresolved.Add(new(unit.Id, "Water terrain requires the current native naval byte1C; the unit marker remains visible."));
                continue;
            }
            if (!terrain.UsesArmyByte1C && _writer.NativeFields7E[slot] == 2)
            {
                unresolved.Add(new(unit.Id, "A naval formation choice does not establish an ordinary land sortie; the unit marker remains visible."));
                continue;
            }
            var creation=new OriginalArmyFormationWriteResult(new(landByte,unit.NativeArmyConfiguration.Formation1C),
                true,false,_writer.Source,_writer.DecodedSha256,_writer.CodeSource);
            OriginalArmyFormationSelection selected = OriginalArmyNativeFormationQueries.Formation(
                terrain, _definitions, creation.Bytes.Byte1B, creation.Bytes.Byte1C);
            var descriptors = OriginalArmyModelSelection.DescribeContextIndependent(
                terrain, creation.Bytes.Byte1B, creation.Bytes.Byte1C);
            var selection = new OriginalPlayableArmySelection(unit.Id, unit.HexId, slot, creation,
                terrain, selected, descriptors);
            selections.Add(unit.Id, selection);
            IReadOnlyList<OriginalArmyComponentVisualBinding>? components = poseProvider?.Invoke(unit, selection);
            if (components is null || components.Count == 0)
            {
                unresolved.Add(new(unit.Id, "Source land descriptors are known; individual world pose and scale are unavailable, so the unit marker remains visible."));
                continue;
            }
            // Snapshot the provider result: later list mutation cannot alter the
            // validated plan, descriptor identities or resource selection.
            OriginalArmyComponentVisualBinding[] snapshot = components.ToArray();
            ValidateComponents(selection, snapshot);
            bindings.Add(unit.Id, Array.AsReadOnly(snapshot));
        }
        return new(new ReadOnlyDictionary<int, OriginalPlayableArmySelection>(selections),
            new ReadOnlyDictionary<int, IReadOnlyList<OriginalArmyComponentVisualBinding>>(bindings),
            unresolved.AsReadOnly());
    }

    /// <summary>Explicit opt-in display policy: one source geometry at a caller
    /// marker pose, retaining the existing sign. This does not reconstruct a
    /// formation or assign a native instance identity. Ordinary infantry category0
    /// and siege category4..6 are the only representatives selected here.</summary>
    public static OriginalPlayableArmyPoseProvider CreateRepresentativeMarkerProvider(
        Quaternion rotation, Vector3 scale, Vector3 offset, string callerPoseSource)
    {
        ValidatePose(rotation, scale, offset, callerPoseSource);
        return (_, selection) =>
        {
            OriginalArmyDescriptorRequest? descriptor = selection.Descriptors.Descriptors.FirstOrDefault(
                item => !item.Cleared && item.Category is 0 or 4 or 5 or 6);
            if (descriptor?.Category is not int category) return [];
            return [new OriginalArmyComponentVisualBinding("source-representative-marker",
                new(category, rotation, scale,
                    $"ordinary-created-land-source-descriptor/{descriptor.DescriptorOffset:X}; caller-marker-pose: {callerPoseSource}"), offset)
                { SourceDescriptorOffset = descriptor.DescriptorOffset }];
        };
    }

    /// <summary>Reconcile the complete source component set through the existing
    /// owner. Call Resolve again after state changes; a stale plan is rejected
    /// before any live model is changed.</summary>
    public IReadOnlyList<OriginalArmyVisualStatus> Apply(OriginalArmyVisualLayer layer,
        MapWorldProjection projection, OriginalTerrainMeshResult terrain, GameState world,
        OriginalPlayableArmyVisualPlan plan)
    {
        ArgumentNullException.ThrowIfNull(layer);
        ArgumentNullException.ThrowIfNull(plan);
        ValidateWorld(world);
        OriginalPlayableArmyVisualPlan current = Resolve(world);
        if (plan.Selections.Count != current.Selections.Count ||
            plan.Selections.Keys.Any(id => !current.Selections.ContainsKey(id)) ||
            plan.Bindings.Keys.Any(id => !plan.Selections.ContainsKey(id)))
            throw new InvalidDataException("Playable Army visual plan references an absent or unresolved unit.");
        foreach ((int id, OriginalPlayableArmySelection selection) in plan.Selections)
        {
            UnitState unit = world.Units[id];
            OriginalPlayableArmySelection fresh = current.Selections[id];
            if (selection.UnitId != id || selection.HexId != unit.HexId ||
                selection.ExplicitLandFormationSlot != fresh.ExplicitLandFormationSlot ||
                selection.Creation != fresh.Creation || selection.Terrain != fresh.Terrain ||
                selection.Formation != fresh.Formation ||
                !selection.Descriptors.PossibleModes.SequenceEqual(fresh.Descriptors.PossibleModes) ||
                !selection.Descriptors.Descriptors.SequenceEqual(fresh.Descriptors.Descriptors) ||
                !ReferenceEquals(selection.Creation.FormationSource, _definitions.Source) ||
                !ReferenceEquals(selection.Terrain.MapSource, _map.Source) ||
                !ReferenceEquals(selection.Terrain.TerrainSource, _terrainTypes.Source) ||
                !ReferenceEquals(selection.Formation.FormationSource, _definitions.Source))
                throw new InvalidDataException("Playable Army visual plan is stale or belongs to another source adapter.");
        }
        foreach ((int id, IReadOnlyList<OriginalArmyComponentVisualBinding> components) in plan.Bindings)
            ValidateComponents(plan.Selections[id], components);
        IReadOnlyList<OriginalArmyVisualStatus> statuses = layer.UpdateComponents(projection, terrain, world, plan.Bindings);
        foreach ((int id, IReadOnlyList<OriginalArmyComponentVisualBinding> components) in plan.Bindings)
            foreach (OriginalArmyComponentVisualBinding component in components)
            {
                MeshInstance3D? model = layer.FindComponentModel(id, component.ComponentId);
                if (model is null) continue;
                OriginalPlayableArmySelection selection = plan.Selections[id];
                OriginalArmyDescriptorRequest request = selection.Descriptors.Descriptors.Single(
                    item => item.DescriptorOffset == component.SourceDescriptorOffset);
                model.SetMeta("source_ordinary_land_creation_projection", true);
                model.SetMeta("source_selected_land_slot", selection.ExplicitLandFormationSlot);
                model.SetMeta("source_created_army_byte1b", (int)selection.Creation.Bytes.Byte1B!.Value);
                model.SetMeta("source_formation_uri", _definitions.Source.ResourceUri);
                model.SetMeta("source_formation_sha256", _definitions.Source.Sha256);
                model.SetMeta("source_formation_decoded_sha256", _writer.DecodedSha256);
                model.SetMeta("source_formation_is_override", _definitions.Source.IsOverride);
                model.SetMeta("source_selection_rule_version", _writer.CodeSource.RuleVersion);
                model.SetMeta("source_selection_code_language", _writer.CodeSource.Language);
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
        if (world.Units.Any(item => item.Value is null || item.Key != item.Value.Id || item.Key < 0 ||
            (uint)item.Value.HexId >= (uint)_map.Map.Cells.Length))
            throw new InvalidDataException("Playable Army state has a mismatched unit identity or invalid source Hex.");
    }

    private bool MatchesSourceFormation(FormationDefinition formation, int slot)
    {
        byte[] raw = _definitions.Formations.Formations[slot].RawParameterBytes;
        return formation.SourceBase == new FormationBaseCoefficients(raw[1], raw[5], raw[6], raw[7], raw[8]);
    }

    private static void ValidateComponents(OriginalPlayableArmySelection selection,
        IReadOnlyList<OriginalArmyComponentVisualBinding> components)
    {
        if (components is null) throw new InvalidDataException("Playable Army component list is unknown.");
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (OriginalArmyComponentVisualBinding component in components)
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
