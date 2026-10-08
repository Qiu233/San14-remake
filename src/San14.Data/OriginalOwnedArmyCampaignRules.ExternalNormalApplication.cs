using San14.Core;

namespace San14.Data;

public enum OriginalExternalArmyNormalApplicationOutcome
{
    CurrentInputsUnresolved, NativeTargetSkipped, CommittedNonfatal, DestructionRequired
}

public sealed record OriginalExternalArmyNormalApplicationReceipt(
    int AttackerId, int TargetId, int ExternalRequestedLoss,
    OriginalNormalArmyApplicationBodyReceipt Body,
    OriginalNormalArmyForceResultApplication? ForceResult,
    OriginalNormalArmyPersonalityApplication? Personality, string Revision);

public sealed record OriginalExternalArmyNormalApplicationResult(
    OriginalExternalArmyNormalApplicationOutcome Outcome, GameState State,
    OriginalExternalArmyNormalApplicationReceipt? Receipt, string? MissingOwner);

public sealed partial class OriginalOwnedArmyCampaignRules
{
    /// <summary>Finite nonfatal178EC0 body and its Force/personality tail for
    /// an external ordinary damage request. It neither generates source pairs,
    /// frame words, automatic damage trees nor174EC0 regional aftermath. The
    /// caller supplies an actual current sampler boundary, not a guessed10h.</summary>
    public OriginalExternalArmyNormalApplicationResult TryApplyExternalNormalArmyApplication(GameState input,
        int attackerId, int targetId, int requestedLoss,
        IOriginalNormalArmyForceResultDefinitionQueries? forceDefinitions = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        var application = TryApplyNativeNormalApplicationBody(input, attackerId, targetId, requestedLoss,
            state => CaptureCurrentDefinitions(state) is { } definitions ? Context(definitions) : null);
        if (application.Outcome == OriginalNormalArmyApplicationPrefixOutcome.CurrentInputsUnresolved)
            return Unknown(application.MissingPhase);
        if (application.Outcome == OriginalNormalArmyApplicationPrefixOutcome.ZeroTroopsCleanupRequired)
            return new(OriginalExternalArmyNormalApplicationOutcome.DestructionRequired, input, null,
                "external-normal.actual_deferred_destruction_required");
        if (application.Receipt is not { } receipt) return Unknown("external-normal.application_receipt");
        if (application.Outcome == OriginalNormalArmyApplicationPrefixOutcome.NativeTargetSkipped)
            return new(OriginalExternalArmyNormalApplicationOutcome.NativeTargetSkipped, application.State,
                new(attackerId, targetId, requestedLoss, receipt, null, null, Revision), null);
        if (receipt.TroopDebit is not { } debit || receipt.Killed != false ||
            !application.State.Units.TryGetValue(attackerId, out var attacker) ||
            !application.State.Units.TryGetValue(targetId, out var target))
            return Unknown("external-normal.nonfatal_quantity_return");
        //The actual Base virtualB0 registry reads the output of all earlier
        //writes, not a definitions closure captured at contact entry.
        forceDefinitions ??= new OriginalNormalArmyForceResultDefinitions(CaptureCurrentDefinitions(application.State));
        var force = TryApplyNativeForceResult(application.State, attacker.ForceId, target.ForceId,
            debit.AppliedLoss, false, forceDefinitions);
        if (force.Outcome == OriginalNormalArmyForceResultOutcome.CurrentInputsUnresolved)
            return Unknown("external-normal.Force_result_definition");
        var personality = TryApplyNativeNormalPersonalityReaction(force.State, targetId);
        if (personality.Outcome == OriginalNormalArmyPersonalityApplicationOutcome.CurrentInputsUnresolved)
            return Unknown($"external-normal.personality:{personality.MissingOwner}");
        return new(OriginalExternalArmyNormalApplicationOutcome.CommittedNonfatal, personality.State,
            new(attackerId, targetId, requestedLoss, receipt, force, personality, Revision), null);

        OriginalExternalArmyNormalApplicationResult Unknown(string? owner) => new(
            OriginalExternalArmyNormalApplicationOutcome.CurrentInputsUnresolved, input, null, owner);
    }
}
