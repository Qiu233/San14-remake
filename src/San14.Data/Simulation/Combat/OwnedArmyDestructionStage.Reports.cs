using San14.Core;

namespace San14.Data;

public sealed partial class OwnedArmyDestructionStage
{
    /// <summary>Exact259360 no-Army-winner5F58 at the source report point.
    /// Other report branches remain with their own consumers. The caller's
    /// transaction owns this intention until every later callback succeeds.</summary>
    private GameState CaptureMoraleDefeatReport(GameState state, SourceWorldContext current,
        ArmyUnitReference victim, ArmyUnitReference winner, ushort reportHex,
        TacticSourceSamplerWorld? sampler, List<CampaignEffectRequest> sink)
    {
        if (!Known(current.ReadArmyUnitNativeValidity(victim), "report_victim_validity")) return state;
        if (Known(current.ReadArmyUnitNativeValidity(winner), "report_winner_validity")) return state;
        byte districtRaw = District();
        var districtPointer = current.ReadDistrictTableReference(districtRaw <= 51 ? districtRaw : 0)
            ?? throw Missing("report_District_pointer");
        int district = Known(districtPointer.PhysicalSlot, "report_District_physical");
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw Missing("report_player_pointer");
        if (!Known(current.ReadForceNativeValidity(player), "report_player_validity")) return state;
        if (Known(current.ReadDistrictForceId(district), "report_District_force") != player.PhysicalSlot) return state;
        int leader = Person(Known(current.ReadDistrictLeader(district), "report_District_leader"));
        if (!Known(current.ReadPersonNativeValidity(leader), "report_leader_validity") ||
            Known(current.ReadPersonStatus(leader), "report_leader_status") != 1) return state;
        int cap = BasePostLoad.ReadMoraleUpper(current.ReadArmyUnitForceReference(victim), current);
        byte morale = Known(current.ReadArmyReturnResourceFields(victim)?.Morale1A, "report_current_Morale1A");
        if (Math.Min((int)morale, cap) >= 1) return state;
        var formatted = package.FormatHistoryReport(state, current, 0x5F58,
            new HistoryMessageOperands(Army0: victim));
        state = formatted.State;
        ushort reporter = Known(current.ReadArmyUnitCommanderId(victim), "report_raw_Commander12");
        int day = sampler?.Day450 ?? throw Missing("report_current_Day450");
        byte recipientRaw = District(); //22BC70 repeats the real current lookup.
        //TC25AFDC uses both marked switches. Keep the raw District byte and
        //captured ushort Hex; only the shared writer clamps stored fields.
        return package.WriteHistory(state, current, new ReportCommand(5, recipientRaw, reportHex, day, formatted.Text,
            CampaignHistoryWriter.Supplemental(state), reporter, MarkBit3: 1, MarkBit4: 1), sink);

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
