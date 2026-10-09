using San14.Core;

namespace San14.Data;

public sealed partial class OriginalOwnedArmyCampaignRules
{
    public OriginalPersonReturnManagerResult TryAdvancePersonReturnCommands(GameState state, NativeMapSeasonDate currentWorldThirdDate)
    {
        if (currentWorldThirdDate.Hour is not (8 or 22)) return new(state, [], null);
        if (state.PersonnelReturns is not { } commands || state.NativeArmyLifecycle?.Indexes.ReturningPersonnelTaskOrder is null)
            return new(null, [], "current.owned_Person_Return_manager");
        if (!commands.Tasks.Values.Any(task => task.DispatchSlot.HasValue) && commands.PendingRetirements.Length == 0)
            return new(state, [], null);
        try
        {
            var world = CaptureOwnedArmyReturnWorld(state);
            return OriginalPersonReturnCommands.TryAdvanceCurrent(state, world.Context, world.Pool, currentWorldThirdDate,
                new PersonReturnGovernorQueries(state, _governorInputs, world.Context));
        }
        catch (InvalidDataException error) { return new(null, [], error.Message); }
    }

    private sealed class PersonReturnGovernorQueries(GameState state, OriginalOwnedArmyGovernorComparatorInputs inputs,
        OriginalEventInitializationContext current) : IOriginalArmyReturnGovernorCurrentQueries
    {
        public OriginalArmyReturnGovernorComparatorFields? ReadCurrentComparatorFields(int person) => inputs.ReadCurrentComparatorFields(state, person);
        public int? ReadCurrentAssociatedVirtual98(OriginalPersonLinkedEntityReference reference) =>
            reference.Identity is int receiver && current.ReadOwnedPersonCommandManager()?.Tasks.ContainsKey(receiver) == true
                ? 0xF : null; //Actual1EDB40 Return v98, read only when reached.
    }
}
