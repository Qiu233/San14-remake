using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //94's wrapper reads World3A through32EFA0, independently of the Story's
    //dialogue Force and selected targets. Reuse the ordinary land-advantage
    //query and saved experience multiplier rather than a doctrine/UI quote.
    private CampaignNarrationExperienceBonus ReadCampaignNarrationExperienceBonusCurrent(
        GameState state, SourceWorldContext current)
    {
        var force = OwnedCurrentPlayerForce.ReadCurrentReference(state, current)
            ?? throw StoryTextMissing("narration_current_player_Force");
        var advantage = PersonReturnPathSource.ReadForceAdvantage(state, current, force, 6);
        int percentage = advantage.Value
            ?? throw StoryTextMissing(advantage.UnresolvedOwner ?? "narration_experience_geographic_advantage");
        if (advantage.QualifiedStates.Length == 0) return new(false, percentage, 0);
        int multiplier = ArmyDestructionParameterSeed.ReadCurrent(state,
            SimulationParameterKeys.ArmyDestruction.ForceExperienceLandAdvantageMultiplier)
            ?? throw StoryTextMissing("narration_experience_geographic_multiplier");
        return new(true, percentage, multiplier);
    }
}
