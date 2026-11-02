using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    // Both the force-list rows/sort and common information columns borrow the
    // same normal-load current. No gameplay allocation is made for presentation.
    internal NewGameOfficerAbilityProducer CreateNewGameOfficerAbilities(GameState state, SourceWorldContext current)
        => new(state,current,_scenario,Seed);
}
