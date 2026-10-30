using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //Only83C100's real post-Result special loop supplies this fifth1 caller.
    //The invocation owner retains/publishes the returned world and effect FIFO.
    internal GameState ApplyStoryStateAdvantageSpecialPersonnelCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, ForceReference ordinary,
        ForceReference special, int level, List<CampaignEffectRequest> effects)
    {
        ArgumentNullException.ThrowIfNull(effects);
        if (!ReferenceEquals(pool.ArmyReturnOwner, current) || !pool.OwnsArmyReturnProfile())
            throw new InvalidDataException("story-state-advantage.special_current_pool");
        return new MonthlyCampaignMaintenanceSource(this, state, current, pool, borrowedEffects: effects)
            .ApplyStoryStateAdvantagePersonnel(ordinary, special, level);
    }

    internal sealed partial class MonthlyCampaignMaintenanceSource
    {
        internal GameState ApplyStoryStateAdvantagePersonnel(ForceReference ordinary,
            ForceReference special, int level)
        {
            ForceRelationChangeCurrent.ApplyStoryStateAdvantageSpecialPersonnel(() => state, current,
                ordinary, special, level, P,
                (person, force) => ConsumeSpecialPersonnelCurrent(person, force, storyStateAdvantage: true),
                package.ReadApplicationGameplay(),
                count => Narration(Format(0x324C, new(Force0: Physical(special), Integers: [count]))),
                removedPeople: RecallSpecialPersonnel);
            return state;
        }

        private bool IsStoryStateAdvantagePersonnelSettled(int person)
        {
            var location = PersonPositionRepair.DecodeSecondLocation(
                Need(current.ReadPersonSecondLocation(person), "story_special_PersonE4"));
            ushort home = Need(current.ReadPersonFirstLocation(person), "story_special_PersonE2");
            if (location.Index != home) return false;
            var receiver = BaseLookup.Find(home, current);
            int district = Need(current.ReadBaseDistrictSlot(receiver), "story_special_Base_virtual78");
            return district == Need(current.ReadPersonDistrictSlot(person), "story_special_PersonE0");
        }
    }
}
