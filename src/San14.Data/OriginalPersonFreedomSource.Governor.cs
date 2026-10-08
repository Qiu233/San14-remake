using San14.Core;

namespace San14.Data;

public sealed partial class OriginalOwnedArmyCampaignRules
{
    /// <summary>265250's reached22ACF0→282C10→26D090 tail, after the
    /// complete free Person fields. Uses this operation's actual current
    /// candidates and temporary pool; no full District reselection.</summary>
    internal OriginalArmyReturnGovernorPacket ApplyPersonFreedomGovernorCurrent(GameState state,
        OriginalEventInitializationContext current, OriginalCurrentListPool pool, OriginalBaseReference oldHome)
    {
        var scoped = new FreedomGovernorPoolQueries(new PersonReturnGovernorQueries(state, _governorInputs, current), pool);
        var prepared = OriginalArmyReturnGovernorUpdater.PrepareCurrent(oldHome, current, scoped);
        var packet = prepared.Packet ?? throw new InvalidDataException(prepared.UnresolvedOwner ?? "current.freedom_282C10");
        OriginalArmyReturnCommanderHomeUpdater.ApplyGovernorOwned(current, packet);
        return packet;
    }

    private sealed class FreedomGovernorPoolQueries(IOriginalArmyReturnGovernorCurrentQueries queries,
        OriginalCurrentListPool pool) : IOriginalArmyReturnGovernorCurrentQueries
    {
        public OriginalArmyReturnGovernorComparatorFields? ReadCurrentComparatorFields(int person) => queries.ReadCurrentComparatorFields(person);
        public int? ReadCurrentAssociatedVirtual98(OriginalPersonLinkedEntityReference reference) => queries.ReadCurrentAssociatedVirtual98(reference);
        public OriginalArmyReturnGovernorPoolCapacity? ReadCurrentTemporaryPoolCapacity() => pool.ArmyReturnCapacity;
    }
}
