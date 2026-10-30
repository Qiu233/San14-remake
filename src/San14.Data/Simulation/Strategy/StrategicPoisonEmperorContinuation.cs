using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //This is the ordinary personnel-manager worker, outside CEventPlayback:
    //the independently constructed/retired history-manager+10 is0. The four
    //unresolved Emperor configuration values remain unknown and lazy.
    private static AutomaticEmperorCaptureInputs PoisonEmperorInputs
        => new(null, null, new(null, null, 0));

    /// <summary>Resume kind13 after its completed trial/history/Area prefix.
    /// Completed means rewards and the real return were scheduled, not that
    /// the personnel manager has already consumed this task's retirement.</summary>
    internal (GameState State, bool Completed) ResumeStrategicPoisonEmperorCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, AutomaticEmperorCaptureInputs? inputs = null)
    {
        var saved = state.StrategicPoisonEmperorContinuation ?? throw StrategyMissing("poison_resume_owner");
        var working = ResumeForceTransferEmperorCurrent(state, current, pool, inputs ?? PoisonEmperorInputs);
        if (working.AutomaticEmperorCapture is not null) return (working, false);
        var task = working.PersonnelCommands?.Strategies?.GetValueOrDefault(saved.TaskId)
            ?? throw StrategyMissing("poison_resume_actual_task");
        if (task.Kind != 13) throw StrategyMissing("poison_resume_task_kind");
        working = CompleteStrategicPoisonBaseTransferCurrent(working, current,
            new(BaseKind.City, saved.TargetCityPhysicalSlot));
        var other = current.ReadForceTableReference(saved.PreviousForcePhysicalSlot <= 51 ? saved.PreviousForcePhysicalSlot : 0)
            ?? throw StrategyMissing("poison_resume_previous_Force");
        if (StrategyNeed(current.ReadForceNativeValidity(other), "poison_resume_previous_Force_validity"))
            working = ForceRelationChangeCurrent.Apply(working, current, saved.OwnForcePhysicalSlot,
                saved.PreviousForcePhysicalSlot,
                () => -StrategicParameter(working, "resolution.poison-success-relation-loss", 10),
                key => StrategyNeed(ArmyDestructionParameterSeed.ReadCurrent(working, key), "poison_resume_relation_parameter"));
        working = ApplyStrategicCommandRewardsCurrent(working, current, saved.ExecutorPhysicalSlot, 13,
            saved.ResultCode == 0 ? 2 : saved.ResultCode == 2 ? 6 : 1);
        var district = current.ReadDistrictTableReference(task.SourceDistrictId <= 51 ? task.SourceDistrictId : 0)
            ?? throw StrategyMissing("poison_resume_return_District");
        working = PersonReturnCommands.ScheduleTalentCompletionReturnCurrent(working, current, task.Id,
            task.MemberIds, saved.CompletionDate, IsTalentPlayerPrimaryDistrict(working, current, district));
        working = working with { StrategicPoisonEmperorContinuation = null };
        return (BaseCapturePublicationSource.Publish(working, current, pool, projectOwnedRoles: true), true);
    }
}
