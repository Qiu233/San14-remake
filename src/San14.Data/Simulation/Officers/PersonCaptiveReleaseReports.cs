using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //2E7890 report prefix only. The caller has resolved the original holder
    //and District and invokes this only for param2!=0, before releasing Person.
    //All state and current owners remain those of the enclosing operation.
    internal void ApplyCaptiveReleaseReports(SourceWorldContext current,
        Func<GameState> latest, Action<GameState> publish, int personPhysical,
        ArmyUnitReference holderArmy, BaseReference holderBase, DistrictReference holderDistrict,
        Func<int> readDay450, List<CampaignEffectRequest> effects)
    {
        InvalidDataException Missing(string field) => new("person-captive-release.report." + field);
        T Need<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        bool ArmyValid() => Need(current.ReadArmyUnitNativeValidity(holderArmy), "holder_Army_validity");
        ForceReference Player() => OwnedCurrentPlayerForce.ReadCurrentReference(latest(), current)
            ?? throw Missing("current_player_Force");
        bool PersonForceIsPlayer()
        {
            //22CB40 precedes2338E0. The latter does not test Force validity,
            //and its zero-player branch must not query the other identity.
            var force = current.ReadPersonForce(personPhysical) ?? throw Missing("Person_Force22CB40");
            int player = Need(current.ReadForcePhysicalSlot(Player()), "player_Force_identity");
            return player != 0 && player == Need(current.ReadForcePhysicalSlot(force), "Person_Force_identity");
        }
        bool HolderDistrictIsPlayer()
        {
            var player = Player();
            if (!Need(current.ReadForceNativeValidity(player), "District_player_Force_validity")) return false;
            int district = Need(holderDistrict.PhysicalSlot, "holder_District_pointer");
            return Need(current.ReadDistrictForceId(district), "holder_District10") ==
                Need(current.ReadForcePhysicalSlot(player), "District_player_Force_identity");
        }
        if (!Need(current.ReadDistrictNativeValidity(holderDistrict), "holder_District_validity") ||
            !(PersonForceIsPlayer() || HolderDistrictIsPlayer())) return;

        HistoryMessageOperands Operands()
        {
            ushort id = Need(current.ReadPersonIdentity(personPhysical), "Person10");
            //30A3E0 resets the Base-argument bank to zero on each call.
            //Only Army-valid reports obtain this real reset-owned Base0=0.
            return ArmyValid()
                ? new(Army0: holderArmy, Base0: 0, Person0: 5012, Person1: id)
                : new(Base0: Need(current.ReadBaseObjectLink(holderBase), "holder_Base4E"),
                    Person0: 5012, Person1: id);
        }
        var main = FormatCaptiveReleaseReport(latest(), current, 0x3240, Operands());
        publish(main.State);
        var supplemental = FormatCaptiveReleaseReport(latest(), current, 0x53A7, Operands());
        publish(supplemental.State);
        int districtId = HolderDistrictIsPlayer()
            ? Need(holderDistrict.PhysicalSlot, "history_District_ordinal22DD20")
            : Need(current.ReadPersonDistrictSlot(personPhysical), "history_PersonE0");
        int rawHex = ArmyValid()
            ? Need(current.ReadArmyUnitRawHex2A(holderArmy), "history_Army2A")
            : current.ReadBaseCurrentHexIndex(holderBase);
        var hex = current.ReadHexTableReference(rawHex < 48400 ? rawHex : 0)
            ?? throw Missing("history_Hex_lookup");
        int hexId = Need(current.ReadHexPhysicalSlot(hex), "history_Hex_ordinal22DEF0");
        int day = readDay450();
        //9354F0: param5=1, param10=0,param11=1,param12/13=0.
        //196CC0 above formats scratch strings; it does not queue two popups.
        publish(WriteHistory(latest(), current, new(1, districtId, hexId, day,
            main.Text, supplemental.Text, 5012, MarkBit0: 1, MarkBit4: 1), effects));
    }
}
