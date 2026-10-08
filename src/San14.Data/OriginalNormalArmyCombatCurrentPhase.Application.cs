using San14.Core;

namespace San14.Data;

public enum OriginalNormalArmyPairApplicationPrefixOutcome
{
    CurrentInputsUnresolved, AwaitingApplicationTail, ZeroTroopsCleanupRequired, NativePairSkipped
}

public sealed record OriginalNormalArmyPairApplicationPrefixResult(
    OriginalNormalArmyPairApplicationPrefixOutcome Outcome, GameState State,
    OriginalAutomaticTacticPrefixResult? Automatic,
    OriginalNormalArmyCombatTacticFrame22Receipt? Frame22,
    OriginalNormalArmyApplicationPrefixResult? Application, string? MissingPhase)
{
    public OriginalNormalArmyForceResultApplication? ForceResult { get; init; }
    public OriginalNormalArmyPersonalityReactionReceipt? PersonalityReaction { get; init; }
    public OriginalNormalArmyPersonalityApplication? PersonalityApplication { get; init; }
    internal GameState? PendingCleanupState { get; init; }
}

public sealed partial class OriginalNormalArmyCombatCurrentPhase
{
    /// <summary>Same169A40 fresh pair: keep stage5 word20, perform automatic
    /// non-damage effects, then the178C20 troop/wound/morale prefix. This is
    /// deliberately not a stage13 resolution: Force/personality/report/cleanup
    /// and174C20 remain required before a driver may advance to the next pair.</summary>
    public OriginalNormalArmyPairApplicationPrefixResult TryApplyPairApplicationPrefix(GameState current, int pairIndex,
        IOriginalNormalArmyForceResultDefinitionQueries? forceDefinitions = null)
    {
        if (currentApplication is null) return Unknown("actual-cycle-application-owner");
        var admission = ResolvePairAdmission(current, pairIndex);
        if (admission.Outcome == OriginalNormalArmyPairAdmissionOutcome.CurrentInputsUnresolved)
            return Unknown(admission.SourceBranch);
        if (admission.Outcome == OriginalNormalArmyPairAdmissionOutcome.NativeSkip)
            return new(OriginalNormalArmyPairApplicationPrefixOutcome.NativePairSkipped, current,
                null, null, null, null);
        var automatic = TryApplyAutomaticTacticPrefix(current, pairIndex);
        if (automatic.Outcome == OriginalAutomaticTacticPrefixOutcome.CurrentInputsUnresolved ||
            automatic.Selection is not { } original)
            return Unknown("automatic-prefix");
        ushort frame20 = original.Stage5.Damage.NativeFrameWord20;
        OriginalNormalArmyCombatTacticFrame22Receipt frame22;
        if (automatic.Outcome == OriginalAutomaticTacticPrefixOutcome.EffectsTailWaitingCommitted)
        {
            //172C10 clears B8; complete0/15/19/20/23 under the already proved
            //zero-chain neither inserts damage nodes nor moves/removes Armies.
            //Consume its actual known-empty Army tree; no-match preserves the
            //fresh169C31..169C52 word22=0. D8/frame24 completeness is not granted.
            if (original.Selection?.SelectedCandidate?.Program.Instructions.Any(instruction =>
                    instruction.Opcode is not (0 or 15 or 19 or 20 or 23)) != false)
                return Unknown("automatic-damage-tree");
            frame22 = OriginalNormalArmyCombatTacticFrame22.ConsumeArmyTree(0,
                checked((ushort)original.Stage5.Pair.Target.NativePoolSlot), [])!;
        }
        else frame22 = new(0, 0, false, 0); //Actual fresh pair and native no-activation path.
        var request = OriginalNormalArmyCombatApplicationSourceMath.BuildNormalRequestedLoss(frame20, frame22.Word22After);
        var applied = currentApplication.TryApplyNativeNormalApplicationPrefix(automatic.State,
            original.Stage5.Pair, request, state => captureDefinitions(state) is { } definitions ? createContext(definitions) : null);
        if (applied.Outcome == OriginalNormalArmyApplicationPrefixOutcome.CurrentInputsUnresolved)
            return new(OriginalNormalArmyPairApplicationPrefixOutcome.CurrentInputsUnresolved, current,
                null, null, null, applied.MissingPhase);
        if (applied.Outcome == OriginalNormalArmyApplicationPrefixOutcome.ZeroTroopsCleanupRequired)
        {
            GameState transient = applied.PendingCleanupState ?? throw new InvalidDataException("Actual zero-troop prefix lacks its operation continuation.");
            OriginalNormalArmyForceResultApplication? force = null;
            if (forceDefinitions is not null && applied.Receipt?.TroopDebit is { } killedDebit)
            {
                var attacker = transient.Units[original.Stage5.Pair.Attacker.ArmyId];
                var target = transient.Units[original.Stage5.Pair.Target.ArmyId];
                force = currentApplication.TryApplyNativeForceResult(transient, attacker.ForceId,
                    target.ForceId, killedDebit.AppliedLoss, applied.Receipt.Killed!.Value, forceDefinitions);
                if (force.Outcome == OriginalNormalArmyForceResultOutcome.CurrentInputsUnresolved)
                    return Unknown("normal-force-callback");
                transient = force.State;
            }
            // A killed commander does not take the nonlethal category1E branch.
            return new(OriginalNormalArmyPairApplicationPrefixOutcome.ZeroTroopsCleanupRequired, current,
                automatic, frame22, applied with { State = current }, applied.MissingPhase)
                { ForceResult = force, PendingCleanupState = transient };
        }
        if (forceDefinitions is not null && applied.Receipt?.TroopDebit is { } debit)
        {
            //1791E0..17921B, ordinary kind27/null second Force. A missing
            //current15F or reached160 registry result rolls back both prefixes.
            var attacker = applied.State.Units[original.Stage5.Pair.Attacker.ArmyId];
            var target = applied.State.Units[original.Stage5.Pair.Target.ArmyId];
            var force = currentApplication.TryApplyNativeForceResult(applied.State, attacker.ForceId,
                target.ForceId, debit.AppliedLoss, applied.Receipt.Killed!.Value, forceDefinitions);
            if (force.Outcome == OriginalNormalArmyForceResultOutcome.CurrentInputsUnresolved)
                return Unknown("normal-force-callback");
            var personality = currentApplication.TryApplyNativeNormalPersonalityReaction(force.State, target.Id);
            if (personality.Outcome == OriginalNormalArmyPersonalityApplicationOutcome.CurrentInputsUnresolved)
                return Unknown("normal-personality-reaction");
            return new(OriginalNormalArmyPairApplicationPrefixOutcome.AwaitingApplicationTail, personality.State,
                automatic, frame22, applied, null) { ForceResult = force, PersonalityReaction = personality.Receipt,
                    PersonalityApplication = personality };
        }
        return new(OriginalNormalArmyPairApplicationPrefixOutcome.AwaitingApplicationTail, applied.State,
            automatic, frame22, applied, null);

        OriginalNormalArmyPairApplicationPrefixResult Unknown(string phase) =>
            new(OriginalNormalArmyPairApplicationPrefixOutcome.CurrentInputsUnresolved, current, null, null, null, phase);
    }
}
