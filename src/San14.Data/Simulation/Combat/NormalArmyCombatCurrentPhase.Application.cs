using San14.Core;

namespace San14.Data;

public enum NormalArmyPairApplicationPrefixOutcome
{
    CurrentInputsUnresolved, AwaitingApplicationTail, ZeroTroopsCleanupRequired, NativePairSkipped, AwaitingPlayerDecision
}

public sealed record NormalArmyPairApplicationPrefixResult(
    NormalArmyPairApplicationPrefixOutcome Outcome, GameState State,
    AutomaticTacticPrefixResult? Automatic,
    NormalArmyCombatTacticFrame22Receipt? Frame22,
    NormalArmyApplicationPrefixResult? Application, string? MissingPhase)
{
    public NormalArmyForceResultApplication? ForceResult { get; init; }
    public NormalArmyPersonalityReactionReceipt? PersonalityReaction { get; init; }
    public NormalArmyPersonalityApplication? PersonalityApplication { get; init; }
    internal GameState? PendingCleanupState { get; init; }
    internal AutomaticTacticExecutionContinuation? PendingAutomaticExecution { get; init; }
    internal bool AutomaticActivationCompleted { get; init; }
}

public sealed partial class NormalArmyCombatCurrentPhase
{
    /// <summary>Same169A40 fresh pair: keep stage5 word20, perform automatic
    /// non-damage effects, then the178C20 troop/wound/morale prefix. This is
    /// deliberately not a stage13 resolution: Force/personality/report/cleanup
    /// and174C20 remain required before a driver may advance to the next pair.</summary>
    public NormalArmyPairApplicationPrefixResult TryApplyPairApplicationPrefix(GameState current, int pairIndex,
        INormalArmyForceResultDefinitionQueries? forceDefinitions = null)
    {
        if (currentApplication is null) return Unknown("actual-cycle-application-owner");
        var admission = ResolvePairAdmission(current, pairIndex);
        if (admission.Outcome == NormalArmyPairAdmissionOutcome.CurrentInputsUnresolved)
            return Unknown(admission.SourceBranch);
        if (admission.Outcome == NormalArmyPairAdmissionOutcome.NativeSkip)
            return new(NormalArmyPairApplicationPrefixOutcome.NativePairSkipped, current,
                null, null, null, null);
        var automatic = TryApplyAutomaticTacticPrefix(current, pairIndex);
        if (automatic.Outcome == AutomaticTacticPrefixOutcome.AwaitingPlayerDecision)
            return new(NormalArmyPairApplicationPrefixOutcome.AwaitingPlayerDecision, automatic.State,
                automatic, null, null, null) { PendingAutomaticExecution = automatic.PendingAutomaticExecution };
        if (automatic.Outcome == AutomaticTacticPrefixOutcome.CurrentInputsUnresolved ||
            automatic.Selection is not { } original)
            return new(NormalArmyPairApplicationPrefixOutcome.CurrentInputsUnresolved, current,
                automatic, null, null, "automatic-prefix");
        ushort frame20 = original.Stage5.Damage.NativeFrameWord20;
        NormalArmyCombatTacticFrame22Receipt frame22;
        if (automatic.ActualFrame22 is { } borrowedFrame22)
            frame22 = borrowedFrame22;
        else if (automatic.Outcome == AutomaticTacticPrefixOutcome.EffectsTailWaitingCommitted)
        {
            //172C10 clears B8; complete0/15/19/20/23 under the already proved
            //zero-chain neither inserts damage nodes nor moves/removes Armies.
            //Consume its actual known-empty Army tree; no-match preserves the
            //fresh169C31..169C52 word22=0. D8/frame24 completeness is not granted.
            if (original.Selection?.SelectedCandidate?.Program.Instructions.Any(instruction =>
                    instruction.Opcode is not (0 or 15 or 19 or 20 or 23)) != false)
                return Unknown("automatic-damage-tree");
            frame22 = NormalArmyCombatTacticFrame22.ConsumeArmyTree(0,
                checked((ushort)original.Stage5.Pair.Target.NativePoolSlot), [])!;
        }
        else frame22 = new(0, 0, false, 0); //Actual fresh pair and native no-activation path.
        return ApplyPairAfterAutomatic(current, automatic.State, pairIndex, frame20, frame22,
            automatic, automatic.NativeFrame24Activated == true, forceDefinitions);

        NormalArmyPairApplicationPrefixResult Unknown(string phase) =>
            new(NormalArmyPairApplicationPrefixOutcome.CurrentInputsUnresolved, current, null, null, null, phase);
    }

