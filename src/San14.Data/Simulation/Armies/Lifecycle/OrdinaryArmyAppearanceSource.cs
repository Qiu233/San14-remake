using San14.Core;

namespace San14.Data;

public sealed record OrdinaryArmyAppearanceReceipt(int UnitId, int ArmyPhysical, byte FormerHold,
    byte RemainingHold, int? SelectedHex, int[] SortedCandidateHexes, string Branch);
public sealed record OrdinaryArmyAppearanceResult(GameState? State,
    OrdinaryArmyAppearanceReceipt[] Receipts, string? UnresolvedOwner);

/// <summary>The source departure of 25C4F0. Appearance chooses
/// a Base exit; it is not an adjacent movement edge and consumes no credit.</summary>
internal static partial class OrdinaryArmyAppearanceSource
{
    private static readonly (int X, int Y)[][] CityOffsets =
    [
        [(0,-2),(1,-1),(2,-1),(2,0),(2,1),(1,2),(0,2),(-1,2),(-2,1),(-2,0),(-2,-1),(-1,-1)],
        [(0,-2),(1,-2),(2,-1),(2,0),(2,1),(1,1),(0,2),(-1,1),(-2,1),(-2,0),(-2,-1),(-1,-2)]
    ];
    private static readonly (int X, int Y)[][] GateOffsets =
    [
        [(0,-1),(1,0),(1,1),(0,1),(-1,1),(-1,0)],
        [(0,-1),(1,-1),(1,0),(0,1),(-1,0),(-1,-1)]
    ];

    internal static (GameState State, OrdinaryArmyAppearanceReceipt Receipt) Depart(
        GameState state, SourceWorldContext current, UnitState army, bool borrowedCurrent = false,
        ArmyCampaignRuleAdapter? sourceRules = null, NativeMapSeasonDate? sourceDate = null)
    {
        int physical = army.NativeArmyOrigin?.NativeSlotIndex ?? throw Missing("Army_physical");
        var receiver = new ArmyUnitReference(physical);
        if (army.NativeTacticRuntime is not { Status10: 1 or 2 } runtime) throw Missing("ordinary_or_transport_status");
        byte formerHold = army.NativeRouteHold56 ?? throw Missing("hold");
        byte remainingHold = (byte)Math.Clamp(formerHold - 1, 0, 9);
        int force = current.ReadForcePhysicalSlot(current.ReadArmyUnitForceReference(receiver)) ?? throw Missing("force");
        var held = army with { NativeRouteHold56 = remainingHold };
        var working = state with { Units = new(state.Units) { [army.Id] = held } };
        if (remainingHold != 0) return Done(working, null, [], "held");
        if ((uint)force > 51 || force != army.ForceId) throw Missing("current_force_identity");
        if (army.March is null) throw Missing("March_owner");
        bool special = force is >= 46 and <= 51;
        int[] sorted = [];
        int? selected = null;
        if (special && !HasSpecialHome(current, receiver))
            selected = current.ReadArmyUnitRawHex2A(receiver) ?? throw Missing("special_current_Hex");
        else
        {
            byte action = ArmyConfigurationSourceAdapter.ReadAction(state, army) ?? throw Missing("action37");
            //2A6500 excludes only the actual effective Fire target from exits.
            //2B5610 can name the first intermediate rather than the final target.
            ushort? fireTarget = action == 16 ? OrdinaryArmyRouteSource.ReadCurrentTarget(state, army, current)
                ?? throw Missing("fire_effective_target") : null;
            ushort next = ArmyMarchSourceAdapter.EncodeDestination(army.March.NextStep) ?? throw Missing("initial_next");
            var home = current.FindArmyUnitCurrentBase(receiver);
            if (!(current.ReadBaseNativeValidity(home) ?? throw Missing("Base_validity")))
                return Done(working, null, [], "no_valid_base");
            var candidates = ReadCandidates(current, home);
            if (candidates.Length == 0) return Done(working, null, [], "no_candidates");
            int reference = candidates.Contains(next) ? next : candidates[0];
            //24DDE0/255360: attachment-free first, squared row/column distance,
            //then physical Hex identity. OrderBy retains duplicate-list stability.
            sorted = candidates.Select(hex => new
            {
                Hex = hex,
                Attachment = current.ReadCaptureHexWord16(hex) ?? throw Missing("candidate_attachment"),
                Distance = SquaredDistance(hex, reference)
            }).OrderBy(x => x.Attachment != 0).ThenBy(x => x.Distance).ThenBy(x => x.Hex).Select(x => x.Hex).ToArray();
            foreach (int hexId in sorted)
            {
                var hex = current.ReadHexTableReference(hexId) ?? throw Missing("candidate_Hex");
                byte surface = current.ReadHexSurfaceCode(hex) ?? throw Missing("candidate_surface");
                var terrain = current.ReadSurfaceTypeTableReference(surface <= 30 ? surface : 0) ?? throw Missing("candidate_Terrain");
                if (terrain.PhysicalSlot is null || (current.ReadSurfaceTypeRawField2A(terrain) ?? throw Missing("Terrain_passable")) == 0) continue;
                short occupant = unchecked((short)(current.ReadHexRaw1C(hex) ?? throw Missing("candidate_occupancy")));
                if (occupant is >= 1 and <= 500) continue;
                ushort link = current.ReadHexObjectLink(hex) ?? throw Missing("candidate_Object_link");
                var obj = current.ReadObjectTableReference(link <= 3000 ? link : 0) ?? throw Missing("candidate_Object");
                if (current.ReadObjectNativeValidity(obj) ?? throw Missing("candidate_Object_validity")) continue;
                if (fireTarget == hexId) continue;
                selected = hexId; break;
            }
        }
        if (selected is not int destination) return Done(working, null, sorted, "no_available_exit");
        var appeared = PlayableUnitPosition.AtHex(held, destination) with
        {
            March = held.March! with { NextStep = ArmyMarchDestination.None }
        };
        working = working with { Units = new(working.Units) { [army.Id] = appeared } };
        if (ArmyMarchSourceAdapter.ReadFirstWaypoint(state, held) == destination)
            working = ArmyWaypointArrivalSource.ConsumeCurrent(working, appeared) ?? throw Missing("waypoint_continuation");
        current.SetArmyUnitRawHex2A(physical, ArmyMarchSourceAdapter.ReadCurrentHex(working, appeared));
        var changed = (borrowedCurrent ? ArmyChangedListsSource.AppendCurrent(working, physical, current) : ArmyChangedListsSource.Append(working, physical, current));
        working = changed.State ?? throw Missing(changed.UnresolvedOwner ?? "changed_list");
        if (sourceRules is null) throw Missing("departure_source_services");
        //The source model/info/first-personality work precedes ownership and
        //reward. It must capture this exit, before a tutorial may move again.
        working = sourceRules.CompleteArmyDeparturePresentationCurrent(working, current, working.Units[army.Id], sourceDate);
        if (!special)
            working = sourceRules.ApplyArmyDepartureTerritoryCurrent(working, current, working.Units[army.Id], destination);
        var completed = working.Units[army.Id];
        working = working with { Units = new(working.Units) { [army.Id] = completed with
            { NativeTacticRuntime = completed.NativeTacticRuntime! with
                { Flags26 = (byte)(completed.NativeTacticRuntime.Flags26 | 0x42) } } } };
        //25C4F0's final241D70 only writes destination. It does not perform the
        //old-cell clear from ordinary2D99C0 traversal or change direction/credit.
        current.SetHexRaw1C(destination, checked((ushort)physical));
        working = working with { ArmyMapOccupancy = current.ExportArmyMapOccupancy() };
        return Done(working, destination, sorted, special ? "appeared_special_force" : "appeared_exit");

        (GameState, OrdinaryArmyAppearanceReceipt) Done(GameState result, int? hex, int[] order, string branch)
            => (result, new(army.Id, physical, formerHold, remainingHold, hex, order, branch));
    }

