using San14.Core;

namespace San14.Data;

public sealed partial class OriginalOwnedArmyDestructionStage
{
    //259360 param3 is a real Base pointer. An ordinary City winner uses its
    //Force/resources; it does not become a synthetic Army or injury Person.
    private static OriginalBaseReference ResolveWinnerBase(GameState state, OriginalEventInitializationContext current,
        SettlementBase? target, bool winnerArmyValid)
    {
        if (target is null) return new(OriginalBaseKind.City, null);
        if (winnerArmyValid) throw Missing("branch.simultaneous_Army_and_Base_winners");
        if (target.Kind != SettlementBaseKind.City) throw Missing("branch.nonordinary_City_winner");
        var city = OriginalArmyDestructionQueue.ResolveBaseReference(state, current, target);
        _ = Known(current.ReadBaseNativeValidity(city), "winner_City_validity");
        return city;
    }
}
