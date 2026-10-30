using System.Globalization;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //357140/364EA0/361F20. The three counters share the display rule,
    //but their raw nonzero icon palette is independent of this information gate.
    private string ReadUnitDetailAbnormal(GameState state, SourceWorldContext current, UnitState unit, byte counter)
    {
        if (counter == 0) return "";
        if (!UnitDetailCommandVisible(state, current, unit)) return UnitDetailData(1, 0x6D6);
        //The offline source constant is an integer4, converted to float by the
        //original getter. For a byte counter this integer ceiling is exact.
        return ((counter + 3) / 4).ToString(CultureInfo.InvariantCulture);
    }

    //32D390 returns a sprite PALETTE for the existing node57 grain icon.
    //It does not select a replacement sprite or calculate a logistics route.
    private int ReadUnitDetailSupplyPalette(GameState state, SourceWorldContext current, UnitState unit)
    {
        var army = UnitDetailArmyReceiver(unit);
        if (!UnitDetailStatusNeed(current.ReadArmyUnitNativeValidity(army), "supply.Army_validity")) return 12;
        if (current.IsArmyUnitBoundToSpecialForce(army)) return 11; //232860, no commander-validity gate.

        //34B940: an actual valid City/Gate home at the Army's current Hex
        //counts as connected before the two saved supply bits are consulted.
        bool supplied = AtHome();
        if (!supplied)
        {
            byte flags = unit.NativeTacticRuntime?.Flags26 ?? throw UnitDetailStatusMissing("supply.Army26");
            supplied = (flags & 0x42) == 0x42;
        }
        if (!supplied) return 12;

        //2338E0 only compares physical Force ordinals, with a nonzero player
        //ordinal. In particular it does not substitute the treaty predicate.
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current)
            ?? throw UnitDetailStatusMissing("supply.player_Force");
        int playerSlot = UnitDetailStatusNeed(current.ReadForcePhysicalSlot(player), "supply.player_Force_receiver");
        if (playerSlot == 0) return 11;
        var force = current.ReadArmyUnitForceReference(army)
            ?? throw UnitDetailStatusMissing("supply.Army_Force");
        if (UnitDetailStatusNeed(current.ReadForcePhysicalSlot(force), "supply.Army_Force_receiver") != playerSlot) return 11;

        //22A730 uses the registered current home, separately from22A630's
        //City/Gate descriptor lookup above. The mapped payer is22A940's City.
        var home = current.FindArmyUnitCurrentBase(army);
        if (!UnitDetailStatusNeed(current.ReadBaseNativeValidity(home), "supply.Home_validity")) return 11;
        var city = CurrentGateMapQueries.FindCity(home, current);
        if (!UnitDetailStatusNeed(current.ReadBaseNativeValidity(city), "supply.City_validity")) return 11;
        int citySlot = UnitDetailStatusNeed(city.PhysicalSlot, "supply.City_receiver");
        //This page's current capture does not bind the combat-City overlay.
        //Read the already saved C9 owner directly; binding all map fields for
        //one selected City would add an unrelated full-map projection.
        byte cityFlags = UnitDetailStatusNeed(state.NativeBaseCaptureCurrent?.Cities.GetValueOrDefault(citySlot)?.C9,
            "supply.CityC9");
        return (cityFlags & 2) != 0 ? 61 : 11;

        bool AtHome()
        {
            ushort commander = UnitDetailStatusNeed(current.ReadArmyUnitCommanderId(army), "supply.commander12");
            var person = (commander <= 6000 ? current.ReadPersonLookupReference(commander)
                : current.ReadTransferPersonTableReference(0))
                ?? throw UnitDetailStatusMissing("supply.commander_lookup");
            int personSlot = UnitDetailStatusNeed(person.PhysicalSlot, "supply.commander_receiver");
            int first = UnitDetailStatusNeed(current.ReadPersonNativeValidity(personSlot), "supply.commander_validity")
                ? UnitDetailStatusNeed(current.ReadPersonFirstLocation(personSlot), "supply.PersonE2") : 0;
            var basis = (first is >= 1 and <= 51 ? current.ReadCityTableReference(first)
                : first is >= 52 and <= 61 ? current.ReadGateTableReference(first - 51)
                : current.ReadCityTableReference(0)) ?? throw UnitDetailStatusMissing("supply.descriptor_Base");
            if (!UnitDetailStatusNeed(current.ReadBaseNativeValidity(basis), "supply.descriptor_Base_validity")) return false;
            ushort hex = UnitDetailStatusNeed(current.ReadArmyUnitRawHex2A(army), "supply.Army2A");
            return hex == current.ReadBaseCurrentHexIndex(basis); //22D880, including invalid-object48400.
        }
    }

    private static T UnitDetailStatusNeed<T>(T? value, string field) where T : struct
        => value ?? throw UnitDetailStatusMissing(field);
    private static InvalidDataException UnitDetailStatusMissing(string field)
        => new("unit.details." + field);
}
