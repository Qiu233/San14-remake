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
                NormalArmyNonlethalPairOutcome.DestructionRequired) return null;
            working = completed.State;
        }
        return working;
    }

    /// <summary>One reached ordinary pair, including its deferred lethal
    /// tail. The prefix runs once; a queued zero-troop receiver remains owned
    /// by this private phase until the real defeat/retirement stages consume it.</summary>
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
        //A full secondary queue returns0 and calls259360 immediately.
        //Keep that reached branch unresolved instead of pretending it queued.
        if (!admission.IsPending || admission.State is not { } queued)
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

    internal TacticSourceSamplerWorld? ReadOrdinaryCycleSampler(GameState current) =>
        captureDefinitions(current) is { } definitions ? createContext(definitions).ReadCurrentSamplerWorld(current) : null;

    private GameState AppendOrdinaryReport(GameState after, NormalArmyCombatPair pair, ushort word20)
    {
        var report = new NativeSubdayReport("normal_army_damage", clock.UpdateOrdinal, pair.Attacker.ArmyId,
            pair.Attacker.NativeForce10, pair.Target.NativePoolSlot, word20);
        return after with { NativeSubdayReports = [.. after.NativeSubdayReports ?? [], report] };
    }
}
