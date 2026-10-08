namespace San14.Core;

public static class PlayableOfficerAvailability
{
    //Original custody status6 overrides the coarse first-playable Officer role.
    //Legacy states without native fields retain their existing eligibility.
    public static bool IsCaptive(OfficerState officer) => officer.NativeAbilityRuntime?.Status11E == 6;

    public static bool IsSourceBusy(GameState state, OfficerState officer)
    {
        if (IsCaptive(officer)) return true;
        if (state.NativePersonReturnCommands is not { } manager || state.NativeArmyLifecycle is not { } lifecycle ||
            lifecycle.Auxiliaries?.AssociatedCommand18Descriptor?.Identity is not int associated) return false;
        //Follow manager18's live links. Free nodes retain old payloads, and a
        //receiver may remain allocated after its association has been removed.
        for (int? at = lifecycle.Pool.Descriptors[associated].Head; at is int nodeId;
             at = lifecycle.Pool.Nodes[nodeId].Next)
        {
            var payload = lifecycle.Pool.Nodes[nodeId].Payload;
            if (payload is not { Kind: "LinkedEntity", Identity: int receiver } ||
                !manager.Commands.TryGetValue(receiver, out var command) || command.Kind != "return") continue;
            var raw = command.RawFields;
            if (raw[0x58] is not byte a || raw[0x59] is not byte b || raw[0x5A] is not byte c || raw[0x5B] is not byte d ||
                (a == 255 && b == 255 && c == 255 && d == 255) || command.Members20.Identity is not int memberList) continue;
            for (int? member = manager.MembersPool.Descriptors[memberList].Head; member is int memberId;
                 member = manager.MembersPool.Nodes[memberId].Next)
                if (manager.MembersPool.Nodes[memberId].Payload is { Kind: "MemberId32", Identity: int person } &&
                    person == officer.Id) return true;
        }
        return false;
    }
}
