using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    public PersonReturnManagerResult TryAdvancePersonReturnCommands(GameState state, NativeMapSeasonDate currentWorldThirdDate)
    {
        if (currentWorldThirdDate.Hour is not (8 or 22)) return new(state, [], null);
        if (state.PersonnelReturns is not { } commands || state.ReadArmyLifecycle()?.Indexes.ReturningPersonnelTaskOrder is null)
            return new(null, [], "current.owned_Person_Return_manager");
        if (!commands.Tasks.Values.Any(task => task.DispatchSlot.HasValue) && commands.PendingRetirements.Length == 0)
            return new(state, [], null);
        try
        {
            var world = CaptureOwnedArmyReturnWorld(state);
            return PersonReturnCommands.TryAdvanceCurrent(state, world.Context, world.Pool, currentWorldThirdDate,
                new PersonReturnGovernorQueries(state, _governorInputs, world.Context));
        }
        catch (InvalidDataException error) { return new(null, [], error.Message); }
    }

    private sealed class PersonReturnGovernorQueries(GameState state, OwnedArmyGovernorComparatorInputs inputs,
        SourceWorldContext current) : IArmyReturnGovernorCurrentQueries
    {
        public ArmyReturnGovernorComparatorFields? ReadCurrentComparatorFields(int person) => inputs.ReadCurrentComparatorFields(state, person);
        public int? ReadCurrentAssociatedVirtual98(PersonLinkedEntityReference reference) =>
            reference.Identity is int receiver && current.ReadOwnedPersonCommandManager()?.Tasks.ContainsKey(receiver) == true
                ? 0xF : null; //Actual1EDB40 Return v98, read only when reached.
    }
}
