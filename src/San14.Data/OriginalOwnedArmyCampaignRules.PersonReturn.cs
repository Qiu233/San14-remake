using San14.Core;

namespace San14.Data;

public sealed partial class OriginalOwnedArmyCampaignRules
{
    public OriginalPersonReturnManagerResult TryAdvancePersonReturnCommands(GameState state, NativeMapSeasonDate currentWorldThirdDate)
    {
        if (currentWorldThirdDate.Hour is not (8 or 22)) return new(state, [], null);
        if (state.NativePersonReturnCommands is not { } commands || state.NativeArmyLifecycle?.Auxiliaries is null)
            return new(null, [], "current.owned_Person_Return_manager");
        if (commands.IdToReceiver.Count == 0 && commands.Secondary28Descriptor.Identity is int secondary &&
            state.NativeArmyLifecycle.Pool.Descriptors.TryGetValue(secondary, out var row) && row.Head is null)
            return new(state, [], null); //Actual empty tree and empty secondary, only transientEB0 reset.
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
            reference.Identity is int receiver && current.ReadOwnedPersonCommandManager()?.Commands.GetValueOrDefault(receiver)?.Kind == "return"
                ? 0xF : null; //Actual1EDB40 Return v98, read only when reached.
    }
}
