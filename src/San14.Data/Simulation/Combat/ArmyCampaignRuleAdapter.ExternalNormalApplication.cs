using San14.Core;

namespace San14.Data;

public enum ExternalArmyNormalApplicationOutcome
{
    CurrentInputsUnresolved, NativeTargetSkipped, CommittedNonfatal, DestructionRequired
}

public sealed record ExternalArmyNormalApplicationReceipt(
    int AttackerId, int TargetId, int ExternalRequestedLoss,
    NormalArmyApplicationBodyReceipt Body,
    NormalArmyForceResultApplication? ForceResult,
    NormalArmyPersonalityApplication? Personality, string Revision);

public sealed record ExternalArmyNormalApplicationResult(
    ExternalArmyNormalApplicationOutcome Outcome, GameState State,
    ExternalArmyNormalApplicationReceipt? Receipt, string? MissingOwner);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Finite nonfatal178EC0 body and its Force/personality tail for
    /// an external ordinary damage request. It neither generates source pairs,
    /// frame words, automatic damage trees nor174EC0 regional aftermath. The
    /// caller supplies an actual current sampler boundary, not a guessed10h.</summary>
    public ExternalArmyNormalApplicationResult TryApplyExternalNormalArmyApplication(GameState input,
        int attackerId, int targetId, int requestedLoss,
        INormalArmyForceResultDefinitionQueries? forceDefinitions = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        var application = TryApplyNativeNormalApplicationBody(input, attackerId, targetId, requestedLoss,
            state => CaptureCurrentDefinitions(state) is { } definitions ? Context(definitions) : null);
        if (application.Outcome == NormalArmyApplicationPrefixOutcome.CurrentInputsUnresolved)
            return Unknown(application.MissingPhase);
        if (application.Outcome == NormalArmyApplicationPrefixOutcome.ZeroTroopsCleanupRequired)
            return new(ExternalArmyNormalApplicationOutcome.DestructionRequired, input, null,
                "external-normal.actual_deferred_destruction_required");
        if (application.Receipt is not { } receipt) return Unknown("external-normal.application_receipt");
        if (application.Outcome == NormalArmyApplicationPrefixOutcome.NativeTargetSkipped)
            return new(ExternalArmyNormalApplicationOutcome.NativeTargetSkipped, application.State,
                new(attackerId, targetId, requestedLoss, receipt, null, null, Revision), null);
        if (receipt.TroopDebit is not { } debit || receipt.Killed != false ||
            !application.State.Units.TryGetValue(attackerId, out var attacker) ||
            !application.State.Units.TryGetValue(targetId, out var target))
            return Unknown("external-normal.nonfatal_quantity_return");
        //The actual Base virtualB0 registry reads the output of all earlier
        //writes, not a definitions closure captured at contact entry.
        forceDefinitions ??= new NormalArmyForceResultDefinitions(CaptureCurrentDefinitions(application.State));
        var force = ApplyForceResultDuringArmyOperation(application.State, attacker.ForceId, target.ForceId,
            debit.AppliedLoss, false, forceDefinitions);
        if (force.Outcome == NormalArmyForceResultOutcome.CurrentInputsUnresolved)
            return Unknown("external-normal.Force_result_definition");
        var personality = TryApplyNativeNormalPersonalityReaction(force.State, targetId);
        if (personality.Outcome == NormalArmyPersonalityApplicationOutcome.CurrentInputsUnresolved)
            return Unknown($"external-normal.personality:{personality.MissingOwner}");
        return new(ExternalArmyNormalApplicationOutcome.CommittedNonfatal, personality.State,
            new(attackerId, targetId, requestedLoss, receipt, force, personality, Revision), null);

        ExternalArmyNormalApplicationResult Unknown(string? owner) => new(
            ExternalArmyNormalApplicationOutcome.CurrentInputsUnresolved, input, null, owner);
    }
}
