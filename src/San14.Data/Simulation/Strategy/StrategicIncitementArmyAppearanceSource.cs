using San14.Core;

namespace San14.Data;

internal static partial class OrdinaryArmyAppearanceSource
{
    //The special creator calls the same 2A6500 exit selector only for a real
    //46..50 commander with a retained Home; otherwise25C4F0 preserves2A.
    internal static int? SelectSpecialCreationExit(GameState state, SourceWorldContext current, UnitState army)
    {
        var home = current.FindArmyUnitCurrentBase(new(army.NativeArmyOrigin!.NativeSlotIndex));
        if (!(current.ReadBaseNativeValidity(home) ?? throw Missing("special_creation_Base_validity"))) return null;
        int[] candidates = ReadCandidates(current, home);
        if (candidates.Length == 0) return null;
        int reference = candidates[0]; //The creator's reset Next48 is BD10.
        var sorted = candidates.Select(hex => new
        {
            Hex = hex,
            Attachment = current.ReadCaptureHexWord16(hex) ?? throw Missing("special_candidate_attachment"),
            Distance = SquaredDistance(hex, reference)
        }).OrderBy(row => row.Attachment != 0).ThenBy(row => row.Distance).ThenBy(row => row.Hex);
        foreach (var row in sorted)
        {
            var hex = current.ReadHexTableReference(row.Hex) ?? throw Missing("special_candidate_Hex");
            int surface = current.ReadHexSurfaceCode(hex) ?? throw Missing("special_candidate_surface");
            var terrain = current.ReadSurfaceTypeTableReference(surface < 31 ? surface : 0) ?? throw Missing("special_candidate_Terrain");
            if (terrain.PhysicalSlot is null || (current.ReadSurfaceTypeRawField2A(terrain) ?? throw Missing("special_candidate_passable")) == 0)
                continue;
            if (unchecked((short)(current.ReadHexRaw1C(hex) ?? throw Missing("special_candidate_Army"))) is >= 1 and <= 500)
                continue;
            var obj = FacilityConstructionLegality.ObjectAt(current, hex);
            if (current.ReadObjectNativeValidity(obj) ?? throw Missing("special_candidate_Object_validity")) continue;
            return row.Hex;
        }
        return null;
    }
}

public sealed partial class ArmyCampaignRuleAdapter
{
    private GameState? ActivateStrategicIncitementArmyCurrent(GameState state, SourceWorldContext current,
        int id, int physical, int person, int force)
    {
        var army = state.Units[id];
        byte hold = (byte)Math.Clamp(StrategyNeed(army.NativeRouteHold56, "incitement_appearance_hold") - 1, 0, 9);
        if (hold != 0) return null;
        int home = StrategyNeed(current.ReadPersonFirstLocation(person), "incitement_appearance_PersonE2");
        int? selected = force is >= 46 and <= 50 && home is >= 1 and <= 61
            ? OrdinaryArmyAppearanceSource.SelectSpecialCreationExit(state, current, army) : army.HexId;
        if (selected is not int hex) return null;
        army = army with { HexId = hex, NativeRouteHold56 = hold,
            March = army.March! with { NextStep = ArmyMarchDestination.None },
            NativeTacticRuntime = army.NativeTacticRuntime! with { Flags26 = (byte)(army.NativeTacticRuntime.Flags26 | 0x42) } };
        state = state with { Units = new(state.Units) { [id] = army } };
        current.SetArmyUnitRawHex2A(physical, (ushort)hex);
        var changed = ArmyChangedListsSource.AppendCurrent(state, physical, current);
        state = changed.State ?? throw StrategyMissing(changed.UnresolvedOwner ?? "incitement_appearance_changed_list");
        current.SetHexRaw1C(hex, (ushort)physical);
        //The special Force skips25C4F0's first territory/merit branch. Its
        //2D3410 caller independently executes185D00(cityarg0) afterwards.
        var target = current.ReadHexTableReference(hex) ?? throw StrategyMissing("incitement_appearance_Hex");
        int before = StrategyNeed(current.ReadHexForceSlot(target), "incitement_appearance_Hex14");
        if (before != force && GateCaptureTerritorySource.Eligible(state, current, target, force, false))
        {
            current.AssignHexForce(target, force);
            var mode = current.ArmyReturnTransferMode ?? throw StrategyMissing("incitement_appearance_transfer_mode");
            if (mode.Present && StrategyNeed(mode.Value, "incitement_appearance_transfer_mode_value") != 0)
                throw StrategyMissing("incitement_appearance_transfer_mode_counter");
            int physicalHex = StrategyNeed(target.PhysicalSlot, "incitement_appearance_Hex_physical");
            var territory = state.Territory.GetValueOrDefault(physicalHex) ?? throw StrategyMissing("incitement_appearance_Territory");
            state = state with { Territory = new(state.Territory) { [physicalHex] = territory with { ForceId = force } } };
            int area = StrategyNeed(current.ReadHexAreaSlot(target), "incitement_appearance_Area");
            var region = state.Areas.GetValueOrDefault(area) ?? throw StrategyMissing("incitement_appearance_Area_owner");
            int owner = StrategyNeed(current.ReadAreaCurrentForceSlot(new(area)), "incitement_appearance_Area_Force");
            state = state with { Areas = new(state.Areas) { [area] = region with { ForceId = owner == 0 ? null : owner } } };
        }
        //241D70 is repeated by the creator after185D00, without clearing a
        //previous cell. Both writes target the actual lookup receiver.
        current.SetHexRaw1C(StrategyNeed(target.PhysicalSlot, "incitement_appearance_final_Hex"), (ushort)physical);
        return state with { ArmyMapOccupancy = current.ExportArmyMapOccupancy() };
    }
}
