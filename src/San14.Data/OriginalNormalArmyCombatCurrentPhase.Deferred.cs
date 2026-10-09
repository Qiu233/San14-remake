using San14.Core;

namespace San14.Data;

public sealed partial class OriginalNormalArmyCombatCurrentPhase
{
    /// <summary>Private stage13 continuation. A reached zero Army stays in
    /// the current pointer table until real stages19/26 consume its request.
    /// This result must remain inside the existing whole-block transaction.</summary>
    internal GameState? TryCompleteOrdinaryPhase(GameState current,
        IOriginalNormalArmyForceResultDefinitionQueries forceDefinitions)
    {
        if (current.Date != clock.AnchorDate || current.NativeClock != clock || current.NativeMapSeason != season ||
            current.ContentHashes.GetValueOrDefault(plan.Moment.SourceScenarioUri) != plan.Moment.SourceScenarioSha256)
            return null;
        GameState working = current;
        for (int index = 0; index < plan.OrderedPairs.Length; index++)
        {
            //This calls the automatic/application/Force prefix exactly once.
            var next = TryCompleteNonlethalPair(working, index, forceDefinitions);
            if (next.Outcome == OriginalNormalArmyNonlethalPairOutcome.CurrentInputsUnresolved) return null;
            if (next.Outcome != OriginalNormalArmyNonlethalPairOutcome.DestructionRequired)
            { working = next.State; continue; }
            if (next.Prefix is not { PendingCleanupState: { } draft, Application.Receipt.Killed: true } prefix ||
                prefix.Automatic?.Outcome != OriginalAutomaticTacticPrefixOutcome.NativeSkip)
                return null;
            //169CE0's fresh frame24 is0; no reached automatic activation wrote
            //it in this branch. The manager98 killed/frame24 write is skipped.
            //An activation with unknown D8/frame24 still requires its owner.
            if (captureDefinitions(draft) is not { } definitions) return null;
            var pair = plan.OrderedPairs[index];
            var admission = OriginalOwnedArmyDestructionCurrent.AdmitOrdinaryArmy1745B0(draft, definitions,
                pair.Target.NativePoolSlot, pair.Attacker.NativePoolSlot, null);
            //A full secondary queue returns0 and calls259360 immediately.
            //Do not replace that source branch with a convenient early reset.
            if (!admission.IsPending || admission.State is not { } queued) return null;
            var aftermath = TryApplyPairAftermath(queued, index);
            if (aftermath.Outcome == OriginalNormalArmyPairAftermathOutcome.CurrentInputsUnresolved) return null;
            working = AppendOrdinaryReport(aftermath.State, pair, prefix.Application!.Receipt!.Request.FrameWord20After);
        }
        return working;
    }

    internal OriginalTacticSourceSamplerWorld? ReadOrdinaryCycleSampler(GameState current) =>
        captureDefinitions(current) is { } definitions ? createContext(definitions).ReadCurrentSamplerWorld(current) : null;

    private GameState AppendOrdinaryReport(GameState after, OriginalNormalArmyCombatPair pair, ushort word20)
    {
        var report = new NativeSubdayReport("normal_army_damage", clock.UpdateOrdinal, pair.Attacker.ArmyId,
            pair.Attacker.NativeForce10, pair.Target.NativePoolSlot, word20);
        return after with { NativeSubdayReports = [.. after.NativeSubdayReports ?? [], report] };
    }
}
