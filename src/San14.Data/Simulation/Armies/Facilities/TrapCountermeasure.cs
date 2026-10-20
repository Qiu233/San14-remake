using San14.Core;
using static San14.Data.TrapParameters;

namespace San14.Data;

internal sealed partial class TrapEffectApplication
{
    /// <summary>18F3D0: the first reached commander effect81, then98,
    /// counters the whole trap. This is a permanent selected-trait lookup;
    /// neither B4 conditions nor the activation mask participates.</summary>
    private bool Countered(ObjectReference facility, int kind,
        FacilityEffectDefinition definition, HexListHandle[] channels)
    {
        var record = Current.ReadPhysicalObject(Need(facility.PhysicalSlot, "counter_Object_pointer"))
            ?? throw Missing("counter_Object_record");
        var reference = Current.ReadObjectDefinitionReference(record.RawField12 <= 30 ? record.RawField12 : 0)
            ?? throw Missing("counter_definition_pointer");
        if (!Need(Current.ReadObjectDefinitionValidity(reference), "counter_definition_validity")) return false;
        if (Need(reference.PhysicalSlot, "counter_definition_identity") != kind)
            throw Missing("counter_definition_identity_changed");
        for (int channel = 0; channel < 2; channel++)
        {
            if (definition.Channel(channel).Effect == 17) continue;
            foreach (var hex in FacilityEffectTargets.ReadHexes(Current, channels[channel]))
            {
                //2C6D20 only tests the indexed Hex's validity and signed1C.
                //It does not resolve16EB20, exclusions or at-Home status.
                int identity = Need(Current.ReadHexPhysicalSlot(hex), "counter_Hex_identity");
                var indexed = Current.ReadHexTableReference(identity < 48400 ? identity : 0)
                    ?? throw Missing("counter_indexed_Hex_pointer");
                if (!indexed.PhysicalSlot.HasValue) continue;
                int first = unchecked((short)Need(Current.ReadHexRaw1C(indexed), "counter_indexed_signed1C"));
                if (first is < 1 or > 500) continue;
                //The independent231660 call uses the original Hex pointer.
                int raw = unchecked((short)Need(Current.ReadHexRaw1C(hex), "counter_signed1C_again"));
                if (raw < 0) throw Missing("counter_negative_Army_pointer_index");
                var army = Current.ReadArmyUnitTableReference(raw <= 500 ? raw : 0)
                    ?? throw Missing("counter_Army_pointer");
                if (!Need(Current.ReadArmyUnitNativeValidity(army), "counter_Army_validity")) continue;
                var commander = CounterCommander(army);
                if (!CounterPersonValid(commander)) continue;
                int person = Need(commander.PhysicalSlot, "counter_Commander_pointer");
                var trait = CounterFirstTrait(person, 81);
                if (!Need(Current.ReadSkillPersonalityValidity(trait), "counter_first_trait_validity"))
                    trait = CounterFirstTrait(person, 98);
                if (!Need(Current.ReadSkillPersonalityValidity(trait), "counter_final_trait_validity")) continue;

                //Both source kind checks occur before either report gate.
                if (kind == 14) Feedback.Speech(army, CombatSpeechEvent.TrapPitfall, facility);
                if (kind == 17) Feedback.Speech(army, CombatSpeechEvent.TrapRockfall, facility);
                bool trapPlayer = CounterTrapPlayerDistrict(facility);
                //Do not short circuit this getter/validity/repeated getter
                //when the trap-side player result is already true.
                var district = CounterArmyDistrict(army);
                bool armyPlayer = false;
                if (Need(Current.ReadDistrictNativeValidity(district), "counter_Army_District_validity"))
                    armyPlayer = CounterDistrictPlayer(CounterArmyDistrict(army));
                if (armyPlayer || trapPlayer)
                    Feedback.CounterReport(facility, kind, army,
                        Need(trait.PhysicalSlot, "counter_trait_identity"), hex);
                Feedback.Speech(army, CombatSpeechEvent.TrapCountered, facility, kind);
                return true;
            }
        }
        return false;
    }

