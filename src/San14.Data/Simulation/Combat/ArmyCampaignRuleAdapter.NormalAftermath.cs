using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    public NormalArmyPairAftermathResult TryApplyNativeNormalPairAftermath(GameState state,
        NormalArmyCombatPair pair) => TryApplyNativeNormalPairAftermath(state, pair, current => Context(current));

    internal NormalArmyPairAftermathResult TryApplyNativeNormalPairAftermath(GameState state,
        NormalArmyCombatPair pair,
        Func<SourceWorldContext, TacticRemakeCurrentContext> createCurrent)
    {
        var definitions = CaptureCurrentDefinitions(state);
        return definitions is null ? new(NormalArmyPairAftermathOutcome.CurrentInputsUnresolved,
            state, null, null, [], "current.stage8_definitions") :
            NormalArmyPairAftermath.TryApply(state, pair, definitions, createCurrent(definitions), _normalAftermathInputs);
    }
}

public sealed partial class NormalArmyCombatCurrentPhase
{
    /// <summary>The reached pair's numeric174EC0 tail. It does not repeat
    /// damage or admission: a queued victim remains a current receiver here.
    /// The caller owns stage7 and ordered advancement within this same phase.</summary>
    public NormalArmyPairAftermathResult TryApplyPairAftermath(GameState current, int pairIndex)
    {
        if (pairIndex < 0 || pairIndex >= plan.OrderedPairs.Length) throw new ArgumentOutOfRangeException(nameof(pairIndex));
        if (currentApplication is null || current.Date != clock.AnchorDate || current.NativeClock != clock ||
            current.NativeMapSeason != season)
            return new(NormalArmyPairAftermathOutcome.CurrentInputsUnresolved, current,
                null, null, [], "current.stage8_phase_owner");
        if (operationCurrent is not null && captureDefinitions(current) is null)
            return new(NormalArmyPairAftermathOutcome.CurrentInputsUnresolved, current,
                null, null, [], "current.stage8_definitions");
        return operationCurrent is null
            ? currentApplication.TryApplyNativeNormalPairAftermath(current, plan.OrderedPairs[pairIndex], createContext)
            : currentApplication.ApplyBorrowedNormalAftermath(current, operationCurrent, createContext(operationCurrent), plan.OrderedPairs[pairIndex]);
    }
}
