using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    public PersonReturnManagerResult TryAdvancePersonReturnCommands(GameState state, NativeMapSeasonDate currentWorldThirdDate)
    {
        if (currentWorldThirdDate.Hour is not (8 or 22)) return new(state, [], null);
        if (state.PersonnelCommands is not { } commands || state.ReadArmyLifecycle()?.Indexes.ReturningPersonnelTaskOrder is null)
            return new(null, [], "current.owned_Person_Return_manager");
        if (commands.Disappearances is null) return new(null, [], "current.personnel_disappearance_tasks");
        if (!commands.DispatchOrder().Any() && commands.PendingRetirements.Length == 0)
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
            reference.Identity is not int receiver ? null :
                current.ReadOwnedPersonCommandManager()?.Returns.ContainsKey(receiver) == true ? 0xF :
                current.ReadOwnedPersonCommandManager()?.Disappearances?.ContainsKey(receiver) == true ? 0x10 : null;
    }
}