    //232860 reads the commander's raw Home and actual District->Force. It is
    //independent from2C96E0's later at-home position test.
    private static bool HasSpecialHome(SourceWorldContext current, ArmyUnitReference army)
    {
        int id = current.ReadArmyUnitCommanderId(army) ?? throw Missing("special_Commander12");
        var person = id <= 6000 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0);
        int physical = person?.PhysicalSlot ?? throw Missing("special_Person_pointer");
        int home = current.ReadPersonFirstLocation(physical) ?? throw Missing("special_PersonE2");
        if (home is < 1 or > 61) return false;
        if (!(current.ReadPersonNativeValidity(physical) ?? throw Missing("special_Person_validity"))) return false;
        var force = current.ReadPersonForce(physical) ?? throw Missing("special_Person_Force");
        int identity = force.PhysicalSlot ?? throw Missing("special_Force_identity");
        return identity is >= 46 and <= 50;
    }

    private static int[] ReadCandidates(SourceWorldContext current, BaseReference home)
    {
        //22F840 uses Object10, defaulting to Hex table0 for an invalid Object.
        ushort link = current.ReadBaseObjectLink(home) ?? throw Missing("Base_Object_link");
        var obj = current.ReadObjectTableReference(link <= 3000 ? link : 0) ?? throw Missing("Base_Object");
        bool valid = current.ReadObjectNativeValidity(obj) ?? throw Missing("Base_Object_validity");
        ushort raw = valid ? current.ReadObjectHexIndex(obj) ?? throw Missing("Base_Object_Hex") : (ushort)48400;
        var center = current.ReadHexTableReference(raw < 48400 ? raw : 0) ?? throw Missing("Base_Hex");
        if (center.PhysicalSlot is not int hex) return [];
        int x = hex % 220, y = hex / 220;
        int kind = current.ReadBaseNativeKind(home) ?? throw Missing("Base_kind");
        var offsets = (kind == 5 ? CityOffsets : GateOffsets)[x & 1];
        var result = new List<int>(offsets.Length);
        foreach (var (dx, dy) in offsets)
        {
            if (x + dx < 0 || y + dy < 0) continue;
            int index = (y + dy) * 220 + x + dx;
            //The original tests nonnegative coordinates but not x<220; a
            //linear index beyond the table resolves to the Hex0 receiver.
            var candidate = current.ReadHexTableReference(index < 48400 ? index : 0) ?? throw Missing("exit_Hex_pointer");
            if (candidate.PhysicalSlot is int physical) result.Add(physical);
        }
        return result.ToArray();
    }
    private static int SquaredDistance(int a, int b)
    { int x = a % 220 - b % 220, y = a / 220 - b / 220; return x * x + y * y; }
    internal static InvalidDataException Missing(string owner) => new("current.ordinary_appearance_" + owner);
}
