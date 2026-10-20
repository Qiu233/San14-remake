using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

internal sealed record FacilityEffectDefinition(S14FacilityEffectChannel First,
    S14FacilityEffectChannel Second, byte Minimum, byte Maximum)
{
    //Separate190260 getter; old explicitly constructed definitions retain
    //unknown rather than borrowing their minimum/maximum effect radius.
    internal byte? TriggerRange { get; init; }
    internal S14FacilityEffectChannel Channel(int index) => index == 0 ? First : Second;
    internal int MaximumFor(GameState state, int kind)
    {
        bool advanced = FacilityEffectCandidates.K(BattleRemakeCurrentSettings.ReadAdvancedSettings(state), "advanced_settings");
        if (!advanced || kind is 9 or 10 or 16) return Maximum;
        // The loaded definition's protected byte getter returns a byte after
        // adding setting25. Retain that wrap without a native encrypted vector.
        int extra = FacilityEffectCandidates.K(BattleRemakeCurrentSettings.ReadAdvancedParameter(state, 37), "advanced_range25");
        return unchecked((byte)(Maximum + extra));
    }
}
