using San14.Core;

namespace San14.Data;

public enum OriginalNormalArmyNonlethalPairOutcome { CurrentInputsUnresolved, NativeSkip, Committed, DestructionRequired }
public sealed record OriginalNormalArmyNonlethalPairResult(OriginalNormalArmyNonlethalPairOutcome Outcome,
    GameState State, OriginalNormalArmyPairApplicationPrefixResult? Prefix,
    OriginalNormalArmyPairAftermathResult? Aftermath, string? MissingOwner);
public sealed record OriginalNormalArmyNonlethalPhaseResult(bool Complete, GameState State,
    OriginalNormalArmyNonlethalPairResult[] OrderedPairs, int? BlockedPairIndex, string? MissingOwner);

public sealed partial class OriginalNormalArmyCombatCurrentPhase
{
    /// <summary>One nonlethal ordinary Army pair: prefix exactly once,
    /// current Force/personality then174EC0. This client uses typed reports;
    /// it has no native display receivers. The source killed/frame24 branch
    /// must use the deferred destruction continuation, never a premature reset.</summary>
    public OriginalNormalArmyNonlethalPairResult TryCompleteNonlethalPair(GameState current, int pairIndex,
        IOriginalNormalArmyForceResultDefinitionQueries forceDefinitions)
    {
        ArgumentNullException.ThrowIfNull(forceDefinitions);
        var prefix = TryApplyPairApplicationPrefix(current, pairIndex, forceDefinitions);
        if (prefix.Outcome == OriginalNormalArmyPairApplicationPrefixOutcome.CurrentInputsUnresolved)
            return Blocked(OriginalNormalArmyNonlethalPairOutcome.CurrentInputsUnresolved, prefix.MissingPhase);
        if (prefix.Outcome == OriginalNormalArmyPairApplicationPrefixOutcome.NativePairSkipped)
            return new(OriginalNormalArmyNonlethalPairOutcome.NativeSkip, current, prefix, null, null);
        if (prefix.Outcome == OriginalNormalArmyPairApplicationPrefixOutcome.ZeroTroopsCleanupRequired)
            return Blocked(OriginalNormalArmyNonlethalPairOutcome.DestructionRequired, "current.full_deferred_destruction");
        if (prefix.Application?.Receipt is not { } application ||
            application.Killed is not false && prefix.Application.Outcome != OriginalNormalArmyApplicationPrefixOutcome.NativeTargetSkipped)
            return Blocked(OriginalNormalArmyNonlethalPairOutcome.CurrentInputsUnresolved, "current.nonlethal_application_return");
        //TC174A30 does not consume frame24 when178EC0 returned false.
        var aftermath = TryApplyPairAftermath(prefix.State, pairIndex);
        if (aftermath.Outcome == OriginalNormalArmyPairAftermathOutcome.CurrentInputsUnresolved)
            return new(OriginalNormalArmyNonlethalPairOutcome.CurrentInputsUnresolved, current,
                prefix with { State = current }, aftermath with { State = current }, aftermath.MissingOwner);
        var pair = plan.OrderedPairs[pairIndex];
        //The native report accumulates word20, not the20+22 requested debit
        //or the capped ready troop loss. Keep it in its separate typed channel.
        GameState after = AppendOrdinaryReport(aftermath.State, pair, application.Request.FrameWord20After);
        NativeSubdayStateValidation.Validate(after);
        return new(OriginalNormalArmyNonlethalPairOutcome.Committed, after, prefix, aftermath, null);

        OriginalNormalArmyNonlethalPairResult Blocked(OriginalNormalArmyNonlethalPairOutcome outcome, string? owner) =>
            new(outcome, current, prefix with { State = current }, null, owner);
    }

    /// <summary>A private ordered Army-only phase transaction. Later pairs
    /// read actual earlier writes while descriptor/cache remains captured.
    /// A reached lethal or unknown pair rolls back the entire finite phase;
    /// no phase cursor/token or middle-of-pair save is introduced.</summary>
    public OriginalNormalArmyNonlethalPhaseResult TryCompleteNonlethalPhase(GameState current,
        IOriginalNormalArmyForceResultDefinitionQueries forceDefinitions)
    {
        ArgumentNullException.ThrowIfNull(forceDefinitions);
        if (current.Date != clock.AnchorDate || current.NativeClock != clock || current.NativeMapSeason != season ||
            current.ContentHashes.GetValueOrDefault(plan.Moment.SourceScenarioUri) != plan.Moment.SourceScenarioSha256)
            return new(false, current, [], null, "current.phase_owner");
        GameState working = current;
        var pairs = new List<OriginalNormalArmyNonlethalPairResult>();
        for (int index = 0; index < plan.OrderedPairs.Length; index++)
        {
            var next = TryCompleteNonlethalPair(working, index, forceDefinitions);
            if (next.Outcome is OriginalNormalArmyNonlethalPairOutcome.CurrentInputsUnresolved or OriginalNormalArmyNonlethalPairOutcome.DestructionRequired)
                return new(false, current, [], index, next.MissingOwner);
            working = next.State; pairs.Add(next);
        }
        return new(true, working, pairs.ToArray(), null, null);
    }
}
