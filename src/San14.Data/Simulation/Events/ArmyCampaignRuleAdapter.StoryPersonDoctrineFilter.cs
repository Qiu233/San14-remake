using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //434 compares the saved current Person14C byte directly. It does not
    //resolve or validate a Doctrine definition or impose Person validity.
    internal int ReadStoryPersonDoctrineByteCurrent(GameState state, SourceWorldContext current, int physical)
    {
        int id = current.ReadPersonIdentity(physical)
            ?? throw new InvalidDataException("story.person_filter.Person_identity");
        var person = state.Officers.GetValueOrDefault(id)
            ?? throw new InvalidDataException("story.person_filter.Person_current_owner");
        int raw = OfficerDoctrineRules.ReadDoctrineId(person.DoctrineId, person.AbilityState)
            ?? throw new InvalidDataException("story.person_filter.Person_raw_Doctrine14C");
        if ((uint)raw > byte.MaxValue) throw new InvalidDataException("story.person_filter.Person_Doctrine_byte");
        return raw;
    }
}
