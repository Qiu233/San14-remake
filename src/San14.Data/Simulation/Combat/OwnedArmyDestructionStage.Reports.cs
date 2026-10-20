using San14.Core;

namespace San14.Data;

public sealed partial class OwnedArmyDestructionStage
{
    /// <summary>Exact259360 no-Army-winner5F58 at the source report point.
    /// Other report branches remain with their own consumers. The caller's
    /// transaction owns this intention until every later callback succeeds.</summary>
    private static void CaptureMoraleDefeatReport(GameState state, SourceWorldContext current,
        ArmyUnitReference victim, ArmyUnitReference winner, ushort reportHex,
        TacticSourceSamplerWorld? sampler, List<CampaignEffectRequest> sink)
    {
        if (!Known(current.ReadArmyUnitNativeValidity(victim), "report_victim_validity")) return;
        if (Known(current.ReadArmyUnitNativeValidity(winner), "report_winner_validity")) return;
        byte districtRaw = District();
        var districtPointer = current.ReadDistrictTableReference(districtRaw <= 51 ? districtRaw : 0)
            ?? throw Missing("report_District_pointer");
        int district = Known(districtPointer.PhysicalSlot, "report_District_physical");
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw Missing("report_player_pointer");
        if (!Known(current.ReadForceNativeValidity(player), "report_player_validity")) return;
        if (Known(current.ReadDistrictForceId(district), "report_District_force") != player.PhysicalSlot) return;
        int leader = Person(Known(current.ReadDistrictLeader(district), "report_District_leader"));
        if (!Known(current.ReadPersonNativeValidity(leader), "report_leader_validity") ||
            Known(current.ReadPersonStatus(leader), "report_leader_status") != 1) return;
        int cap = BasePostLoad.ReadMoraleUpper(current.ReadArmyUnitForceReference(victim), current);
        byte morale = Known(current.ReadArmyReturnResourceFields(victim)?.Morale1A, "report_current_Morale1A");
        if (Math.Min((int)morale, cap) >= 1) return;
        string name = "";
        //Message n2 resolves the current lookup Person, independently of the
        //raw Commander12 reporter read made after formatting below.
        if (Known(current.ReadArmyUnitNativeValidity(victim), "report_name_Army_validity"))
        {
            int commander = Person(Known(current.ReadArmyUnitCommanderId(victim), "report_name_Commander"));
            if (Known(current.ReadPersonNativeValidity(commander), "report_name_Person_validity"))
            {
                int id = Known(current.ReadPersonIdentity(commander), "report_name_Person_identity");
                name = state.Officers.GetValueOrDefault(id)?.Name ?? throw Missing("report_current_full_Name");
            }
        }
        ushort reporter = Known(current.ReadArmyUnitCommanderId(victim), "report_raw_Commander12");
        int day = sampler?.Day450 ?? throw Missing("report_current_Day450");
        byte recipientRaw = District(); //22BC70 repeats the real current lookup.
        var recipientPointer = current.ReadDistrictTableReference(recipientRaw <= 51 ? recipientRaw : 0)
            ?? throw Missing("report_recipient_pointer");
        byte recipient = checked((byte)Known(current.ReadDistrictPhysicalSlot(recipientPointer), "report_recipient_identity"));
        int physical = Known(victim.PhysicalSlot, "report_victim_pointer");
        int army = ArmyDestructionQueue.ResolveUnitId(state, physical) ?? throw Missing("report_victim_campaign_identity");
        sink.Add(new(null, null) { ArmyMoraleDefeatReport = new(army, reporter, name, recipient, reportHex, day) });

        int Person(ushort raw) =>
            (raw <= 6000 ? current.ReadPersonLookupReference(raw) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot
                ?? throw Missing("report_current_Person_pointer");
        byte District()
        {
            int person = Person(Known(current.ReadArmyUnitCommanderId(victim), "report_district_Commander"));
            return !Known(current.ReadPersonNativeValidity(person), "report_district_Person_validity") ? (byte)0 :
                Known(current.ReadPersonDistrictSlot(person), "report_district_current_E0");
        }
    }
}
