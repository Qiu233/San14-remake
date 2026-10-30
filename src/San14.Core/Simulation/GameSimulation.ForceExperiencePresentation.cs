namespace San14.Core;

public sealed partial class GameSimulation
{
    public bool IsForcePolicyEditorOwner(GameState expectedOwner, long batchSequence, int requestIndex, int forceId)
    {
        if (_progress is not null || expectedOwner.Phase is not (TurnPhase.Strategy or TurnPhase.Reporting) ||
            !ReferenceEquals(_normalPeriod?.Candidate ?? State, expectedOwner)) return false;
        var batch = expectedOwner.CampaignEffectRequests?.PendingBatches.FirstOrDefault(row => row.Sequence == batchSequence);
        return batch is { Source: CampaignEffectSource.ForcePolicyLevelUp } &&
            (uint)requestIndex < (uint)batch.Requests.Length && batch.Requests[requestIndex].ForcePolicyLevelUp is
                { Stage: ForcePolicyLevelUpStage.PolicyEditor } scene && scene.ForceId == forceId;
    }

    /// <summary>The existing governance commands inside their actual saved
    /// level-up editor. Ordinary strategy commands keep their usual phase gate.</summary>
    public CommandResult ApplyPolicyLevelUpOrder(GameState expectedOwner, long batchSequence, int requestIndex,
        GameOrder order, IOrderRules rules, CommandContext context, IOrderAuthority authority,
        CampaignAppRandomState? appRandomSnapshot = null)
    {
        if (order is not (SetGovernanceRosterOrder or CompleteGovernanceNarrationOrder or ExpandGovernanceDepartmentOrder) ||
            !IsForcePolicyEditorOwner(expectedOwner, batchSequence, requestIndex, order.ForceId))
            return CommandResult.Reject("The policy level-up editor no longer owns this command.", "governance.policy_scene");
        var error = GameEngine.CheckPresentationOrder(expectedOwner, order, context, authority);
        if (error is not null) return error;
        GameState source = appRandomSnapshot is null ? expectedOwner : expectedOwner with { CampaignAppRandom32 = appRandomSnapshot };
        var result = GameEngine.ApplyOwned(expectedOwner, GameStateCopy.Copy(source), order, rules, context);
        if (result.Accepted)
        {
            if (_normalPeriod is { } pending) _normalPeriod = pending with { Candidate = result.State! };
            else State = result.State!;
        }
        return result;
    }

    /// <summary>Called after the real header has finished its accumulated
    /// experience transition. This is the source boundary which may enqueue
    /// a doctrine-level scene; merely granting experience does not enqueue it.</summary>
    public bool TryCompleteForceExperiencePresentation(GameState expectedOwner, long batchSequence,
        int requestIndex, int previousDisplayLevel, IForceExperiencePresentationRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return TryUpdateForcePresentation(expectedOwner, batchSequence, requestIndex, (owner, source, request) =>
        {
            if (source is not (CampaignEffectSource.TalentCommands or CampaignEffectSource.PersonnelAdministrationCommands or CampaignEffectSource.MonthlyMaintenance) || request.TalentForceExperiencePresentation is not
                { Operation: TalentForceExperiencePresentationOperation.ExperienceGain } experience) return null;
            return (rules.CompleteExperiencePresentation(owner, experience, previousDisplayLevel), null);
        });
    }

    /// <summary>Only actual announcement, policy-choice or editor-return inputs
    /// advance this saved scene. A requested editor is never an automatic Yes.</summary>
    public bool TryAdvanceForcePolicyLevelUp(GameState expectedOwner, long batchSequence, int requestIndex,
        ForcePolicyLevelUpInput input, IForceExperiencePresentationRules rules)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(rules);
        return TryUpdateForcePresentation(expectedOwner, batchSequence, requestIndex, (owner, source, request) =>
        {
            if (source != CampaignEffectSource.ForcePolicyLevelUp || request.ForcePolicyLevelUp is not { } scene) return null;
            var result = rules.AdvancePolicyLevelUp(owner, scene, input);
            return (result.State, result.Pending is null ? null : request with { ForcePolicyLevelUp = result.Pending });
        });
    }

    private bool TryUpdateForcePresentation(GameState expectedOwner, long batchSequence, int requestIndex,
        Func<GameState, CampaignEffectSource, CampaignEffectRequest, (GameState State, CampaignEffectRequest? Pending)?> apply)
    {
        if (_progress is not null) return false;
        var pending = _normalPeriod;
        var owner = pending?.Candidate ?? State;
        if (!ReferenceEquals(owner, expectedOwner) || owner.CampaignEffectRequests is not { } effects) return false;
        int index = Array.FindIndex(effects.PendingBatches, batch => batch.Sequence == batchSequence);
        if (index < 0 || (uint)requestIndex >= (uint)effects.PendingBatches[index].Requests.Length) return false;
        var original = effects.PendingBatches[index];
        var request = original.Requests[requestIndex];
        var result = apply(owner, original.Source, request);
        if (result is not { } transition) return false;
        var updatedEffects = transition.State.CampaignEffectRequests
            ?? throw new InvalidDataException("Force presentation lost its request owner.");
        index = Array.FindIndex(updatedEffects.PendingBatches, batch => batch.Sequence == batchSequence);
        if (index < 0 || (uint)requestIndex >= (uint)updatedEffects.PendingBatches[index].Requests.Length ||
            !ReferenceEquals(updatedEffects.PendingBatches[index].Requests[requestIndex], request))
            throw new InvalidDataException("Force presentation changed the reached request before publication.");
        var reached = updatedEffects.PendingBatches[index];
        var batches = updatedEffects.PendingBatches.ToList();
        if (transition.Pending is { } replacement)
        {
            var requests = (CampaignEffectRequest[])reached.Requests.Clone();
            requests[requestIndex] = replacement;
            batches[index] = reached with { Requests = requests };
        }
        else if (reached.Requests.Length == 1) batches.RemoveAt(index);
        else
        {
            var requests = new CampaignEffectRequest[reached.Requests.Length - 1];
            Array.Copy(reached.Requests, 0, requests, 0, requestIndex);
            Array.Copy(reached.Requests, requestIndex + 1, requests, requestIndex, requests.Length - requestIndex);
            batches[index] = reached with { Requests = requests };
        }
        var completed = transition.State with
        {
            CampaignEffectRequests = updatedEffects with { PendingBatches = batches.ToArray() }
        };
        if (pending is not null) _normalPeriod = pending with { Candidate = completed };
        else State = completed;
        return true;
    }
}
