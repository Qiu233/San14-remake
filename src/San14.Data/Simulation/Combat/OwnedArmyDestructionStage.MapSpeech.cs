using San14.Core;

namespace San14.Data;

public sealed partial class OwnedArmyDestructionStage
{
    //1B4AB0 constructs its Army/Person packet before1B4720 tests queue
    //capacity/admission. Even14's explicit original-Hex operand still follows
    //the real22D6C0 Person-position query; no pending70 image is fabricated.
    private GameState QueueDestructionArmySpeechCurrent(GameState state, SourceWorldContext current,
        ArmyUnitReference army, int actionCode, NativeMapSeasonDate? sourceDate, int? originalHex = null)
    {
        if (!Known(current.ReadArmyUnitNativeValidity(army), "map_speech_Army_validity")) return state;
        int commander = CommanderPhysical(current, army);
        var position = PersonReturnCommands.ResolvePersonPosition(state, current, commander,
            () => sourceDate ?? throw Missing("current.map_speech_actual_date"));
        if (current.ReadOwnedPersonCommandManager() is { } commands)
            state = state with { PersonnelCommands = commands };
        int personId = Known(current.ReadPersonIdentity(commander), "map_speech_Person10");
        int hex = originalHex ?? (position.PhysicalSlot.HasValue
            ? Known(current.ReadHexPhysicalSlot(position), "map_speech_Hex22DEF0") : 48400);
        int physical = Known(army.PhysicalSlot, "map_speech_Army_pointer");
        var lifecycle = state.ReadArmyLifecycle() ?? throw Missing("current.map_speech_lifecycle");
        var receiver = lifecycle.Receivers.Find(lifecycle.Slots[physical].ReceiverId)
            ?? throw Missing("current.map_speech_Army_owner");
        int armyId = receiver.ActiveUnitId ?? receiver.PendingImage?.Id
            ?? throw Missing("current.map_speech_Army_identity");
        //The14 packet is zero-initialized apart from its original-Hex word.
        //The10 call passes an actual null packet (TC25AA6C XOR R9D,R9D).
        return package.QueueArmyMapSpeechCurrent(state, current, armyId, personId, actionCode, hex,
            contextArgument: actionCode == 14 ? 0 : null);
    }
}
