using San14.Core;

namespace San14.Data;

public enum NormalArmyNonlethalPairOutcome { CurrentInputsUnresolved, NativeSkip, Committed, DestructionRequired }
public sealed record NormalArmyNonlethalPairResult(NormalArmyNonlethalPairOutcome Outcome,
    GameState State, NormalArmyPairApplicationPrefixResult? Prefix,
    NormalArmyPairAftermathResult? Aftermath, string? MissingOwner);
public sealed record NormalArmyNonlethalPhaseResult(bool Complete, GameState State,
    NormalArmyNonlethalPairResult[] OrderedPairs, int? BlockedPairIndex, string? MissingOwner);

public sealed partial class NormalArmyCombatCurrentPhase
{
    /// <summary>One nonlethal ordinary Army pair: prefix exactly once,
    /// current Force/personality then174EC0. This client uses typed reports;
    /// it has no native display receivers. The source killed/frame24 branch
    /// must use the deferred destruction continuation, never a premature reset.</summary>
    public NormalArmyNonlethalPairResult TryCompleteNonlethalPair(GameState current, int pairIndex,
        INormalArmyForceResultDefinitionQueries forceDefinitions)
    {
        ArgumentNullException.ThrowIfNull(forceDefinitions);
        var prefix = TryApplyPairApplicationPrefix(current, pairIndex, forceDefinitions);
        if (prefix.Outcome == NormalArmyPairApplicationPrefixOutcome.CurrentInputsUnresolved)
            return Blocked(NormalArmyNonlethalPairOutcome.CurrentInputsUnresolved, prefix.MissingPhase);
        if (prefix.Outcome == NormalArmyPairApplicationPrefixOutcome.NativePairSkipped)
            return new(NormalArmyNonlethalPairOutcome.NativeSkip, current, prefix, null, null);
        if (prefix.Outcome == NormalArmyPairApplicationPrefixOutcome.ZeroTroopsCleanupRequired)
            return Blocked(NormalArmyNonlethalPairOutcome.DestructionRequired, "current.full_deferred_destruction");
        if (prefix.Application?.Receipt is not { } application ||
            application.Killed is not false && prefix.Application.Outcome != NormalArmyApplicationPrefixOutcome.NativeTargetSkipped)
            return Blocked(NormalArmyNonlethalPairOutcome.CurrentInputsUnresolved, "current.nonlethal_application_return");
        //TC174A30 does not consume frame24 when178EC0 returned false.
        var aftermath = TryApplyPairAftermath(prefix.State, pairIndex);
        if (aftermath.Outcome == NormalArmyPairAftermathOutcome.CurrentInputsUnresolved)
            return new(NormalArmyNonlethalPairOutcome.CurrentInputsUnresolved, current,
                prefix with { State = current }, aftermath with { State = current }, aftermath.MissingOwner);
        var pair = plan.OrderedPairs[pairIndex];
        //The native report accumulates word20, not the20+22 requested debit
        //or the capped ready troop loss. Keep it in its separate typed channel.
        GameState after = AppendOrdinaryReport(aftermath.State, pair, application.Request.FrameWord20After);
        return new(NormalArmyNonlethalPairOutcome.Committed, after, prefix, aftermath, null);

        NormalArmyNonlethalPairResult Blocked(NormalArmyNonlethalPairOutcome outcome, string? owner) =>
            new(outcome, current, prefix with { State = current }, null, owner);
    }

    /// <summary>A private ordered Army-only phase transaction. Later pairs
    /// read actual earlier writes while descriptor/cache remains captured.
    /// A reached lethal or unknown pair rolls back the entire finite phase;
    /// no phase cursor/token or middle-of-pair save is introduced.</summary>
    public NormalArmyNonlethalPhaseResult TryCompleteNonlethalPhase(GameState current,
        INormalArmyForceResultDefinitionQueries forceDefinitions)
    {
        ArgumentNullException.ThrowIfNull(forceDefinitions);
        if (current.Date != clock.AnchorDate || current.NativeClock != clock || current.NativeMapSeason != season ||
            current.ContentHashes.GetValueOrDefault(plan.Moment.SourceScenarioUri) != plan.Moment.SourceScenarioSha256)
            return new(false, current, [], null, "current.phase_owner");
        GameState working = current;
        var pairs = new List<NormalArmyNonlethalPairResult>();
        for (int index = 0; index < plan.OrderedPairs.Length; index++)
        {
            var next = TryCompleteNonlethalPair(working, index, forceDefinitions);
            if (next.Outcome is NormalArmyNonlethalPairOutcome.CurrentInputsUnresolved or NormalArmyNonlethalPairOutcome.DestructionRequired)
                return new(false, current, [], index, next.MissingOwner);
            working = next.State; pairs.Add(next);
        }
        return new(true, working, pairs.ToArray(), null, null);
    }
}
