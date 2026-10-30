using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The reached monthly salary release tail. The salary caller owns
    /// the actual sorted release list, retained report speaker, current world
    /// and one ordered effect batch, including recursion into its Gates.</summary>
    internal GameState CompleteNativeSalaryCaptiveReleasesCurrent(GameState state,
        SourceWorldContext current, BaseReference receiver, HexListHandle releaseCaptives,
        ref TransferPersonReference? reportSpeaker, List<CampaignEffectRequest> effects)
    {
        var head = current.ReadRegistryHead(new(releaseCaptives.Identity))
            ?? throw SalaryCaptiveMissing("release_list_head");
        if (!head.Identity.HasValue) return state;

        //233A00 tests the real current player before looking up the receiver's
        //Force. This report precedes every individual notification1 release.
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current)
            ?? throw SalaryCaptiveMissing("report_player_Force");
        if (K(current.ReadForceNativeValidity(player), "report_player_validity"))
        {
            int rawForce = K(current.ReadBaseForceSlot(receiver), "report_Base_Force");
            var force = current.ReadForceTableReference(rawForce < 52 ? rawForce : 0)
                ?? throw SalaryCaptiveMissing("report_Force_pointer");
            if (force == player)
            {
                var speaker = reportSpeaker;
                if (speaker?.PhysicalSlot is not int retainedPhysical ||
                    !K(current.ReadPersonNativeValidity(retainedPhysical), "retained_speaker_validity"))
                {
                    var district = current.ReadBaseDistrictReference(receiver)
                        ?? throw SalaryCaptiveMissing("report_Base_District");
                    if (K(current.ReadDistrictNativeValidity(district), "report_District_validity"))
                    {
                        var pool = current.ReadOwnedCurrentListPool()
                            ?? throw SalaryCaptiveMissing("report_current_pool");
                        speaker = new PersonDeathHistorySelection(state, current, pool, SearchAbilities(current),
                            _governorInputs, (id, effect) => _baseCapacityInputs.ReadPanelModifier(state, id, effect))
                            .Select(district);
                    }
                    else speaker = current.ReadPersonLookupReference(5012)
                        ?? throw SalaryCaptiveMissing("report_default_Person5012");
                    reportSpeaker = speaker;
                }

                effects.Add(new(null, null) { MonthlyMaintenance =
                    new(MonthlyMaintenanceOperation.ControllerQueueEvent, 6, null, null) });
                int speakerPhysical = K(speaker?.PhysicalSlot, "report_speaker_pointer");
                ushort speakerId = K(current.ReadPersonIdentity(speakerPhysical), "report_speaker_identity");
                ushort ruler = K(current.ReadMoraleWaveRuler22EAC0(force), "report_ruler");
                ushort location = K(current.ReadBaseObjectLink(receiver), "report_Base4E");
                string spoken = Format(0x5E3B, new(Person0: speakerId, Person1: ruler, Base0: location));
                string main = Format(0x5E40, new(Base0: K(current.ReadBaseObjectLink(receiver), "history_Base4E")));
                //The source rereads the retained Person identity after both
                //scratch messages. History and the conditional speech use it.
                speakerId = K(current.ReadPersonIdentity(speakerPhysical), "history_speaker_identity");
                int day = Day450();
                int hex = ReadReportBaseMarker(current, receiver, SalaryCaptiveMissing);
                int districtId = K(current.ReadBaseDistrictSlot(receiver), "history_Base_virtual78");
                state = WriteHistory(state, current, new(5, districtId, hex, day, main, spoken,
                    speakerId, MarkBit0: 1, MarkBit4: 1), effects);
                effects.Add(new(null, null) { MonthlyMaintenance =
                    new(MonthlyMaintenanceOperation.ConditionalSpeakerText, null,
                        K(current.ReadPersonIdentity(speakerPhysical), "speech_speaker_identity"), spoken) });
            }
        }

        //Do not replace this actual list with all matching captives or release
        //during billing. Debit and the salary report have already completed.
        var node = head;
        while (node.Identity.HasValue)
        {
            var value = current.ReadRegistryNodePayload(node)
                ?? throw SalaryCaptiveMissing("release_list_payload");
            if (value.Kind != RegistryKind.Person) throw SalaryCaptiveMissing("release_list_kind");
            int person = K(value.PhysicalSlot, "release_Person_pointer");
            state = ApplyCaptiveReleaseCurrent(state, current, person,
                state.NativeMapSeason?.ThirdDate, Day450, effects, null, reportRelease: true);
            node = current.ReadRegistryNodeNext(node) ?? throw SalaryCaptiveMissing("release_list_next");
        }
        return state;

        int Day450() => (ReadArmyDestructionSampler(state, current)
            ?? throw SalaryCaptiveMissing("current_World450_45C")).Day450;
        string Format(int root, HistoryMessageOperands operands)
        {
            var formatted = FormatNormalTurnReport(state, current, root, operands, latest => state = latest);
            state = formatted.State;
            return formatted.Text;
        }
        static T K<T>(T? value, string field) where T : struct => value ?? throw SalaryCaptiveMissing(field);
    }

    private static InvalidDataException SalaryCaptiveMissing(string field)
        => new("army.Turn_salary.captive-release." + field);
}
