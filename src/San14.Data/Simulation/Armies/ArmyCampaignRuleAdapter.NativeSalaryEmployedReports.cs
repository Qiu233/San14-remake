using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The reached employed-person salary report tail. The caller
    /// owns the actual shortage flags, ordered departure list and effect batch.
    /// ordinaryShortage is the source's any-retained-loyalty-loss flag;
    /// specialShortage is its special-person remaining-in-service flag.</summary>
    internal GameState CompleteNativeSalaryEmployedReportsCurrent(GameState state,
        SourceWorldContext current, BaseReference receiver, bool ordinaryShortage,
        bool specialShortage, HexListHandle departingSpecialPeople,
        ref TransferPersonReference? reportSpeaker, List<CampaignEffectRequest> effects)
    {
        //Each source report branch selects again, even if the prior branch
        //left a valid speaker. The later captive report may reuse this result.
        if (ordinaryShortage && IsPlayerReceiver()) reportSpeaker = ShortageReport(0x5E3A);
        if (specialShortage && IsPlayerReceiver()) reportSpeaker = ShortageReport(0x5E3C);

        var node = current.ReadRegistryHead(new(departingSpecialPeople.Identity))
            ?? throw SalaryEmployedReportMissing("departure_head");
        //The source's player test encloses narration, history AND Freedom.
        //NPC special departures are not processed by this salary tail.
        if (!node.Identity.HasValue || !IsPlayerReceiver()) return state;
        int count = K(current.ReadTemporaryHexListCount(departingSpecialPeople), "departure_count");
        string narration = Format(0x34A8, new(Integers: [count]));
        effects.Add(new(null, null) { MonthlyMaintenance =
            new(MonthlyMaintenanceOperation.NarrationText, null, null, narration) });

        var pool = current.ReadOwnedCurrentListPool()
            ?? throw SalaryEmployedReportMissing("departure_current_pool");
        while (node.Identity.HasValue)
        {
            var value = current.ReadRegistryNodePayload(node)
                ?? throw SalaryEmployedReportMissing("departure_payload");
            if (value.Kind != RegistryKind.Person) throw SalaryEmployedReportMissing("departure_kind");
            int person = K(value.PhysicalSlot, "departure_Person_pointer");
            string main = Format(0x5E19, new(
                Force0: K(current.ReadBaseForceSlot(receiver), "departure_Base_Force"),
                Person0: K(current.ReadPersonIdentity(person), "departure_Person_identity")));
            var force = ReceiverForce();
            string detail = Format(0x5239, new(Person0: 5012,
                Person1: K(current.ReadMoraleWaveRuler22EAC0(force), "departure_report_ruler"),
                Person2: K(current.ReadPersonIdentity(person), "departure_report_Person_identity")));
            int district = K(ForcePrimaryDistrictSource.Resolve(current, ReceiverForce()).PhysicalSlot,
                "departure_primary_District");
            var position = PersonReturnCommands.ResolvePersonPosition(state, current, person, Date);
            if (current.ReadOwnedPersonCommandManager() is { } tasks) state = state with { PersonnelCommands = tasks };
            int hex = K(position.PhysicalSlot, "departure_Person_Hex");
            state = WriteHistory(state, current, new(2, district, hex, Day450(), main, detail,
                5012, MarkBit0: 1, MarkBit4: 1), effects);

            //265250 receives the real receiver Force's raw ruler10 Person,
            //not the selected report speaker or a departing person's force.
            int forcePhysical = K(ReceiverForce().PhysicalSlot, "departure_receiver_Force_pointer");
            ushort rawRuler = K(current.ReadForceRecord(forcePhysical)?.RawRuler10, "departure_Force_ruler10");
            var formerRuler = current.ReadPersonLookupReference(rawRuler <= 6000 ? rawRuler : 0)
                ?? throw SalaryEmployedReportMissing("departure_former_ruler_lookup");
            state = ApplyCaptiveFreedomCurrent(state, current, pool, person,
                state.NativeMapSeason?.ThirdDate, Day450, Sampler, effects, formerRuler: formerRuler);
            node = current.ReadRegistryNodeNext(node) ?? throw SalaryEmployedReportMissing("departure_next");
        }
        return state;

        TransferPersonReference ShortageReport(int spokenRoot)
        {
            var district = current.ReadBaseDistrictReference(receiver)
                ?? throw SalaryEmployedReportMissing("report_Base_District");
            TransferPersonReference speaker;
            if (K(current.ReadDistrictNativeValidity(district), "report_District_validity"))
            {
                var speakerPool = current.ReadOwnedCurrentListPool()
                    ?? throw SalaryEmployedReportMissing("report_current_pool");
                speaker = new PersonDeathHistorySelection(state, current, speakerPool, SearchAbilities(current),
                    _governorInputs, (id, effect) => _baseCapacityInputs.ReadPanelModifier(state, id, effect))
                    .Select(district);
            }
            else speaker = current.ReadPersonLookupReference(5012)
                ?? throw SalaryEmployedReportMissing("report_default_Person5012");
            effects.Add(new(null, null) { MonthlyMaintenance =
                new(MonthlyMaintenanceOperation.ControllerQueueEvent, 6, null, null) });
            int speakerPhysical = K(speaker.PhysicalSlot, "report_speaker_pointer");
            string spoken = Format(spokenRoot, new(
                Person0: K(current.ReadPersonIdentity(speakerPhysical), "report_speaker_identity"),
                Person1: K(current.ReadMoraleWaveRuler22EAC0(ReceiverForce()), "report_ruler"),
                Base0: K(current.ReadBaseObjectLink(receiver), "report_Base4E")));
            string main = Format(0x5E3F, new(Base0: K(current.ReadBaseObjectLink(receiver), "history_Base4E")));
            ushort speakerId = K(current.ReadPersonIdentity(speakerPhysical), "history_speaker_identity");
            int day = Day450();
            int hex = ReadReportBaseMarker(current, receiver, SalaryEmployedReportMissing);
            int districtId = K(current.ReadBaseDistrictSlot(receiver), "history_Base_virtual78");
            state = WriteHistory(state, current, new(5, districtId, hex, day, main, spoken,
                speakerId, MarkBit0: 1, MarkBit4: 1), effects);
            effects.Add(new(null, null) { MonthlyMaintenance =
                new(MonthlyMaintenanceOperation.ConditionalSpeakerText, null,
                    K(current.ReadPersonIdentity(speakerPhysical), "speech_speaker_identity"), spoken) });
            return speaker;
        }
        bool IsPlayerReceiver()
        {
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current)
                ?? throw SalaryEmployedReportMissing("report_player_Force");
            return K(current.ReadForceNativeValidity(player), "report_player_validity") && ReceiverForce() == player;
        }
        ForceReference ReceiverForce()
        {
            int raw = K(current.ReadBaseForceSlot(receiver), "receiver_Base_Force");
            return current.ReadForceTableReference(raw < 52 ? raw : 0)
                ?? throw SalaryEmployedReportMissing("receiver_Force_lookup");
        }
        NativeMapSeasonDate Date() => state.NativeMapSeason?.ThirdDate
            ?? throw SalaryEmployedReportMissing("departure_current_date");
        TacticSourceSamplerWorld Sampler() => ReadArmyDestructionSampler(state, current)
            ?? throw SalaryEmployedReportMissing("current_World450_45C");
        int Day450() => Sampler().Day450;
        string Format(int root, HistoryMessageOperands operands)
        {
            var formatted = FormatNormalTurnReport(state, current, root, operands, latest => state = latest);
            state = formatted.State;
            return formatted.Text;
        }
        static T K<T>(T? value, string field) where T : struct => value ?? throw SalaryEmployedReportMissing(field);
    }

    private static InvalidDataException SalaryEmployedReportMissing(string field)
        => new("army.Turn_salary.employed-reports." + field);
}
