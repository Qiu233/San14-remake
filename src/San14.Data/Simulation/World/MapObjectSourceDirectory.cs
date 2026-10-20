using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using San14.Core;

namespace San14.Data;

/// <summary>Only current values without a world entity projection. In
/// particular an unplaced receiver still owns the goal read without validity.</summary>
public sealed record UnplacedMapObjectSourceFields(byte Kind, byte Faction, ushort? Durability);

/// <summary>Source-table identity and otherwise unmapped current knowledge.
/// No native pointers, instruction bytes or mutable packet image are stored.</summary>
public sealed class MapObjectSourceState
{
    public IReadOnlyList<MapObjectReceiverId> PhysicalReceivers { get; }
    public IReadOnlyList<int?> LookupPhysicalSlots { get; }
    public IReadOnlyDictionary<int, ushort> UnmappedCoordinates { get; }
    public IReadOnlyDictionary<int, byte> UnmappedFactions { get; }
    public IReadOnlyDictionary<int, byte> UnmappedKinds { get; }
    public IReadOnlyDictionary<int, UnplacedMapObjectSourceFields> UnplacedFields { get; }
    public IReadOnlyDictionary<int, ushort> CurrentFields16 { get; }
    public IReadOnlyDictionary<int, ushort> HexObjectLookupAliases { get; }
    [JsonConstructor]
    public MapObjectSourceState(IReadOnlyList<MapObjectReceiverId> physicalReceivers,
        IReadOnlyList<int?> lookupPhysicalSlots, IReadOnlyDictionary<int, ushort> unmappedCoordinates,
        IReadOnlyDictionary<int, byte> unmappedFactions, IReadOnlyDictionary<int, byte> unmappedKinds,
        IReadOnlyDictionary<int, UnplacedMapObjectSourceFields> unplacedFields,
        IReadOnlyDictionary<int, ushort> currentFields16,
        IReadOnlyDictionary<int, ushort>? hexObjectLookupAliases = null)
    {
        PhysicalReceivers = Array.AsReadOnly(physicalReceivers.ToArray());
        LookupPhysicalSlots = Array.AsReadOnly(lookupPhysicalSlots.ToArray());
        UnmappedCoordinates = new ReadOnlyDictionary<int, ushort>(new Dictionary<int, ushort>(unmappedCoordinates));
        UnmappedFactions = new ReadOnlyDictionary<int, byte>(new Dictionary<int, byte>(unmappedFactions));
        UnmappedKinds = new ReadOnlyDictionary<int, byte>(new Dictionary<int, byte>(unmappedKinds));
        UnplacedFields = new ReadOnlyDictionary<int, UnplacedMapObjectSourceFields>(new Dictionary<int, UnplacedMapObjectSourceFields>(unplacedFields));
        CurrentFields16 = new ReadOnlyDictionary<int, ushort>(new Dictionary<int, ushort>(currentFields16));
        HexObjectLookupAliases = new ReadOnlyDictionary<int, ushort>(new Dictionary<int, ushort>(hexObjectLookupAliases ?? new Dictionary<int, ushort>()));
        if (HexObjectLookupAliases.Keys.Any(hex => hex is < 0 or >= 48400))
            throw new InvalidDataException("Map object Hex lookup alias has an invalid cell.");
        if (PhysicalReceivers.Count != 3001 || LookupPhysicalSlots.Count != 3001 ||
            PhysicalReceivers.Any(x => x.Value <= 0) || PhysicalReceivers.Distinct().Count() != 3001 ||
            LookupPhysicalSlots.Any(x => x is < 0 or > 3000) ||
            UnplacedFields.Any(x => x.Value is null) || UnmappedCoordinates.Any(x => x.Value <= 48400) ||
            UnmappedCoordinates.Keys.Concat(UnmappedFactions.Keys).Concat(UnmappedKinds.Keys)
                .Concat(UnplacedFields.Keys).Concat(CurrentFields16.Keys).Any(x => x is < 0 or > 3000))
            throw new InvalidDataException("Map object source directory is outside the inspected resource profile.");
    }
    /// <summary>Current compatibility fact only. Missing means unknown;
    /// immutable replacement preserves the independent17 byte and all aliases.</summary>
    internal MapObjectSourceState WithCurrentFields16(int physical, ushort? value)
    {
        if ((uint)physical >= PhysicalReceivers.Count)
            throw new InvalidDataException("Unknown object fields16 receiver.");
        var fields = new Dictionary<int, ushort>(CurrentFields16);
        if (value is ushort known) fields[physical] = known; else fields.Remove(physical);
        return new(PhysicalReceivers, LookupPhysicalSlots, UnmappedCoordinates, UnmappedFactions,
            UnmappedKinds, UnplacedFields, fields, HexObjectLookupAliases);
    }
    internal MapObjectSourceState Reset(int physical)
    {
        var coordinates = new Dictionary<int, ushort>(UnmappedCoordinates); coordinates.Remove(physical);
        var factions = new Dictionary<int, byte>(UnmappedFactions); factions.Remove(physical);
        var kinds = new Dictionary<int, byte>(UnmappedKinds); kinds.Remove(physical);
        var unplaced = new Dictionary<int, UnplacedMapObjectSourceFields>(UnplacedFields); unplaced.Remove(physical);
        // The16 word's meaning is unproved. Its actual reset is nevertheless
        // retained at this compatibility boundary rather than dropped.
        var tails = new Dictionary<int, ushort>(CurrentFields16) { [physical] = 0 };
        return new(PhysicalReceivers, LookupPhysicalSlots, coordinates, factions, kinds, unplaced, tails, HexObjectLookupAliases);
    }
    internal MapObjectSourceState WithConstructedReceiver(int physical, ushort fields16, byte faction, bool knownFaction)
    {
        var coordinates = new Dictionary<int, ushort>(UnmappedCoordinates); coordinates.Remove(physical);
        var factions = new Dictionary<int, byte>(UnmappedFactions); factions.Remove(physical);
        if (!knownFaction) factions[physical] = faction;
        var kinds = new Dictionary<int, byte>(UnmappedKinds); kinds.Remove(physical);
        var unplaced = new Dictionary<int, UnplacedMapObjectSourceFields>(UnplacedFields); unplaced.Remove(physical);
        var tails = new Dictionary<int, ushort>(CurrentFields16) { [physical] = fields16 };
        return new(PhysicalReceivers, LookupPhysicalSlots, coordinates, factions, kinds, unplaced, tails, HexObjectLookupAliases);
    }
    internal MapObjectSourceState WithHexLookupAlias(int hex, ushort? value)
    {
        var aliases = new Dictionary<int, ushort>(HexObjectLookupAliases);
        if (value is ushort raw) aliases[hex] = raw; else aliases.Remove(hex);
        return new(PhysicalReceivers, LookupPhysicalSlots, UnmappedCoordinates, UnmappedFactions,
            UnmappedKinds, UnplacedFields, CurrentFields16, aliases);
    }
    internal MapObjectSourceState WithFaction(int physical, byte? value)
    {
        var factions = new Dictionary<int, byte>(UnmappedFactions);
        if (value is byte raw) factions[physical] = raw; else factions.Remove(physical);
        return new(PhysicalReceivers, LookupPhysicalSlots, UnmappedCoordinates, factions, UnmappedKinds, UnplacedFields, CurrentFields16, HexObjectLookupAliases);
    }
}

