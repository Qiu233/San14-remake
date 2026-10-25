using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>2851B0's winner11 followed by its first live HomeArmy18
    /// traversal. This precedes the independent HomeArmy destruction pass and
    /// all Person temporaries. The valid winner supplies the shared packet's
    /// explicit Base Hex; both actions retain its independent Base4E operand.</summary>
    internal GameState QueueBaseCaptureInitialArmySpeechesCurrent(GameState state,
        SourceWorldContext current, BaseReference target, ArmyUnitReference winner,
        NativeMapSeasonDate? sourceDate)
    {
        int location = CaptureSpeechNeed(current.ReadBaseObjectLink(target), "Base4E");
        int? packetHex = null;
        if (CaptureSpeechNeed(current.ReadArmyUnitNativeValidity(winner), "winner_validity"))
        {
            packetHex = current.ReadBaseCurrentHexIndex(target);
            state = QueueBaseCaptureArmySpeechCurrent(state, current, winner, 11,
                location, sourceDate, packetHex);
        }
        var descriptor = current.ReadRegistryDescriptor(RegistryKind.Army)
            ?? throw CaptureSpeechMissing("manager60_descriptor");
        var node = current.ReadRegistryHead(descriptor) ?? throw CaptureSpeechMissing("manager60_head");
        while (node.Identity.HasValue)
        {
            var entry = current.ReadRegistryNodePayload(node) ?? throw CaptureSpeechMissing("manager60_payload");
            if (entry is not { Kind: RegistryKind.Army, PhysicalSlot: int physical })
                throw CaptureSpeechMissing("manager60_Army_pointer");
            if (!CaptureSpeechNeed(current.ReadOwnedArmyExclusion233560(entry), "HomeArmy233560"))
            {
                var army = new ArmyUnitReference(physical);
                int commander = CaptureSpeechCommander(current, army);
                //22A7A0 validates the Commander before reading Home E2;
                //1B4AB0 then independently checks the selected Army itself.
                if (CaptureSpeechNeed(current.ReadPersonNativeValidity(commander), "HomeArmy_Commander_validity") &&
                    CaptureSpeechNeed(current.ReadPersonFirstLocation(commander), "HomeArmy_CommanderE2") == location)
                    state = QueueBaseCaptureArmySpeechCurrent(state, current, army, 18,
                        location, sourceDate, packetHex);
            }
            node = current.ReadRegistryNodeNext(node) ?? throw CaptureSpeechMissing("manager60_next");
        }
        return state;
    }

    /// <summary>Actual1B4AB0 capture packet. A null explicit Hex represents
    /// the native48400 sentinel, including a neutral Gate's invalid winner0.
    /// Admission and App draws belong to the shared map-speech queue.</summary>
    internal GameState QueueBaseCaptureArmySpeechCurrent(GameState state,
        SourceWorldContext current, ArmyUnitReference army, int actionCode,
        int baseLocation, NativeMapSeasonDate? sourceDate, int? explicitHex = null)
    {
        if (actionCode is not (11 or 18)) throw CaptureSpeechMissing("action_code");
        if (!CaptureSpeechNeed(current.ReadArmyUnitNativeValidity(army), "packet_Army_validity")) return state;
        if (current.ReadOwnedPersonCommandManager() is null)
        {
            if (state.PersonnelCommands is null) throw CaptureSpeechMissing("personnel_manager");
            PersonReturnCommands.BindCurrent(state, current);
        }
        int commander = CaptureSpeechCommander(current, army);
        //The wrapper evaluates22D6C0 even when the packet already contains
        //an explicit Base Hex. Only the final48400 test chooses the fallback.
        var position = PersonReturnCommands.ResolvePersonPosition(state, current, commander,
            () => sourceDate ?? state.NativeMapSeason?.ThirdDate ?? throw CaptureSpeechMissing("source_date"));
        int officer = CaptureSpeechNeed(current.ReadPersonIdentity(commander), "Commander10");
        int physical = CaptureSpeechNeed(army.PhysicalSlot, "Army_physical");
        var lifecycle = state.ReadArmyLifecycle() ?? throw CaptureSpeechMissing("lifecycle");
        if ((uint)physical >= lifecycle.Slots.Count) throw CaptureSpeechMissing("Army_source_slot");
        var receiver = lifecycle.Receivers.Find(lifecycle.Slots[physical].ReceiverId)
            ?? throw CaptureSpeechMissing("Army_receiver");
        int id = receiver.ActiveUnitId ?? receiver.PendingImage?.Id ?? throw CaptureSpeechMissing("Army_identity");
        int hex = explicitHex is int supplied && supplied != 48400 ? supplied : position.PhysicalSlot ?? 48400;
        state = state with { PersonnelCommands = current.ReadOwnedPersonCommandManager()
            ?? throw CaptureSpeechMissing("personnel_publication") };
        return QueueArmyMapSpeechCurrent(state, current, id, officer, actionCode, hex,
            contextArgument: baseLocation);
    }

    private static int CaptureSpeechCommander(SourceWorldContext current, ArmyUnitReference army)
    {
        ushort raw = CaptureSpeechNeed(current.ReadArmyUnitCommanderId(army), "Commander12");
        var person = (raw <= 6000 ? current.ReadPersonLookupReference(raw) : current.ReadTransferPersonTableReference(0))
            ?? throw CaptureSpeechMissing("Commander_lookup");
        return CaptureSpeechNeed(person.PhysicalSlot, "Commander_physical");
    }
    private static T CaptureSpeechNeed<T>(T? value, string field) where T : struct => value ?? throw CaptureSpeechMissing(field);
    private static InvalidDataException CaptureSpeechMissing(string field) => new("Base-capture.Army-speech." + field);
}
