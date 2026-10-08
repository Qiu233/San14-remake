using San14.Core;

namespace San14.Data;

public sealed partial class OriginalOwnedArmyCampaignRules
{
    public OriginalNormalArmyPersonalityApplication TryApplyNativeNormalPersonalityReaction(GameState state, int targetArmyId)
    {
        if (!state.Units.TryGetValue(targetArmyId, out var target) || CaptureCurrentDefinitions(state) is not { } definitions)
            return new(OriginalNormalArmyPersonalityApplicationOutcome.CurrentInputsUnresolved, state, null, [], "current.target_Commander");
        return OriginalNormalArmyPersonalityApplicationSource.TryApply(state, target, Context(definitions), definitions, Revision);
    }
    /// <summary>Pure current17921B nonlethal personality producer. The real
    /// pair driver must query it after257420, not use it to skip Force/report
    /// callbacks. Unsupported179E80 is explicitly marked in the receipt.</summary>
    public OriginalNormalArmyPersonalityReactionReceipt? ResolveNativeNormalPersonalityReaction(GameState state, int targetArmyId)
    {
        if (!state.Units.TryGetValue(targetArmyId, out var target) || CaptureCurrentDefinitions(state) is not { } definitions)
            return null;
        return OriginalNormalArmyPersonalityReaction.Resolve(state, target, Context(definitions), definitions, Revision);
    }
}
