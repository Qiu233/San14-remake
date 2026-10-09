using San14.Core;

namespace San14.Data;

/// <summary>The current source's unsigned XP representation. Domain knowledge
/// outside that representation is retained and never truncated.</summary>
public static class OfficerGrowthSourceAdapter
{
    public static uint? ReadExperience(OfficerGrowthState? state, int abilityIndex) =>
        state?.ExperienceFor(abilityIndex) is ulong value && value <= uint.MaxValue ? (uint)value : null;

    public static OfficerGrowthState Fresh(uint[]? experience) =>
        new(experience?.Select(value => (ulong?)value).ToArray(), []);
}
