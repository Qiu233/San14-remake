using San14.Core;

namespace San14.Data;

public sealed partial class TacticRemakeCurrentContext
{
    internal BattleDamageSourceParameters? ReadFieldObjectDamageParameters(GameState state) =>
        Bound(state) ? _normalDamage?.CurrentParameters(state) : null;

    /// <summary>Fresh target-only producer for TC191920. This context must
    /// belong to the caller's current operation; no initial-world capture,
    /// opponent placeholder, pair count or normal-combat cache is consulted.</summary>
    public BattleRemakeCurrentFieldObjectTargetReceipt? ResolveFieldObjectTarget(
        GameState state, UnitState target, BattleRemakeCurrentParameterSeed battleSeed,
        BattleRemakeCurrentRegionParameterSeed regionSeed)
    {
        if (!Bound(state) || _abilities is null || ArmyValid(state, target) != true) return null;
        var factors = new TacticRemakeCurrentBattleFactors(this, _definitions, regionSeed, Revision);
        var queries = new TacticRemakeCurrentBattleQueries(this, _definitions, Revision, factors);
        //The explicit1 is191920's actual argument to2A9180. The entry avoids
        //169E20's accepted-primary count, cache inheritance and effect55.
        var common = queries.ReadCommonQueriesForDescriptor(state, target, 1);
        if (common is null) return null;
        var attributes = new BattleRemakeCurrentArmyAttributes(battleSeed, _definitions, _abilities, queries, Revision);
        return attributes.ResolveFieldObjectTarget(state, target, common);
    }
}
