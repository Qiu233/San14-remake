using San14.Core;

namespace San14.Data;

internal sealed record StorySinglePersonDeathMutationResult(GameState State, int? NativeReturn,
    CampaignStorySinglePersonDeathContinuation? Continuation = null);

public sealed partial class ArmyCampaignRuleAdapter
{
    //3C3380 resolves one Person once, then owns295EE0/28E6D0/3D5120 in that order.
    private StorySinglePersonDeathMutationResult ApplyStorySinglePersonDeathCurrent(GameState state,
        SourceWorldContext current, int person, CampaignStorySinglePersonDeathContinuation? continuation,
        StoryPersonMutationClassification classification)
    {
        if (continuation is null && !SingleDeathNeed(current.ReadPersonNativeValidity(person), "Person_validity"))
            return new(state, 0);
        if (continuation is not null && continuation.PersonPhysicalSlot != person)
            throw SingleDeathMissing("resumed_target");
        var pool = current.ReadOwnedCurrentListPool() ?? throw SingleDeathMissing("shared_pool");
        bool manager = SingleDeathNeed(current.ReadEventManagerPresent(), "event_manager_presence");
        _ = BorrowOwnedArmyReturnWorld(state, current, pool);
        if (continuation is not null)
        {
            //The real successor command completes the death prefix. Do not
            //resolve a new actor/ruler or execute295EE0 a second time.
            if (state.PersonDeathContinuation is not null) return new(state, null, continuation);
        }
        else
        {
            if (manager)
            {
                current.ReportBlockerState = new(true, 1);
                state = state with { CampaignEventEligibility =
                    (state.CampaignEventEligibility ?? throw SingleDeathMissing("event_manager_owner"))
                        with { HistoryRecordingSuppressed = true } };
            }
            var death = new PersonDeathSource(this, state, current, pool)
                .ApplyCombatDeath(person, new(null), 0, destroyAssociatedArmy: true,
                    report: true, history: true, suppressReports: false);
            state = death.State;
            if (death.NativeReturn is null)
            {
                if (state.PersonDeathContinuation is null) throw SingleDeathMissing("successor_owner");
                state = PublishNormalTurnCurrent(state, current, pool);
                state = PersonAssignmentPrecallInputs.PublishCurrent(state, current);
                return new(state, null, new(person));
            }
        }
        //Single162 drains before clearing the reporting gate and recording the
        //actual manager fact; list237 instead drains once after its whole list.
        state = ConsumeArmyLifecyclePendingCurrent(state, current, pool) ?? throw SingleDeathMissing("pending_cleanup");
        if (manager)
        {
            current.ReportBlockerState = new(true, 0);
            state = state with { CampaignEventEligibility =
                (state.CampaignEventEligibility ?? throw SingleDeathMissing("event_manager_after_owner"))
                    with { HistoryRecordingSuppressed = false } };
            classification.RecordDeath(person);
        }
        state = PublishNormalTurnCurrent(state, current, pool);
        state = PersonAssignmentPrecallInputs.PublishCurrent(state, current);
        return new(state, 1);
    }

    private static T SingleDeathNeed<T>(T? value, string field) where T : struct => value ?? throw SingleDeathMissing(field);
    private static InvalidDataException SingleDeathMissing(string field) => new("story.single_person_death." + field);
}
