using San14.Core;

namespace San14.Data;

public sealed partial class OriginalOwnedArmyCampaignRules
{
    /// <summary>265250's reached22ACF0→282C10→26D090 tail, after the
    /// complete free Person fields. Uses this operation's actual current
    /// candidates; no full District reselection.</summary>
    internal OriginalArmyReturnGovernorPacket ApplyPersonFreedomGovernorCurrent(GameState state,
        OriginalEventInitializationContext current, OriginalBaseReference oldHome)
    {
        var queries = new PersonReturnGovernorQueries(state, _governorInputs, current);
        var prepared = OriginalArmyReturnGovernorUpdater.PrepareCurrent(oldHome, current, queries);
        var packet = prepared.Packet ?? throw new InvalidDataException(prepared.UnresolvedOwner ?? "current.freedom_282C10");
        OriginalArmyReturnCommanderHomeUpdater.ApplyGovernorOwned(current, packet);
        return packet;
    }

}
