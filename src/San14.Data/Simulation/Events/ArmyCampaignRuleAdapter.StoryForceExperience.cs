using San14.Core;

namespace San14.Data;

internal sealed record StoryForceExperienceMutationResult(GameState State, int NativeReturn,
    IReadOnlyList<CampaignEffectRequest> Effects);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Ordinary121 uses the actual land advantage before the shared
    /// Force experience writer. Its caller publishes the returned real pending
    /// presentation requests once through the normal campaign effect owner.</summary>
    internal StoryForceExperienceMutationResult ApplyStoryForceExperienceCurrent(GameState state,
        SourceWorldContext current, IReadOnlyList<int> arguments, ForceReference force)
    {
        if (arguments.Count != 8) throw StoryForceExperienceMissing("arguments");
        var advantage = PersonReturnPathSource.ReadForceAdvantage(state, current, force, 6);
        int effect = advantage.Value ?? throw StoryForceExperienceMissing(advantage.UnresolvedOwner ?? "Force_land_advantage6");
        int scale = ArmyDestructionParameterSeed.ReadCurrent(state,
            SimulationParameterKeys.ArmyDestruction.ForceExperienceLandAdvantageMultiplier)
            ?? throw StoryForceExperienceMissing("land_advantage_multiplier");
        int requested = arguments[1];
        //The original uses signed int32 multiplication followed by signed
        //division toward zero. Negative requests still reach this formula.
        int amount = unchecked(unchecked(scale * effect * requested) / 100 + requested);
        var effects = new List<CampaignEffectRequest>();
        state = ApplyAdministrationForceExperienceCurrent(state, current, force, amount, effects);
        //258A60 owns validity, nonnegative gain, clamping, source levels and
        //the player's gain/level-ten requests. There is no manager summary.
        return new(state, 1, effects);
    }

    private static InvalidDataException StoryForceExperienceMissing(string owner)
        => new("story.force_experience.current." + owner);
}
