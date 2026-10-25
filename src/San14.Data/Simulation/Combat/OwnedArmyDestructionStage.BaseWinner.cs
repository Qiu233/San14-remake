using San14.Core;

namespace San14.Data;

public sealed partial class OwnedArmyDestructionStage
{
    //259360 param3 is a real Base pointer. An ordinary City/Gate winner uses its
    //Force/resources; it does not become a synthetic Army or injury Person.
    private static BaseReference ResolveWinnerBase(GameState state, SourceWorldContext current,
        SettlementBase? target, bool winnerArmyValid)
    {
        if (target is null) return new(BaseKind.City, null);
        if (winnerArmyValid) throw Missing("branch.simultaneous_Army_and_Base_winners");
        if (target.Kind is not (SettlementBaseKind.City or SettlementBaseKind.Gate))
            throw Missing("branch.nonordinary_Base_winner");
        var basis = ArmyDestructionQueue.ResolveBaseReference(state, current, target);
        _ = Known(current.ReadBaseNativeValidity(basis), "winner_Base_validity");
        return basis;
    }
}
