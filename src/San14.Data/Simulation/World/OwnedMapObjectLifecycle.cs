using San14.Core;

namespace San14.Data;

/// <summary>The explicit new-campaign allocation and current object projection.
/// Restoring a campaign never calls the initial resource projection.</summary>
internal static class OwnedMapObjectLifecycle
{
    internal static GameState InstallFresh(GameState state, LoadedScenario scenario,
        LoadedObjectDefinitions definitions, SourceWorldContext initial)
    {
        if (state.MapObjects is not null || CampaignSourceKnowledge.From(state.ResourceState).MapObjectSource is not null ||
            state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } || state.Units.Count != 0 ||
            scenario.Snapshot.ObjectSourceRecords?.Objects is not { Count: 3001 } records ||
            definitions.Catalog.Definitions.Count != 31)
            throw Missing("fresh_allocation");
        var identities = Enumerable.Range(1, 3001).Select(id => new MapObjectReceiverId(id)).ToArray();
        var lookups = Enumerable.Range(0, 3001).Select(slot =>
            (initial.ReadObjectTableReference(slot) ?? throw Missing("fresh_table_pointer")).PhysicalSlot).ToArray();
        var coordinates = new Dictionary<int, ushort>(); var factions = new Dictionary<int, byte>();
        var kinds = new Dictionary<int, byte>(); var unplaced = new Dictionary<int, UnplacedMapObjectSourceFields>();
        var structures = new Dictionary<int, StructureState>(state.Structures ?? []);
        int nextStructure = structures.Count == 0 ? 1 : checked(structures.Keys.Max() + 1);
        var receivers = new MapObjectReceiverState[3001];
        for (int physical = 0; physical < records.Count; physical++)
        {
            var source = records[physical];
            if (source.PhysicalSlot != physical) throw Missing("fresh_record_order");
            int rawKind = source.RawField12, definition = rawKind <= 30 ? rawKind : 0;
            var pose = source.RawField10 < 48400 ? new MapObjectPose(MapObjectPoseKind.Hex, source.RawField10)
                : source.RawField10 == 48400 ? new(MapObjectPoseKind.None) : new(MapObjectPoseKind.Unresolved);
            if (source.RawField10 > 48400) coordinates.Add(physical, source.RawField10);
            if (rawKind > 30) kinds.Add(physical, source.RawField12);
            int faction = source.RawField13;
            bool knownFaction = faction == 0 || state.Forces.ContainsKey(faction);
            if (!knownFaction) factions.Add(physical, source.RawField13);
            MapObjectSubject? subject = null; MapObjectBaseFaction? allegiance = null;
            if (rawKind == 1 && state.Cities.ContainsKey(physical) || rawKind == 2 && state.Gates.ContainsKey(physical))
            {
                var category = rawKind == 1 ? MapObjectCategory.City : MapObjectCategory.Gate;
                subject = new(category, MapObjectSourceDirectory.DefinitionKey(definition), physical, null);
                allegiance = faction == 0 ? new(MapObjectFactionKind.Unaffiliated)
                    : knownFaction ? new(MapObjectFactionKind.Force, faction) : new(MapObjectFactionKind.Unresolved);
                int? durability = rawKind == 1 ? state.Cities[physical].Durability : state.Gates[physical].Durability;
                if (durability != source.RawField14) throw Missing("fresh_base_durability_projection");
            }
            else if (rawKind != 0 && source.RawField10 < 48400 && physical != 0)
            {
                var category = Category(definitions.Catalog.Definitions[definition].RawField2C);
                if (category is MapObjectCategory.City or MapObjectCategory.Gate) throw Missing("base_receiver_identity");
                int id = nextStructure++;
                structures.Add(id, new(id, MapObjectSourceDirectory.DefinitionKey(definition), source.RawField10,
                    knownFaction && faction != 0 ? faction : null, source.RawField14, MaxDurability: null));
                subject = new(category, MapObjectSourceDirectory.DefinitionKey(definition), null, id);
            }
            else
            {
                int? baseDurability = state.Cities.TryGetValue(physical, out var city) ? city.Durability
                    : state.Gates.TryGetValue(physical, out var gate) ? gate.Durability : null;
                bool hasBaseOwner = state.Cities.ContainsKey(physical) || state.Gates.ContainsKey(physical);
                if (hasBaseOwner && baseDurability != source.RawField14) throw Missing("fresh_empty_base_durability_projection");
                unplaced.Add(physical, new(source.RawField12, source.RawField13, hasBaseOwner ? null : source.RawField14));
            }
            receivers[physical] = new(identities[physical], subject, pose, allegiance);
        }
        var links = new Dictionary<int, MapObjectReceiverId>();
        var aliases = new Dictionary<int, ushort>();
        for (int hex = 0; hex < 48400; hex++)
        {
            ushort ordinal = initial.ReadHexObjectLink(new(hex)) ?? throw Missing("fresh_hex_binding");
            int? physical = lookups[ordinal <= 3000 ? ordinal : 0];
            if (physical is int at && at != 0) links.Add(hex, identities[at]);
            if (physical is null || ordinal != physical.Value) aliases.Add(hex, ordinal);
        }
        // CObject ctor2216A0 clears both16/17. Static239040 omits them;
        // install only for a genuinely new allocation, never on restore.
        var constructorFields = Enumerable.Range(0, 3001).ToDictionary(x => x, _ => (ushort)0);
        var sourceState = new MapObjectSourceState(identities, lookups, coordinates, factions, kinds, unplaced, constructorFields, aliases);
        return state with { MapObjects = new(receivers, new(48400, links)), Structures = structures,
            ResourceState = CampaignSourceKnowledge.From(state.ResourceState).WithMapObjectSource(sourceState) };
    }

    /// <summary>Apply the actual Hex1A word writer and its resolved receiver
    /// together. Raw0 stays0 even if lookup0 resolves elsewhere or is null.</summary>
    internal static GameState WriteHexBinding(GameState state, SourceWorldContext current, int hex, ushort raw)
    {
        var source = CampaignSourceKnowledge.From(state.ResourceState).MapObjectSource ?? throw Missing("hex_source");
        var directory = new MapObjectSourceDirectory(source);
        var owner = state.MapObjects ?? throw Missing("hex_owner");
        var bindings = owner.HexBindings ?? throw Missing("hex_bindings");
        if ((uint)hex >= bindings.TileCount) throw Missing("hex_index");
        var occupied = new Dictionary<int, MapObjectReceiverId>(bindings.Occupied);
        occupied.Remove(hex);
        MapObjectReceiverId? resolved = directory.Lookup(raw);
        if (resolved is { } id && id != directory.ReceiverAtPhysical(0)) occupied[hex] = id;
        source = source.WithHexLookupAlias(hex, resolved is null || raw != directory.PhysicalOf(resolved.Value) ? raw : null);
        current.WriteHexObjectLink(new(hex), raw);
        return state with { MapObjects = owner with { HexBindings = bindings with { Occupied = occupied } },
            ResourceState = CampaignSourceKnowledge.From(state.ResourceState).WithMapObjectSource(source) };
    }

    internal static void BindCurrent(GameState state, SourceWorldContext current) => current.BindCurrentMapObjects(state);

    /// <summary>Publish current map-faction writes separately from Base B0.
    /// Durability remains in the existing Base/Structure owner.</summary>
    internal static GameState PublishCurrent(GameState state, SourceWorldContext current)
    {
        var source = CampaignSourceKnowledge.From(state.ResourceState).MapObjectSource;
        if (source is null || state.MapObjects is not { } owner) throw Missing("publication_owner");
        var directory = new MapObjectSourceDirectory(source);
        var structures = new Dictionary<int, StructureState>(state.Structures ?? []);
        var rows = owner.Receivers.ToArray();
        for (int index = 0; index < rows.Length; index++)
        {
            var receiver = rows[index];
            if (receiver.Subject is not { } subject) continue;
            int physical = directory.PhysicalOf(receiver.Id);
            byte? faction = current.ReadObjectForceSlot13(new(physical));
            bool unresolved = faction is byte value && value != 0 && !state.Forces.ContainsKey(value);
            source = source.WithFaction(physical, unresolved ? faction : null);
            if (subject.Category is MapObjectCategory.City or MapObjectCategory.Gate)
                rows[index] = receiver with { BaseMapFaction = faction is null ? null
                    : faction == 0 ? new(MapObjectFactionKind.Unaffiliated)
                    : unresolved ? new(MapObjectFactionKind.Unresolved) : new(MapObjectFactionKind.Force, faction) };
            else
            {
                if (faction is null || current.ReadObjectRawField14(new(physical)) is not ushort durability)
                    throw Missing("structure_publication");
                var entity = structures[subject.StructureId!.Value];
                structures[entity.Id] = entity with { ForceId = faction == 0 || unresolved ? null : faction,
                    Durability = durability };
            }
        }
        return state with { MapObjects = new(rows, owner.HexBindings), Structures = structures,
            ResourceState = CampaignSourceKnowledge.From(state.ResourceState).WithMapObjectSource(source) };
    }

    internal static void Validate(GameState state)
    {
        var source = CampaignSourceKnowledge.From(state.ResourceState).MapObjectSource;
        if (source is null)
        {
            if (state.MapObjects is not null) throw Missing("missing_source_directory");
            return;
        }
        if (state.MapObjects is not { } owner || source.PhysicalReceivers.Count != owner.Receivers.Count ||
            source.PhysicalReceivers.Except(owner.Receivers.Select(x => x.Id)).Any()) throw Missing("source_receiver_identity");
        var directory = new MapObjectSourceDirectory(source);
        if (owner.HexBindings is { } bindings)
            foreach (var hex in source.HexObjectLookupAliases.Keys)
            {
                MapObjectReceiverId? receiver = bindings.Occupied.TryGetValue(hex, out var bound) ? bound : null;
                if (directory.EncodeHexBinding(hex, receiver) is null) throw Missing("hex_lookup_alias_projection");
            }
        foreach (var receiver in owner.Receivers)
        {
            int physical = directory.PhysicalOf(receiver.Id);
            if (receiver.Pose?.Kind == MapObjectPoseKind.Unresolved != source.UnmappedCoordinates.ContainsKey(physical))
                throw Missing("unmapped_pose");
            if (receiver.Subject is { } subject)
            {
                _ = MapObjectSourceDirectory.DefinitionSlot(subject.DefinitionKey);
                if (source.UnplacedFields.ContainsKey(physical)) throw Missing("duplicate_unplaced_header");
                if (subject.StructureId is int structure && receiver.Pose is { Kind: MapObjectPoseKind.Hex, HexId: int hex } &&
                    state.Structures![structure].HexId != hex) throw Missing("structure_pose_projection");
                if (subject.Category is MapObjectCategory.City or MapObjectCategory.Gate &&
                    receiver.BaseMapFaction?.Kind == MapObjectFactionKind.Unresolved != source.UnmappedFactions.ContainsKey(physical))
                    throw Missing("unmapped_base_faction");
            }
            else if (source.UnplacedFields.TryGetValue(physical, out var fields) &&
                (state.Cities.ContainsKey(physical) || state.Gates.ContainsKey(physical)) && fields.Durability is not null)
                throw Missing("duplicate_empty_base_durability");
        }
    }

    internal static MapObjectCategory Category(byte category) => category switch
    { 1 => MapObjectCategory.City, 2 => MapObjectCategory.Facility, 3 => MapObjectCategory.Obstacle,
        4 => MapObjectCategory.Trap, _ => MapObjectCategory.Unclassified };
    private static InvalidDataException Missing(string field) => new("map_objects." + field);
}