    private NormalArmyPairApplicationPrefixResult ApplyPairAfterAutomatic(GameState current, GameState afterAutomatic,
        int pairIndex, ushort frame20, NormalArmyCombatTacticFrame22Receipt frame22,
        AutomaticTacticPrefixResult? automatic, bool activated,
        INormalArmyForceResultDefinitionQueries? forceDefinitions)
    {
        var pair = plan.OrderedPairs[pairIndex];
        if (currentApplication is null) return Unknown("actual-cycle-application-owner");
        var request = NormalArmyCombatApplicationSourceMath.BuildNormalRequestedLoss(frame20, frame22.Word22After);
        TacticRemakeCurrentContext? ReadCurrent(GameState reached) =>
            captureDefinitions(reached) is { } definitions ? createContext(definitions) : null;
        var applied = operationCurrent is null
            ? currentApplication.TryApplyNativeNormalApplicationPrefix(afterAutomatic, pair, request, ReadCurrent)
            : currentApplication.TryApplyBorrowedNormalApplicationPrefix(afterAutomatic, pair, request, ReadCurrent, operationCurrent);
        if (applied.Outcome == NormalArmyApplicationPrefixOutcome.CurrentInputsUnresolved)
            return new(NormalArmyPairApplicationPrefixOutcome.CurrentInputsUnresolved, current,
                null, null, null, applied.MissingPhase);
        if (applied.Outcome == NormalArmyApplicationPrefixOutcome.ZeroTroopsCleanupRequired)
        {
            GameState transient = applied.PendingCleanupState ?? throw new InvalidDataException("Actual zero-troop prefix lacks its operation continuation.");
            NormalArmyForceResultApplication? force = null;
            if (forceDefinitions is not null && applied.Receipt?.TroopDebit is { } killedDebit)
            {
                var attacker = transient.Units[pair.Attacker.ArmyId];
                var target = transient.Units[pair.Target.ArmyId];
                force = operationCurrent is null
                    ? currentApplication.ApplyForceResultDuringArmyOperation(transient, attacker.ForceId,
                        target.ForceId, killedDebit.AppliedLoss, applied.Receipt.Killed!.Value, forceDefinitions)
                    : currentApplication.ApplyBorrowedNormalForceResult(transient, operationCurrent, attacker.ForceId,
                        target.ForceId, killedDebit.AppliedLoss, applied.Receipt.Killed!.Value, forceDefinitions);
                if (force.Outcome == NormalArmyForceResultOutcome.CurrentInputsUnresolved)
                    return Unknown("normal-force-callback");
                transient = force.State;
            }
            // A killed commander does not take the nonlethal category1E branch.
            return new(NormalArmyPairApplicationPrefixOutcome.ZeroTroopsCleanupRequired, current,
                automatic, frame22, applied with { State = current }, applied.MissingPhase)
                { ForceResult = force, PendingCleanupState = transient, AutomaticActivationCompleted = activated };
        }
        if (forceDefinitions is not null && applied.Receipt?.TroopDebit is { } debit)
        {
            //1791E0..17921B, ordinary kind27/null second Force. A missing
            //current15F or reached160 registry result rolls back both prefixes.
            var attacker = applied.State.Units[pair.Attacker.ArmyId];
            var target = applied.State.Units[pair.Target.ArmyId];
            var force = operationCurrent is null
                ? currentApplication.ApplyForceResultDuringArmyOperation(applied.State, attacker.ForceId,
                    target.ForceId, debit.AppliedLoss, applied.Receipt.Killed!.Value, forceDefinitions)
                : currentApplication.ApplyBorrowedNormalForceResult(applied.State, operationCurrent, attacker.ForceId,
                    target.ForceId, debit.AppliedLoss, applied.Receipt.Killed!.Value, forceDefinitions);
            if (force.Outcome == NormalArmyForceResultOutcome.CurrentInputsUnresolved)
                return Unknown("normal-force-callback");
            var personalityContext = operationCurrent is null ? null : ReadCurrent(force.State);
            if (operationCurrent is not null && personalityContext is null) return Unknown("normal-personality-current");
            var personality = operationCurrent is null
                ? currentApplication.TryApplyNativeNormalPersonalityReaction(force.State, target.Id)
                : currentApplication.ApplyBorrowedNormalPersonality(force.State, operationCurrent, personalityContext!, target.Id);
            if (personality.Outcome == NormalArmyPersonalityApplicationOutcome.CurrentInputsUnresolved)
                return Unknown("normal-personality-reaction");
            return new(NormalArmyPairApplicationPrefixOutcome.AwaitingApplicationTail, personality.State,
                automatic, frame22, applied, null) { ForceResult = force, PersonalityReaction = personality.Receipt,
                    PersonalityApplication = personality, AutomaticActivationCompleted = activated };
        }
        return new(NormalArmyPairApplicationPrefixOutcome.AwaitingApplicationTail, applied.State,
            automatic, frame22, applied, null) { AutomaticActivationCompleted = activated };

        NormalArmyPairApplicationPrefixResult Unknown(string phase) =>
            new(NormalArmyPairApplicationPrefixOutcome.CurrentInputsUnresolved, current, null, null, null, phase);
    }
}
