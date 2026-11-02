using System.Collections.ObjectModel;
using System.Collections.Frozen;
using San14.Core;

namespace San14.Data;

/// <summary>A registered Army may still be inside its current Home Base.
/// That Army owns real resources and commands without a field-map marker.</summary>
public sealed record ArmyMapPresenceRow(int UnitId, int? ArmyPhysicalSlot,
    bool? ArmyValid, bool? IsExcluded, bool? IsAtHome, int? HomeLocationId, int? Troops)
{
    public bool? IsOnMap => ArmyValid == false || IsExcluded == true || IsAtHome == true
        ? false : ArmyValid == true && IsExcluded == false && IsAtHome == false ? true : null;
}

public sealed record ArmyMapPresenceUnavailable(int UnitId, string Field);
public sealed record BaseLabelArmyTroopsUnavailable(int? ArmyPhysicalSlot,
    int? LocationId, string Field);

/// <summary>Immutable values for one committed World. Base keys are the real
/// City/Gate location IDs; troops are already debited from that Base stock.</summary>
public sealed record ArmyMapPresenceFrame(IReadOnlyDictionary<int, ArmyMapPresenceRow> Armies,
    IReadOnlySet<int> VisibleUnitIds,
    IReadOnlyDictionary<int, int?> AwaitingDepartureTroopsByLocation,
    IReadOnlyList<ArmyMapPresenceUnavailable> Unavailable)
{
    public string? UnresolvedInput => Unavailable.Count == 0 ? null : Unavailable[0].Field;
    /// <summary>1AD290/6DE160's independent same-Hex native Army list.
    /// A known value, including zero, proves a nonempty list. Null is unknown
    /// list membership or total, never permission to format a zero slash.</summary>
    public IReadOnlyDictionary<int, int?> BaseLabelArmyTroopsByLocation { get; init; }
        = FrozenDictionary<int, int?>.Empty;
    public IReadOnlyList<BaseLabelArmyTroopsUnavailable> BaseLabelUnavailable { get; init; }
        = Array.Empty<BaseLabelArmyTroopsUnavailable>();
    public string? BaseLabelUnresolvedInput => BaseLabelUnavailable.Count == 0
        ? null : BaseLabelUnavailable[0].Field;
}

public sealed partial class ArmyCampaignRuleAdapter
{
    private GameState? _mapPresenceWorld;
    private ArmyMapPresenceFrame? _mapPresenceFrame;

    /// <summary>Capture once after a World publication. Camera and input
    /// requests reuse these values for the same immutable World identity.</summary>
    public ArmyMapPresenceFrame? CaptureMapArmyPresence(GameState state)
    {
        if (ReferenceEquals(state, _mapPresenceWorld)) return _mapPresenceFrame;
        return CaptureMapArmyGroupingCurrent(state) is { } current
            ? CaptureMapArmyPresence(state, current) : null;
    }

