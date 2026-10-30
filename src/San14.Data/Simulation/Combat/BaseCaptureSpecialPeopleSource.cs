using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //2851B0 keeps the two independent source lists until after34AB returns.
    //2A8A writes history;34AB is the only dialog in this group.
    internal (GameState State, string? SpecialNotice) PrepareBaseCapturePeopleReportsCurrent(
        GameState state, SourceWorldContext current, BaseReference target, ArmyUnitReference winner,
        DistrictReference capturedDistrict, ForceReference winnerDistrictForce,
        IReadOnlyList<int> ordinaryPeople, IReadOnlyList<int> specialPeople,
        List<CampaignEffectRequest> effects)
    {
        string? notice = null;
        if (ordinaryPeople.Count != 0 && ReachedPlayerGate() &&
            BasePeopleNeed(current.ReadArmyUnitNativeValidity(winner), "report_winner_validity"))
        {
            var reporter = SelectDistrict(ordinaryPeople[0]);
            int commanderId = BasePeopleNeed(current.ReadArmyUnitCommanderId(winner), "report_Commander12");
            int commander = BasePeopleNeed(current.ReadPersonLookupReference(commanderId <= 6000 ? commanderId : 0)?.PhysicalSlot,
                "report_Commander_pointer");
            var formatted = FormatReportMessage(state, current, 0x2A8A,
                new(Person0: BasePeopleNeed(current.ReadPersonIdentity(commander), "report_Commander10"),
                    Person1: BasePeopleNeed(current.ReadPersonIdentity(ordinaryPeople[0]), "report_first_Person10"),
                    Integers: [ordinaryPeople.Count]), ReportMessageOrigin.BaseCapturePeople, null);
            state = formatted.State;
            int day = (ReadArmyDestructionSampler(state, current) ?? throw BasePeopleMissing("report_World450")).Day450;
            state = WriteHistory(state, current, new(1,
                BasePeopleNeed(current.ReadDistrictPhysicalSlot(reporter), "report_District60"),
                current.ReadBaseCurrentHexIndex(target), day, formatted.Text, string.Empty, 5012,
                MarkBit0: 0, MarkBit3: 1, MarkBit4: 1, MarkBit5: 0, MarkBit6: 0), effects);
        }
        if (specialPeople.Count != 0 && ReachedPlayerGate() &&
            BasePeopleNeed(current.ReadArmyUnitNativeValidity(winner), "notice_winner_validity"))
        {
            //The source reads this actual ordinal even though this second
            //block has no history call and does not use it as a speaker.
            _ = BasePeopleNeed(current.ReadDistrictPhysicalSlot(SelectDistrict(specialPeople[0])), "notice_District60");
            var formatted = FormatReportMessage(state, current, 0x34AB,
                new(Integers: [specialPeople.Count]), ReportMessageOrigin.BaseCapturePeople, null);
            state = formatted.State;
            if (BaseCapturePeopleNoticeAllowedCurrent(state, current)) notice = formatted.Text;
        }
        return (state, notice);

        bool ReachedPlayerGate() => TalentForceIsPlayerCurrent(state, current, winnerDistrictForce) ||
            BasePeopleNeed(current.ReadDistrictNativeValidity(capturedDistrict), "captured_District_validity") &&
            IsPlayerDistrict(capturedDistrict);
        bool IsPlayerDistrict(DistrictReference district)
        {
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current)
                ?? throw BasePeopleMissing("player_Force_pointer");
            if (!BasePeopleNeed(current.ReadForceNativeValidity(player), "player_Force_validity")) return false;
            return BasePeopleNeed(current.ReadDistrictForceSlot(district), "District_raw_Force10") ==
                BasePeopleNeed(current.ReadForcePhysicalSlot(player), "player_Force60");
        }
        DistrictReference SelectDistrict(int firstPerson)
        {
            if (BasePeopleNeed(current.ReadDistrictNativeValidity(capturedDistrict), "selected_captured_District_validity") &&
                IsPlayerDistrict(capturedDistrict)) return capturedDistrict;
            int raw = BasePeopleNeed(current.ReadPersonDistrictSlot(firstPerson), "first_PersonE0");
            var first = current.ReadDistrictTableReference(raw <= 51 ? raw : 0) ?? throw BasePeopleMissing("first_District_pointer");
            if (BasePeopleNeed(current.ReadDistrictNativeValidity(first), "first_District_validity") && IsPlayerDistrict(first)) return first;
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current)
                ?? throw BasePeopleMissing("fallback_player_Force");
            return ForcePrimaryDistrictSource.Resolve(current, player);
        }
    }

    private bool BaseCapturePeopleNoticeAllowedCurrent(GameState state, SourceWorldContext current)
    {
        if (BasePeopleNeed(ReadApplicationReturnToTitlePending(), "notice_return_title")) return false;
        var blocker = current.ReadReportBlocker() ?? (state.CampaignEventEligibility?.HistoryRecordingSuppressed is bool suppressed
            ? new ReportBlockerState(true, suppressed ? 1 : 0) : null) ?? throw BasePeopleMissing("notice_event_manager1B0");
        return !blocker.Present || BasePeopleNeed(blocker.RawField1B0, "notice_event_manager1B0_value") == 0;
    }

    //2E0FC0(Person,winner,0,0,null,null) reaches265250 immediately for
    //an employed bit14 Person. Reuse its complete prefix, live Army/Return
    //cleanup and32BC50 producer. Turn action53 alone reclaims the Person.
    internal GameState ApplyBaseCaptureSpecialPersonCurrent(GameState state, SourceWorldContext current,
        WorldIndexContext pool, int person, NativeMapSeasonDate? date, TacticSourceSamplerWorld? sampler,
        List<CampaignEffectRequest> effects)
    {
        TacticSourceSamplerWorld ReadSampler() => sampler ?? ReadArmyDestructionSampler(state, current)
            ?? throw BasePeopleMissing("special_capture_sampler");
        return ApplyCaptiveFreedomCurrent(state, current, pool, person, date,
            () => ReadSampler().Day450, ReadSampler, effects);
    }

    private static T BasePeopleNeed<T>(T? value, string field) where T : struct => value ?? throw BasePeopleMissing(field);
    private static InvalidDataException BasePeopleMissing(string field) => new("capture.people." + field);
}
