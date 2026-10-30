using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>1920D0's actual kind8 tail calls1B4AB0(Army,9). Admission
    /// precedes text/voice preparation; no view or played speech is implied.</summary>
    internal GameState QueueFacilityMoraleSpeechCurrent(GameState state, SourceWorldContext current,
        ArmyUnitReference army, NativeArmyProgressStageMoment moment)
    {
        if (!MapSpeechNeed(current.ReadArmyUnitNativeValidity(army), "facility_morale_Army_validity")) return state;
        int slot = MapSpeechNeed(army.PhysicalSlot, "facility_morale_Army_pointer");
        var arena = state.ReadArmyLifecycle() ?? throw MapSpeechMissing("facility_morale_Army_owner");
        var receiver = arena.Receivers.Find(arena.Slots[slot].ReceiverId)
            ?? throw MapSpeechMissing("facility_morale_Army_receiver");
        int armyId = receiver.ActiveUnitId ?? receiver.PendingImage?.Id
            ?? throw MapSpeechMissing("facility_morale_Army_identity");
        ushort commander = MapSpeechNeed(current.ReadArmyUnitCommanderId(army), "facility_morale_Commander12");
        var personReference = commander <= 6000 ? current.ReadPersonLookupReference(commander)
            : current.ReadTransferPersonTableReference(0);
        int person = MapSpeechNeed(personReference?.PhysicalSlot, "facility_morale_Person_pointer");
        int officer = MapSpeechNeed(current.ReadPersonIdentity(person), "facility_morale_Person10");
        int hex = PersonReturnCommands.ResolvePersonPosition(state, current, person,
            moment.PreWriterThirdDate).PhysicalSlot ?? 48400;
        return QueueArmyMapSpeechCurrent(state, current, armyId, officer, 9, hex);
    }
}
