using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>29EBB0's actual ending-state admission. Its only business
    /// operand is the normalized event900 flag; the parent application state
    /// is supplied by the attached host, never serialized as a native handle.
    /// This neither decides victory nor acknowledges an ending or profile write.</summary>
    internal CampaignEndingAdmission? ReadCampaignEndingAdmissionCurrent(GameState state,
        SourceWorldContext current, bool fromEvent900, bool parentStatePresent)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(current);
        //3343E0 precedes all World/observer and parent-state queries.
        bool returning = ReadApplicationReturnToTitlePending()
            ?? throw EndingAdmissionMissing("Application_return_to_title3343E0");
        if (returning) return null;

        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current)
            ?? throw EndingAdmissionMissing("current_player32EFA0");
        bool playerValid = current.ReadForceNativeValidity(player)
            ?? throw EndingAdmissionMissing("current_player_native_validity");
        if (!playerValid)
        {
            //333F00 is reached only after an invalid current player. An
            //observer does not acquire a fabricated valid player or a win.
            uint flags = current.ReadWorldTransferServiceFlags()
                ?? throw EndingAdmissionMissing("observer_World16A8");
            if ((flags & 0x100) == 0) return null;
        }

        //571630 reads the real current parent state. A known absent host
        //does not enqueue CEndingState even though the Turn already stopped.
        return parentStatePresent ? new(fromEvent900) : null;
    }

    private static InvalidDataException EndingAdmissionMissing(string owner)
        => new("campaign.ending.admission." + owner);
}