    /// <summary>Borrow the existing current for this World; never imports an
    /// Army arena, mutates grouping, debits troops or advances appearance.</summary>
    public ArmyMapPresenceFrame CaptureMapArmyPresence(GameState state, SourceWorldContext current)
    {
        if (ReferenceEquals(state, _mapPresenceWorld) && _mapPresenceFrame is { } cached) return cached;
        var queries = MapArmyGroupingQueries(state, current);
        //Bind this World's scalar Army inputs before the waiting Home getter
        //or the independent Base-label list reads that same current view.
        var baseLabelTroops = CaptureBaseLabelArmyTroops(state, current, queries);
        var armies = new Dictionary<int, ArmyMapPresenceRow>(state.Units.Count);
        var waitingTroops = new Dictionary<int, int?>();
        var visible = new HashSet<int>();
        var unavailable = new List<ArmyMapPresenceUnavailable>();
        bool cannotAttributePotentialWaiting = false;
        foreach (var army in state.Units.Values)
        {
            int? physical = army.NativeArmyOrigin?.NativeSlotIndex;
            var row = new ArmyMapPresenceRow(army.Id, physical, null, null, null, null,
                army.Troops is >= 0 and <= ushort.MaxValue ? army.Troops : null);
            if (physical is not (>= 1 and <= 500))
            {
                cannotAttributePotentialWaiting = true;
                unavailable.Add(new(army.Id, "current.Army_presence_receiver"));
                armies.Add(army.Id, row); continue;
            }
            var receiver = queries.ReadReceiver(physical.Value);
            if (receiver.UnitId != army.Id)
            {
                cannotAttributePotentialWaiting = true;
                unavailable.Add(new(army.Id, "current.Army_presence_identity"));
                armies.Add(army.Id, row); continue;
            }
            row = row with { ArmyValid = receiver.IsValid };
            if (receiver.IsValid == true)
            {
                bool? excluded = queries.IsExcluded233560(physical.Value);
                bool? atHome = excluded == true ? false : excluded == false
                    ? queries.IsAtHome2C96E0(physical.Value) : null;
                row = row with { IsExcluded = excluded, IsAtHome = atHome };
                if (excluded is null || atHome is null) cannotAttributePotentialWaiting = true;
                if (excluded is null) unavailable.Add(new(army.Id, "current.Army_presence_excluded"));
                if (atHome is null) unavailable.Add(new(army.Id, "current.Army_presence_at_home"));
                if (excluded == false && atHome == true)
                {
                    int? location;
                    try { location = current.ReadBaseObjectLink(current.FindArmyUnitCurrentBase(new(physical.Value))); }
                    catch (InvalidDataException) { location = null; }
                    if (location is >= 1 and <= 61)
                    {
                        row = row with { HomeLocationId = location };
                        if (!waitingTroops.ContainsKey(location.Value)) waitingTroops.Add(location.Value, 0);
                        if (row.Troops is int troops)
                            waitingTroops[location.Value] += troops;
                        else
                        {
                            waitingTroops[location.Value] = null;
                            unavailable.Add(new(army.Id, "current.Army_presence_troops"));
                        }
                    }
                    else
                    {
                        cannotAttributePotentialWaiting = true;
                        unavailable.Add(new(army.Id, "current.Army_presence_home"));
                    }
                }
            }
            else if (receiver.IsValid is null)
            {
                cannotAttributePotentialWaiting = true;
                unavailable.Add(new(army.Id, "current.Army_presence_validity"));
            }
            if (row.IsOnMap == true) visible.Add(army.Id);
            armies.Add(army.Id, row);
        }
        //An unattributed potential member can belong to any Base. Do not
        //publish known contributions as complete totals for those Bases.
        if (cannotAttributePotentialWaiting)
            foreach (int location in waitingTroops.Keys) waitingTroops[location] = null;
        _mapPresenceFrame = new(new ReadOnlyDictionary<int, ArmyMapPresenceRow>(armies),
            visible.ToFrozenSet(), new ReadOnlyDictionary<int, int?>(waitingTroops), unavailable.AsReadOnly())
            { BaseLabelArmyTroopsByLocation = baseLabelTroops.Totals,
              BaseLabelUnavailable = baseLabelTroops.Unavailable };
        _mapPresenceWorld = state;
        return _mapPresenceFrame;
    }

