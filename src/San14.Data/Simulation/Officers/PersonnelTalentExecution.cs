using San14.Core;

namespace San14.Data;

internal sealed record PersonnelTalentAdvanceResult(GameState State, bool Retire);

internal interface IPersonnelTalentExecution
{
    PersonnelTalentAdvanceResult AdvanceTalentCurrent(GameState state, SourceWorldContext current,
        PersonnelTalentTask task, NativeMapSeasonDate date);
}

public sealed partial class ArmyCampaignRuleAdapter : IPersonnelTalentExecution
{
    PersonnelTalentAdvanceResult IPersonnelTalentExecution.AdvanceTalentCurrent(GameState state,
        SourceWorldContext current, PersonnelTalentTask task, NativeMapSeasonDate date)
    {
        if (date.Hour != 22) return new(state, false);
        //1DC420 /1DACF0 mark started before their first cancellation guard.
        task = task with { Started = true };
        var owner = current.ReadOwnedPersonCommandManager() ?? throw TalentCommandMissing("task_manager");
        owner.Talents![task.Id] = task;
        current.SetOwnedPersonCommandManager(owner);
        state = state with { PersonnelCommands = owner };
        var effects = new List<CampaignEffectRequest>();
        if (task.IsRecruitWait)
        {
            var wait = AdvanceTalentRecruitWaitCurrent(state, current, task, date, effects);
            return new(PublishTalentEffects(wait.State), wait.Retire);
        }
        var cancel = ReadTalentCancellation(state, current, task, date);
        if (cancel != TalentCancellation.None)
            return new(PublishTalentEffects(CancelTalentCurrent(state, current, task, date, cancel, effects)), true);
        int remaining = Math.Max(0, unchecked(PersonReturnCommands.CalendarOrdinal(task.DueDate) -
            PersonReturnCommands.CalendarOrdinal(date)));
        if (remaining > 1) return new(state, false);
        cancel = ReadTalentCancellation(state, current, task, date);
        if (cancel != TalentCancellation.None)
            return new(PublishTalentEffects(CancelTalentCurrent(state, current, task, date, cancel, effects)), true);
        if (task.Kind == TalentOrderKind.Search)
        {
            var result = CompleteSearchCurrent(state, current, task, date, effects);
            state = result.State ?? throw TalentCommandMissing(result.UnresolvedOwner ?? "search_completion");
        }
        else
            state = CompleteTalentRecruitCurrent(state, current, task, date, effects);
        return new(PublishTalentEffects(state), true);

        GameState PublishTalentEffects(GameState result) => effects.Count == 0 ? result : result with
        {
            CampaignEffectRequests = (result.CampaignEffectRequests ?? throw TalentCommandMissing("effect_owner")).Append(
                CampaignEffectSource.TalentCommands, result.NativeMapSeason?.AdvancedCalendarSteps
                    ?? throw TalentCommandMissing("effect_calendar"), effects.ToArray())
        };
    }

}
