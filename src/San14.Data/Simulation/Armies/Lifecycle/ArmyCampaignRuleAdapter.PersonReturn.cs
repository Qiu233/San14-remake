using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    public PersonReturnManagerResult TryAdvancePersonReturnCommands(GameState state, NativeMapSeasonDate currentWorldThirdDate)
        => AdvancePersonReturnCommands(state, currentWorldThirdDate, null, null);

    internal PersonReturnManagerResult ContinuePersonReturnCommands(GameState state,
        NativeMapSeasonDate currentWorldThirdDate, int resumeAfterSlot, int? resumedDiplomacyTaskId = null,
        int? resumedStrategicTaskId = null, int? resumedIntelligenceTaskId = null)
        => AdvancePersonReturnCommands(state, currentWorldThirdDate, null, null, resumeAfterSlot,
            resumedDiplomacyTaskId, resumedStrategicTaskId, resumedIntelligenceTaskId);

    //The actual stage20 cut borrows the same current/list pool as movement
    //and arrival. It must not recapture or replace that live pool.
    internal PersonReturnManagerResult CompletePersonReturnCut(GameState state, SourceWorldContext current,
        WorldIndexContext pool, NativeMapSeasonDate date)
        => AdvancePersonReturnCommands(state, date, current, pool);

    private PersonReturnManagerResult AdvancePersonReturnCommands(GameState state, NativeMapSeasonDate currentWorldThirdDate,
        SourceWorldContext? operationCurrent, WorldIndexContext? operationPool, int resumeAfterSlot = -1,
        int? resumedDiplomacyTaskId = null, int? resumedStrategicTaskId = null, int? resumedIntelligenceTaskId = null)
    {
        if (currentWorldThirdDate.Hour is not (8 or 22)) return new(state, [], null);
        if (state.PersonnelCommands is not { } commands || state.ReadArmyLifecycle()?.Indexes.ReturningPersonnelTaskOrder is null)
            return new(null, [], "current.owned_Person_Return_manager");
        if (commands.Disappearances is null) return new(null, [], "current.personnel_disappearance_tasks");
        if (!commands.DispatchOrder().Any() && commands.PendingRetirements.Length == 0)
            return new(state, [], null);
        try
        {
            var world = operationCurrent is null ? CaptureOwnedArmyReturnWorld(state) :
                BorrowOwnedArmyReturnWorld(state, operationCurrent, operationPool
                    ?? throw new InvalidDataException("current.person_return_pool"));
            if (commands.Returns.Values.Any(task => task.IsReassignment) || commands.Talents?.Count > 0 || commands.Diplomacies?.Count > 0)
                _personAssignmentPrecallInputs.BindCurrent(state, world.Context);
            if (commands.Intelligences?.Count > 0)
            {
                _baseCapacityInputs.BindCurrent(state, world.Context);
                _personAssignmentPrecallInputs.BindCurrent(state, world.Context);
            }
            if (resumedIntelligenceTaskId is int resumedIntelligence)
            {
                var retained = world.Context.ReadOwnedPersonCommandManager()?.Intelligences?.GetValueOrDefault(resumedIntelligence)
                    ?? throw new InvalidDataException("current.person_return_intelligence_task");
                if (retained.DispatchSlot != resumeAfterSlot || retained.NarrationProgress?.Pending is not { } pending ||
                    pending.SourceDate != currentWorldThirdDate)
                    throw new InvalidDataException("current.person_return_intelligence_resume_owner");
                var advanced = ((IPersonnelIntelligenceExecution)this).AdvanceIntelligenceCurrent(
                    state, world.Context, retained, currentWorldThirdDate);
                state = advanced.State;
                var updated = world.Context.ReadOwnedPersonCommandManager() ?? throw new InvalidDataException("current.person_return_intelligence_manager");
                if (updated.Intelligences?.GetValueOrDefault(resumedIntelligence)?.NarrationProgress?.Pending is not null)
                {
                    state = operationCurrent is not null ? PersonReturnCommands.PublishBorrowedCurrent(state, world.Context)
                        : PersonReturnCommands.PublishCurrent(state, world.Context);
                    return new(state, [], null) { NextAfterSlot = resumeAfterSlot, AwaitingIntelligenceTaskId = resumedIntelligence };
                }
                if (!advanced.Retire) throw new InvalidDataException("current.person_return_intelligence_incomplete_tail");
                updated = PersonReturnCommands.QueueRetirementCurrent(updated, world.Context, resumedIntelligence);
                state = state with { PersonnelCommands = updated };
            }
            if (resumedDiplomacyTaskId is int resumedTask)
            {
                if (state.DemandSurrenderCompletionTail is { } surrender && surrender.Task.Id == resumedTask)
                {
                    var advanced = ((IPersonnelDiplomacyExecution)this).AdvanceDiplomacyCurrent(
                        state, world.Context, surrender.Task, currentWorldThirdDate);
                    state = advanced.State;
                    if (state.DemandSurrenderCompletionTail is not null)
                    {
                        // Preserve the exact reached manager prefix, including
                        // its pending retirements. The current task has not
                        // returned merely because its nested Emperor UI waits.
                        state = operationCurrent is not null
                            ? PersonReturnCommands.PublishBorrowedCurrent(state, world.Context)
                            : PersonReturnCommands.PublishCurrent(state, world.Context);
                        return new(state, [], null)
                        { NextAfterSlot = resumeAfterSlot, AwaitingDiplomacyTaskId = resumedTask };
                    }
                }
                else
                    state = ContinueUnilateralDiplomacyCurrent(state, world.Context, resumedTask, currentWorldThirdDate);
            }
            if (resumedStrategicTaskId is int resumedStrategy &&
                state.StrategicPoisonEmperorContinuation is { } poison)
            {
                if (poison.TaskId != resumedStrategy)
                    throw new InvalidDataException("current.person_return_poison_task_owner");
                var (resumedState, completed) = ResumeStrategicPoisonEmperorCurrent(state, world.Context, world.Pool);
                state = resumedState;
                if (!completed)
                {
                    state = operationCurrent is not null
                        ? PersonReturnCommands.PublishBorrowedCurrent(state, world.Context)
                        : PersonReturnCommands.PublishCurrent(state, world.Context);
                    return new(state, [], null)
                    { NextAfterSlot = resumeAfterSlot, AwaitingStrategicTaskId = resumedStrategy };
                }
                var completedOwner = world.Context.ReadOwnedPersonCommandManager()
                    ?? throw new InvalidDataException("current.person_return_poison_command_manager");
                completedOwner = PersonReturnCommands.QueueRetirementCurrent(completedOwner, world.Context, resumedStrategy);
                state = state with { PersonnelCommands = completedOwner };
            }
            var queries = new PersonReturnGovernorQueries(state, _governorInputs, world.Context);
            return PersonReturnCommands.AdvanceCurrent(state, world.Context, world.Pool,
                currentWorldThirdDate, queries, operationCurrent is not null, this, resumeAfterSlot);
        }
        catch (InvalidDataException error) { return new(null, [], error.Message); }
    }

    private sealed class PersonReturnGovernorQueries(GameState state, OwnedArmyGovernorComparatorInputs inputs,
        SourceWorldContext current) : IArmyReturnGovernorCurrentQueries
    {
        public ArmyReturnGovernorComparatorFields? ReadCurrentComparatorFields(int person) => inputs.ReadCurrentComparatorFields(state, person);
        public int? ReadCurrentAssociatedVirtual98(PersonLinkedEntityReference reference) =>
            reference.Identity is not int receiver ? null :
                current.ReadOwnedPersonCommandManager()?.Returns.GetValueOrDefault(receiver) is { } task ? task.IsReassignment ? 1 : 0xF :
                current.ReadOwnedPersonCommandManager()?.Disappearances?.ContainsKey(receiver) == true ? 0x10 :
                current.ReadOwnedPersonCommandManager()?.Talents?.GetValueOrDefault(receiver) is { } talent
                    ? talent.IsRecruitWait ? 22 : talent.Kind == TalentOrderKind.Search ? 3 : 2 :
                current.ReadOwnedPersonCommandManager()?.Diplomacies?.GetValueOrDefault(receiver) is { } diplomacy
                    ? diplomacy.NativeKind :
                current.ReadOwnedPersonCommandManager()?.Strategies?.GetValueOrDefault(receiver) is { } strategic
                    ? strategic.Kind :
                current.ReadOwnedPersonCommandManager()?.Independences?.ContainsKey(receiver) == true ? 23 :
                current.ReadOwnedPersonCommandManager()?.Caravans?.GetValueOrDefault(receiver) is { } caravan
                    ? caravan.NativeKind :
                current.ReadOwnedPersonCommandManager()?.Intelligences?.GetValueOrDefault(receiver) is { } intelligence
                    ? intelligence.Kind == PersonnelIntelligenceKind.Away ? 21 : 25 : null;
    }
}
