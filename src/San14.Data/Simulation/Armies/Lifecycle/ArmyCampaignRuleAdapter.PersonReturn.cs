using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    public PersonReturnManagerResult TryAdvancePersonReturnCommands(GameState state, NativeMapSeasonDate currentWorldThirdDate)
        => AdvancePersonReturnCommands(state, currentWorldThirdDate, null, null);

    internal PersonReturnManagerResult ContinuePersonReturnCommands(GameState state,
        NativeMapSeasonDate currentWorldThirdDate, int resumeAfterSlot, int? resumedDiplomacyTaskId = null)
        => AdvancePersonReturnCommands(state, currentWorldThirdDate, null, null, resumeAfterSlot, resumedDiplomacyTaskId);

    //The actual stage20 cut borrows the same current/list pool as movement
    //and arrival. It must not recapture or replace that live pool.
    internal PersonReturnManagerResult CompletePersonReturnCut(GameState state, SourceWorldContext current,
        WorldIndexContext pool, NativeMapSeasonDate date)
        => AdvancePersonReturnCommands(state, date, current, pool);

    private PersonReturnManagerResult AdvancePersonReturnCommands(GameState state, NativeMapSeasonDate currentWorldThirdDate,
        SourceWorldContext? operationCurrent, WorldIndexContext? operationPool, int resumeAfterSlot = -1,
        int? resumedDiplomacyTaskId = null)
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
            if (resumedDiplomacyTaskId is int resumedTask)
                state = ContinueUnilateralDiplomacyCurrent(state, world.Context, resumedTask, currentWorldThirdDate);
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
                    ? diplomacy.NativeKind : null;
    }
}
