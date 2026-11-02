using San14.Core;

namespace San14.Data;

public sealed partial class SourceWorldContext
{
    private readonly Dictionary<int, ushort?> _currentObjectPositions = [];
    private readonly Dictionary<int, byte?> _currentObjectKinds = [];
    private readonly Dictionary<int, ushort?> _currentObjectFields16 = [];
    internal ushort? ReadMapObjectFields16(ObjectReference receiver) =>
        _currentObjectFields16.GetValueOrDefault(MapPhysical(receiver.PhysicalSlot, 3001, "object fields16"));
    internal void WriteMapObjectFields16(ObjectReference receiver, ushort value) =>
        _currentObjectFields16[MapPhysical(receiver.PhysicalSlot, 3001, "object fields16")] = value;
    internal MapObjectSourceDirectory? CurrentMapObjectDirectory { get; private set; }
    private void CopyCurrentMapObjectKnowledgeTo(SourceWorldContext other)
    {
        other.CurrentMapObjectDirectory = CurrentMapObjectDirectory;
        other._currentObjectPositions.Clear(); other._currentObjectKinds.Clear();
        other._currentObjectFields16.Clear();
        foreach (var value in _currentObjectFields16) other._currentObjectFields16.Add(value.Key, value.Value);
        foreach (var value in _currentObjectPositions) other._currentObjectPositions.Add(value.Key, value.Value);
        foreach (var value in _currentObjectKinds) other._currentObjectKinds.Add(value.Key, value.Value);
    }

    /// <summary>Replace the entire opening object prefix, including empty receivers.
    /// Missing saved knowledge stays unknown and cannot revive scenario objects.</summary>
    internal void BindCurrentMapObjects(GameState state)
    {
        _objects.Clear(); _objectForceSlots13.Clear(); _objectRawFields14.Clear();
        _currentObjectPositions.Clear(); _currentObjectKinds.Clear(); _hexObjectLinks.Clear();
        _currentObjectFields16.Clear();
        _emptyHexObjectLinksKnown = false;
        var source = CampaignSourceKnowledge.From(state.ResourceState).MapObjectSource;
        CurrentMapObjectDirectory = null;
        if (state.MapObjects is not { } owner || source is null) return;
        var directory = source.Directory;
        CurrentMapObjectDirectory = directory;
        foreach (var field in source.CurrentFields16) _currentObjectFields16[field.Key] = field.Value;
        var rows = owner.Receivers.ToDictionary(x => x.Id);
        for (int ordinal = 0; ordinal < source.LookupPhysicalSlots.Count; ordinal++)
            SetObjectTablePointer(ordinal, source.LookupPhysicalSlots[ordinal]);
        for (int physical = 0; physical < source.PhysicalReceivers.Count; physical++)
        {
            var row = rows[source.PhysicalReceivers[physical]];
            ushort? hex = row.Pose?.Kind switch
            { MapObjectPoseKind.Hex => checked((ushort)row.Pose.HexId!.Value), MapObjectPoseKind.None => 48400,
                MapObjectPoseKind.Unresolved => source.UnmappedCoordinates.TryGetValue(physical, out ushort coordinate) ? coordinate : null, _ => null };
            byte? kind; byte? faction; ushort? durability;
            if (row.Subject is not { } subject)
            {
                source.UnplacedFields.TryGetValue(physical, out var fields);
                kind = fields?.Kind ?? 0; faction = fields?.Faction ?? 0;
                int? baseDurability = state.Cities.TryGetValue(physical, out var city) ? city.Durability
                    : state.Gates.TryGetValue(physical, out var gate) ? gate.Durability : null;
                durability = state.Cities.ContainsKey(physical) || state.Gates.ContainsKey(physical)
                    ? baseDurability is >= 0 and <= ushort.MaxValue ? (ushort)baseDurability.Value : null
                    : fields is null ? (ushort)0 : fields.Durability;
            }
            else
            {
                kind = source.UnmappedKinds.TryGetValue(physical, out byte raw) ? raw
                    : checked((byte)MapObjectSourceDirectory.DefinitionSlot(subject.DefinitionKey));
                if (subject.Category is MapObjectCategory.City or MapObjectCategory.Gate)
                {
                    faction = row.BaseMapFaction?.Kind switch
                    { MapObjectFactionKind.Unaffiliated => 0, MapObjectFactionKind.Force => checked((byte)row.BaseMapFaction.ForceId!.Value),
                        MapObjectFactionKind.Unresolved => source.UnmappedFactions.TryGetValue(physical, out byte factionCode) ? factionCode : null, _ => null };
                    int? value = subject.Category == MapObjectCategory.City ? state.Cities[subject.LocationId!.Value].Durability
                        : state.Gates[subject.LocationId!.Value].Durability;
                    durability = value is >= 0 and <= ushort.MaxValue ? (ushort)value.Value : null;
                }
                else
                {
                    var entity = state.Structures![subject.StructureId!.Value];
                    faction = source.UnmappedFactions.TryGetValue(physical, out byte rawFaction) ? rawFaction
                        : checked((byte)(entity.ForceId ?? 0));
                    durability = entity.Durability is >= 0 and <= ushort.MaxValue ? (ushort)entity.Durability : null;
                }
            }
            _currentObjectPositions[physical] = hex; _currentObjectKinds[physical] = kind;
            if (hex is ushort position && kind is byte definition) SetObjectRecord(new(physical, position, definition));
            SetObjectForceSlot13(physical, faction); SetObjectRawField14(physical, durability);
        }
        if (owner.HexBindings is { } bindings)
        {
            _emptyHexObjectLinksKnown = true;
            foreach (var (hex, receiver) in bindings.Occupied)
                SetHexObjectLink(hex, directory.EncodeHexBinding(hex, receiver));
            foreach (int hex in source.HexObjectLookupAliases.Keys)
                if (!bindings.Occupied.ContainsKey(hex)) SetHexObjectLink(hex, directory.EncodeHexBinding(hex, null));
            // The usual known empty default is raw0. If0 resolves to another
            // receiver, an empty cell without an observed alias is unknown.
            if (directory.Lookup(0) is { } zero && zero != directory.ReceiverAtPhysical(0))
                for (int hex = 0; hex < bindings.TileCount; hex++)
                    if (!bindings.Occupied.ContainsKey(hex) && !source.HexObjectLookupAliases.ContainsKey(hex))
                        SetHexObjectLink(hex, null);
        }
    }
}
