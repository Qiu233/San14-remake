using San14.Core;

namespace San14.Data;

/// <summary>Remaining complete current pipelines, separate from facts already
/// constructed and owned by GameState. Missing methods stay unknown.</summary>
public interface ITacticRemakeUnresolvedQueries
{
    int? ReadCommanderCapacity(GameState state, OfficerState commander) => null;
    int? ReadForcePolicyEffect(GameState state, ForceState force, int effect) => null;
    int? ReadForceEffect10(GameState state, ForceState force) => null;
    TacticRemakeChainResolution? ResolveChain(GameState state, UnitState caster, int tacticId) => null;
}

public sealed record TacticRemakeChainResolution(int ChainCount,
    int SumOfNativeWaitingContributions, ulong RandomState);