    private PersonalityReference CounterFirstTrait(int person, byte effect)
    {
        //2AB720 performs its own fresh Person validity read on each attempt.
        if (!Need(Current.ReadPersonNativeValidity(person), "counter_trait_Person_validity")) return new(null);
        for (int index = 0; index < 9; index++)
        {
            var trait = TacticRemakeCurrentTraits.ResolveCommanderPersonality(Current, person, index)
                ?? throw Missing("counter_Person230730_trait_pointer");
            if (!Need(Current.ReadSkillPersonalityValidity(trait), "counter_Personality_validity")) continue;
            if (Need(Current.ReadPersonalityByte(trait, 0xB6), "counter_trait_B6") == effect ||
                Need(Current.ReadPersonalityByte(trait, 0xBA), "counter_trait_BA") == effect) return trait;
        }
        return new(null);
    }

    private TransferPersonReference CounterCommander(ArmyUnitReference army)
    {
        ushort raw = Need(Current.ReadArmyUnitCommanderId(army), "counter_Commander12");
        return (raw <= 6000 ? Current.ReadPersonLookupReference(raw) : Current.ReadTransferPersonTableReference(0))
            ?? throw Missing("counter_Commander_lookup");
    }
    private bool CounterPersonValid(TransferPersonReference person) => person.PhysicalSlot is int physical &&
        Need(Current.ReadPersonNativeValidity(physical), "counter_Commander_validity");

    private DistrictReference CounterArmyDistrict(ArmyUnitReference army)
    {
        var person = CounterCommander(army);
        byte raw = CounterPersonValid(person)
            ? Need(Current.ReadPersonDistrictSlot(Need(person.PhysicalSlot, "counter_District_Person_pointer")), "counter_PersonE0")
            : (byte)0;
        return Current.ReadDistrictTableReference(raw <= 51 ? raw : 0)
            ?? throw Missing("counter_Army_District_pointer");
    }

    private bool CounterTrapPlayerDistrict(ObjectReference facility)
    {
        int rawForce = Need(Current.ReadObjectForceSlot13(facility), "counter_Object13");
        var force = Current.ReadForceTableReference(rawForce <= 51 ? rawForce : 0)
            ?? throw Missing("counter_Object_Force_pointer");
        if (!Feedback.PlayerForce(force)) return false; //2338E0 has no Force-validity gate.
        ushort rawHex = Need(Current.ReadObjectHexIndex(facility), "counter_Object10");
        var hex = Current.ReadHexTableReference(rawHex < 48400 ? rawHex : 0)
            ?? throw Missing("counter_Object_Hex_pointer");
        if (!hex.PhysicalSlot.HasValue) return false;
        ushort rawArea = Need(Current.ReadHexAreaSlot(hex), "counter_Hex12");
        var area = Current.ReadAreaTableReference(rawArea <= 500 ? rawArea : 0)
            ?? throw Missing("counter_Area_pointer");
        if (!Need(Current.ReadAreaNativeValidity(area), "counter_Area_validity")) return false;
        byte rawCity = Need(Current.ReadAreaCitySlot(area), "counter_Area35");
        var city = Current.ReadCityTableReference(rawCity <= 51 ? rawCity : 0)
            ?? throw Missing("counter_City_pointer");
        if (!Need(Current.ReadBaseNativeValidity(city), "counter_City_validity")) return false;
        var district = Current.ReadCityDistrictReference(city) ?? throw Missing("counter_City_current_District");
        return Need(Current.ReadDistrictNativeValidity(district), "counter_City_District_validity") &&
            CounterDistrictPlayer(district);
    }

    private bool CounterDistrictPlayer(DistrictReference district)
    {
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(State, Current)
            ?? throw Missing("counter_player_Force_pointer");
        if (!Need(Current.ReadForceNativeValidity(player), "counter_player_Force_validity")) return false;
        byte force = Need(Current.ReadDistrictForceSlot(district), "counter_District_Force10");
        return force == Need(Current.ReadForcePhysicalSlot(player), "counter_player_Force_identity");
    }
}