public sealed class MapObjectSourceDirectory
{
    private readonly MapObjectSourceState state;
    private readonly Dictionary<MapObjectReceiverId, int> physical;
    public MapObjectSourceDirectory(MapObjectSourceState state)
    { this.state = state; physical = state.PhysicalReceivers.Select((id, slot) => (id, slot)).ToDictionary(x => x.id, x => x.slot); }
    public MapObjectReceiverId ReceiverAtPhysical(int value) => (uint)value < state.PhysicalReceivers.Count
        ? state.PhysicalReceivers[value] : throw new InvalidDataException("Unknown object physical receiver.");
    public int PhysicalOf(MapObjectReceiverId value) => physical.TryGetValue(value, out int slot) ? slot
        : throw new InvalidDataException("Unknown map object campaign receiver.");
    public MapObjectReceiverId? Lookup(int value) => state.LookupPhysicalSlots[value <= 3000 && value >= 0 ? value : 0]
        is int slot ? ReceiverAtPhysical(slot) : null;
    // Hex1A stores the lookup key, not a physical receiver ordinal. Preserve
    // an observed alias exactly; without one only the same physical key is safe.
    internal ushort? EncodeHexBinding(int hex, MapObjectReceiverId? receiver)
    {
        ushort candidate = state.HexObjectLookupAliases.TryGetValue(hex, out ushort saved)
            ? saved : checked((ushort)(receiver is { } id ? PhysicalOf(id) : 0));
        MapObjectReceiverId? actual = Lookup(candidate);
        if (actual == ReceiverAtPhysical(0)) actual = null;
        return actual == receiver ? candidate : null;
    }
    internal static string DefinitionKey(int slot) => "san14-object-definition:" + slot;
    internal static int DefinitionSlot(string key) => key.StartsWith("san14-object-definition:", StringComparison.Ordinal) &&
        int.TryParse(key.AsSpan("san14-object-definition:".Length), out int value) && value is >= 0 and <= 30 ? value
        : throw new InvalidDataException("Map object has an unknown resource definition key.");
}