    //6DE160 traverses the physical Army table, not active World.Units. A
    //retiring receiver's real PendingImage can still satisfy native validity.
    //One source pass aggregates by raw Hex before the finite Base lookup pass.
    private static (IReadOnlyDictionary<int, int?> Totals,
        IReadOnlyList<BaseLabelArmyTroopsUnavailable> Unavailable) CaptureBaseLabelArmyTroops(
        GameState state, SourceWorldContext current, IArmyMapGroupingQueries queries)
    {
        var byHex = new Dictionary<ushort, int?>();
        var unavailable = new List<BaseLabelArmyTroopsUnavailable>();
        var slots = state.ReadArmyLifecycle()?.ReceiverSlots();
        bool cannotAttributePotentialMember = slots is null;
        if (slots is null)
            unavailable.Add(new(null, null, "current.Base_label_saved_Army_owner"));
        //The native range contains slot0 followed by the500 real receivers.
        for (int lookup = 0; slots is not null && lookup <= 500; lookup++)
        {
            if (lookup >= slots.Length || slots[lookup].PhysicalSlot != lookup)
            {
                cannotAttributePotentialMember = true;
                unavailable.Add(new(null, null, "current.Base_label_saved_Army_receiver." + lookup)); continue;
            }
            var saved = slots[lookup];
            //This is the scalar projection used by RestoreOwnedArmyReturnSlots,
            //without pool/trait import or any simulation/World write.
            current.SetArmyUnitTablePointer(lookup, lookup);
            current.SetArmyUnitCommanderId(lookup, saved.Commander12);
            current.SetArmyUnitStatusFlag10(lookup, saved.Status10);
            var pointer = new ArmyUnitReference(lookup);
            bool? valid = current.ReadArmyUnitNativeValidity(pointer);
            if (valid == false) continue;
            int physical = lookup;
            var image = saved.ActiveUnitId is int id ? state.Units.GetValueOrDefault(id) : saved.PendingImage;
            //A missing image cannot retain opening-scenario values. The source
            //position adapter preserves actual conflicting/unknown knowledge.
            current.SetArmyUnitRawHex2A(physical, image is null ? null : ArmyMarchSourceAdapter.ReadCurrentHex(state, image));
            ushort? ready = image is { Troops: >= 0 and <= ushort.MaxValue } readyImage ? (ushort)readyImage.Troops : null;
            var previousResources = current.ReadArmyReturnResourceFields(pointer);
            current.BindArmyReturnResourceFields(physical, previousResources is { } existing
                ? existing with { Ready16 = ready } : new(ready, null, null, null, null));
            var receiver = queries.ReadReceiver(physical);
            byte? membership = receiver.Byte13C;
            if (membership is > 0) continue;
            ushort? hex = current.ReadArmyUnitRawHex2A(pointer);
            if (hex is null)
            {
                cannotAttributePotentialMember = true;
                Missing(physical, "Army_hex2A"); continue;
            }
            if (valid is null || membership is null)
            {
                byHex[hex.Value] = null;
                if (valid is null) Missing(physical, "Army_validity");
                if (membership is null) Missing(physical, "Army13C");
                continue;
            }
            ushort? troops = current.ReadArmyReturnResourceFields(pointer)?.Ready16;
            if (troops is null)
            {
                byHex[hex.Value] = null;
                Missing(physical, "Army_ready16"); continue;
            }
            //A zero-troop member still establishes the source list's presence.
            byHex[hex.Value] = byHex.TryGetValue(hex.Value, out int? before)
                ? before + troops.Value : troops.Value;
        }
        var totals = new Dictionary<int, int?>();
        for (int location = 1; location <= 61; location++)
        {
            var target = location <= 51 ? current.ReadCityTableReference(location)
                : current.ReadGateTableReference(location - 51);
            if (target is null)
            {
                totals[location] = null;
                unavailable.Add(new(null, location, "current.Base_label_Base_pointer")); continue;
            }
            bool? valid = current.ReadBaseNativeValidity(target);
            if (valid == false) continue;
            if (valid is null)
            {
                totals[location] = null;
                unavailable.Add(new(null, location, "current.Base_label_Base_validity")); continue;
            }
            try
            {
                ushort hex = current.ReadBaseCurrentHexIndex(target);
                if (cannotAttributePotentialMember) totals[location] = null;
                else if (byHex.TryGetValue(hex, out int? sum)) totals[location] = sum;
            }
            catch (InvalidDataException)
            {
                totals[location] = null;
                unavailable.Add(new(null, location, "current.Base_label_Base_hex"));
            }
        }
        return (new ReadOnlyDictionary<int, int?>(totals), unavailable.AsReadOnly());

        void Missing(int physical, string field) =>
            unavailable.Add(new(physical, null, "current.Base_label_" + field));
    }
}
