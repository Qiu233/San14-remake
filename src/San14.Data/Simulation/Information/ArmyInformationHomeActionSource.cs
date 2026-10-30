using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //355F80: resolve the commander's home before testing the special Forces.
    private string ReadUnitDetailHome(GameState state, SourceWorldContext current, UnitState unit)
    {
        var army = UnitDetailArmyReceiver(unit);
        int commander = UnitDetailHomeActionNeed(current.ReadArmyUnitCommanderId(army), "home.commander12");
        var person = (commander <= 6000 ? current.ReadPersonLookupReference(commander)
            : current.ReadTransferPersonTableReference(0))
            ?? throw UnitDetailHomeActionMissing("home.commander_lookup");
        int personSlot = UnitDetailHomeActionNeed(person.PhysicalSlot, "home.commander_receiver");
        BaseKind kind = BaseKind.City;
        int index = 0;
        if (UnitDetailHomeActionNeed(current.ReadPersonNativeValidity(personSlot), "home.commander_validity"))
        {
            int first = UnitDetailHomeActionNeed(current.ReadPersonFirstLocation(personSlot), "home.PersonE2");
            //22A6C0 has a City0 fallback for every non-Base descriptor.
            if (first is >= 1 and <= 51) index = first;
            else if (first is >= 52 and <= 61) { kind = BaseKind.Gate; index = first - 51; }
        }

        var force = current.ReadArmyUnitForceReference(army)
            ?? throw UnitDetailHomeActionMissing("home.Force_lookup");
        int forceSlot = UnitDetailHomeActionNeed(current.ReadForcePhysicalSlot(force), "home.Force_receiver");
        if (forceSlot == 51) return "";
        if (forceSlot is >= 46 and <= 50 && !current.IsArmyUnitBoundToSpecialForce(army)) return "";

        return UnitDetailBaseName(state, current, kind, index);
    }

    private string UnitDetailBaseName(GameState state, SourceWorldContext current, BaseKind kind, int index)
    {
        var location = (kind == BaseKind.City ? current.ReadCityTableReference(index) : current.ReadGateTableReference(index))
            ?? throw UnitDetailHomeActionMissing("home.Base_lookup");
        int physical = UnitDetailHomeActionNeed(location.PhysicalSlot, "home.Base_receiver");
        //The source directly reads Name12, even on the invalid placeholder.
        //Positive receivers use the current names; slot0's immutable name is
        //already loaded from the normal fixed-resource definition.
        if (physical != 0)
            return kind == BaseKind.City
                ? state.Cities.GetValueOrDefault(physical)?.Name ?? throw UnitDetailHomeActionMissing("home.City_current_name12")
                : state.Gates.GetValueOrDefault(physical + 51)?.Name ?? throw UnitDetailHomeActionMissing("home.Gate_current_name12");
        if (kind == BaseKind.City)
        {
            var rows = _scenario.Snapshot.Cities;
            if (rows.Count == 0 || rows[0].Slot != 0) throw UnitDetailHomeActionMissing("home.City0_definition");
            return rows[0].Name;
        }
        else
        {
            var rows = _scenario.Snapshot.Gates;
            if (rows.Count == 0 || rows[0].Slot != 0) throw UnitDetailHomeActionMissing("home.Gate0_definition");
            return rows[0].Name;
        }
    }

    //35DC70 ->30AE60: the information gate precedes the Army37 read.
    private string ReadUnitDetailAction(GameState state, SourceWorldContext current, UnitState unit)
    {
        if (!UnitDetailCommandVisible(state, current, unit)) return UnitDetailData(1, 0x6D6);
        byte action = UnitDetailHomeActionNeed(ArmyConfigurationSourceAdapter.ReadAction(state, unit), "command.Army37");
        return action is >= 1 and <= 17 ? UnitDetailData(0, 0x103 + action) : "";
    }

    //Shared with the two target values and the remaining command-page fields.
    //3EB010 is the CEnvironment selector, not an application display flag.
    private bool UnitDetailCommandVisible(GameState state, SourceWorldContext current, UnitState unit)
    {
        int selector = UnitDetailHomeActionNeed(BattleRemakeCurrentSettings.ReadSelectorE58(state), "command.environment_selector");
        int restriction = UnitDetailHomeActionNeed(BattleRemakeCurrentSettings.ReadInt32(state, selector == 0 ? 0x2CC : 0xD4),
            "command.environment_information_mode");
        if (restriction != 1) return true;
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current)
            ?? throw UnitDetailHomeActionMissing("command.player_Force");
        var force = current.ReadArmyUnitForceReference(UnitDetailArmyReceiver(unit))
            ?? throw UnitDetailHomeActionMissing("command.army_Force");
        return new DiplomacyCommandSource(state, current).HasTreaty(
            UnitDetailHomeActionNeed(current.ReadForcePhysicalSlot(player), "command.player_Force_receiver"),
            UnitDetailHomeActionNeed(current.ReadForcePhysicalSlot(force), "command.army_Force_receiver"));
    }

    private string UnitDetailData(int section, int id) => ReadHistoryDataText(section, id);

    private static ArmyUnitReference UnitDetailArmyReceiver(UnitState unit)
    {
        int slot = unit.NativeArmyOrigin?.NativeSlotIndex
            ?? throw UnitDetailHomeActionMissing("Army_receiver");
        if (slot is < 0 or > 500) throw UnitDetailHomeActionMissing("Army_receiver_range");
        return new(slot);
    }

    private static T UnitDetailHomeActionNeed<T>(T? value, string field) where T : struct
        => value ?? throw UnitDetailHomeActionMissing(field);
    private static InvalidDataException UnitDetailHomeActionMissing(string field)
        => new($"unit.details.{field}");
}
