using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal (GameState State, int NativeReturn) ApplyStoryPersonRelationsCurrent(GameState state,
        SourceWorldContext current, int code, int first, int second = 0)
    {
        if (code == 165)
            return (PersonRelationshipSource.Valid(current, first)
                ? PersonRelationshipSource.ClearPartner(state, current, first) : state, 1);
        if (!PersonRelationshipSource.Valid(current, first) || !PersonRelationshipSource.Valid(current, second))
            return (state, 1);
        if (code == 164)
        {
            if (PersonRelationshipSource.Sex(state, current, first) == PersonRelationshipSource.Sex(state, current, second))
                return (state, 0);
            return (PersonRelationshipSource.SetPartners(state, current, first, second), 1);
        }
        if (code == 319) return (first != second ? PersonRelationshipSource.AddLike(state, current, first, second) : state, 1);
        throw new InvalidDataException("story.relationship.opcode");
    }
}
