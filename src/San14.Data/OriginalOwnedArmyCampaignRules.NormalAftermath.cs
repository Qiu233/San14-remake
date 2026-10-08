using San14.Core;

namespace San14.Data;

public sealed partial class OriginalOwnedArmyCampaignRules
{
    public OriginalNormalArmyPairAftermathResult TryApplyNativeNormalPairAftermath(GameState state,
        OriginalNormalArmyCombatPair pair) => TryApplyNativeNormalPairAftermath(state, pair, current => Context(current));

    internal OriginalNormalArmyPairAftermathResult TryApplyNativeNormalPairAftermath(GameState state,
        OriginalNormalArmyCombatPair pair,
        Func<OriginalEventInitializationContext, OriginalTacticRemakeCurrentContext> createCurrent)
    {
        var definitions = CaptureCurrentDefinitions(state);
        return definitions is null ? new(OriginalNormalArmyPairAftermathOutcome.CurrentInputsUnresolved,
            state, null, null, [], "current.stage8_definitions") :
            OriginalNormalArmyPairAftermath.TryApply(state, pair, definitions, createCurrent(definitions), _normalAftermathInputs);
    }
}

public sealed partial class OriginalNormalArmyCombatCurrentPhase
{
    /// <summary>The reached pair's numeric174EC0 tail. It does not repeat
    /// damage or admission: a queued victim remains a current receiver here.
    /// The caller owns stage7 and ordered advancement within this same phase.</summary>
    public OriginalNormalArmyPairAftermathResult TryApplyPairAftermath(GameState current, int pairIndex)
    {
        if (pairIndex < 0 || pairIndex >= plan.OrderedPairs.Length) throw new ArgumentOutOfRangeException(nameof(pairIndex));
        if (currentApplication is null || current.Date != clock.AnchorDate || current.NativeClock != clock ||
            current.NativeMapSeason != season)
            return new(OriginalNormalArmyPairAftermathOutcome.CurrentInputsUnresolved, current,
                null, null, [], "current.stage8_phase_owner");
        return currentApplication.TryApplyNativeNormalPairAftermath(current, plan.OrderedPairs[pairIndex], createContext);
    }
}
