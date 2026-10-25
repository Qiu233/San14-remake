using San14.Core;

namespace San14.Data;

public sealed partial class NormalArmyCombatCurrentPhase
{
    /// <summary>Private stage13 continuation. A reached zero Army stays in
    /// the current pointer table until real stages19/26 consume its request.
    /// This result must remain inside the existing whole-block transaction.</summary>
    internal GameState? TryCompleteOrdinaryPhase(GameState current,
        INormalArmyForceResultDefinitionQueries forceDefinitions)
    {
        if (current.Date != clock.AnchorDate || current.NativeClock != clock || current.NativeMapSeason != season ||
            current.ContentHashes.GetValueOrDefault(plan.Moment.SourceScenarioUri) != plan.Moment.SourceScenarioSha256)
            return null;
        GameState working = current;
        for (int index = 0; index < plan.OrderedPairs.Length; index++)
        {
            var completed = TryCompleteOrdinaryPair(working, index, forceDefinitions);
            if (completed.Outcome is NormalArmyNonlethalPairOutcome.CurrentInputsUnresolved or
                NormalArmyNonlethalPairOutcome.DestructionRequired or
                NormalArmyNonlethalPairOutcome.AwaitingPlayerDecision) return null;
            working = completed.State;
        }
        return working;
    }

    /// <summary>One reached ordinary pair, including1745B0 admission. A full
    /// queue settles this defeat immediately; a player decision returns the
    /// already applied prefix to its enclosing phase for durable continuation.</summary>
    internal NormalArmyNonlethalPairResult TryCompleteOrdinaryPair(GameState current, int pairIndex,
        INormalArmyForceResultDefinitionQueries forceDefinitions)
    {
        var completed = TryCompleteNonlethalPair(current, pairIndex, forceDefinitions);
        if (completed.Outcome != NormalArmyNonlethalPairOutcome.DestructionRequired) return completed;
        if (completed.Prefix is not { PendingCleanupState: { } draft, Application.Receipt.Killed: true } prefix)
            return completed;
        if (prefix.Automatic?.Outcome != AutomaticTacticPrefixOutcome.NativeSkip)
        {
            if (prefix.Automatic?.NativeFrame24Activated != true) return completed;
            //A real completed172EB0 activation set frame24. A true178EC0
            //return now reaches16D9F0 busy98 before the actual1745B0 queue.
            draft = ArmyCampaignRuleAdapter.MarkBorrowedAutomaticTacticFatalFeedback(draft);
        }
        if (captureDefinitions(draft) is not { } definitions)
            return Unresolved("current.ordinary_destruction_definitions");
        var pair = plan.OrderedPairs[pairIndex];
        var admission = OwnedArmyDestructionCurrent.AdmitOrdinaryArmy1745B0(draft, definitions,
            pair.Target.NativePoolSlot, pair.Attacker.NativePoolSlot, null);
        GameState queued;
        if (admission.RequiresImmediateSettlement)
        {
            if (currentApplication is null || operationCurrent is null || operationEffects is null || operationSampler is null)
                return Unresolved("current.ordinary_destruction_immediate_operation_owner");
            var immediate = currentApplication.ApplyNormalCombatImmediateDefeatCurrent(draft, operationCurrent,
                new(pair.Target.ArmyId, pair.Attacker.ArmyId, null),
                operationSampler.Read(draft) ?? throw new InvalidDataException("current.ordinary_destruction_immediate_sampler"),
                plan.Moment.WorldThirdDate, plan.Moment.CalendarCycleOrdinal, operationEffects);
            if (immediate.ArmyDefeatContinuation is not null)
                return new(NormalArmyNonlethalPairOutcome.AwaitingPlayerDecision, immediate, prefix, null, null);
            queued = immediate;
        }
        else if (admission.IsPending && admission.State is { } pending)
            queued = pending;
        else
            return Unresolved(admission.UnresolvedOwner ?? (admission.RequiresImmediateSettlement
                ? "current.ordinary_destruction_immediate_259360"
                : "current.ordinary_destruction_admission"));
        var aftermath = TryApplyPairAftermath(queued, pairIndex);
        if (aftermath.Outcome == NormalArmyPairAftermathOutcome.CurrentInputsUnresolved)
            return Unresolved(aftermath.MissingOwner ?? "current.ordinary_destruction_aftermath");
        GameState after = AppendOrdinaryReport(aftermath.State, pair, prefix.Application!.Receipt!.Request.FrameWord20After);
        return new(NormalArmyNonlethalPairOutcome.Committed, after, prefix, aftermath, null);

        NormalArmyNonlethalPairResult Unresolved(string owner) =>
            new(NormalArmyNonlethalPairOutcome.CurrentInputsUnresolved, current, prefix, null, owner);
    }

    internal GameState CompletePairAfterImmediateDefeat(GameState state, int pairIndex, ushort word20)
    {
        var aftermath = TryApplyPairAftermath(state, pairIndex);
        if (aftermath.Outcome == NormalArmyPairAftermathOutcome.CurrentInputsUnresolved)
            throw new InvalidDataException(aftermath.MissingOwner ?? "current.immediate_destruction_aftermath");
        return AppendOrdinaryReport(aftermath.State, plan.OrderedPairs[pairIndex], word20);
    }

    internal TacticSourceSamplerWorld? ReadOrdinaryCycleSampler(GameState current) =>
        captureDefinitions(current) is { } definitions ? createContext(definitions).ReadCurrentSamplerWorld(current) : null;

    private GameState AppendOrdinaryReport(GameState after, NormalArmyCombatPair pair, ushort word20)
    {
        var report = new NativeSubdayReport("normal_army_damage", clock.UpdateOrdinal, pair.Attacker.ArmyId,
            pair.Attacker.NativeForce10, pair.Target.NativePoolSlot, word20);
        return after with { NativeSubdayReports = [.. after.NativeSubdayReports ?? [], report] };
    }
}
