using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Read-only normal pre-start visual definitions. The existing
    /// preview capture validates world/source identity and supplies only owners
    /// present before the simulation starts; absent runtime inputs stay unknown.</summary>
    public SourceWorldContext CaptureNewGameVisualDefinitions(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!ArmyOperationContext.IsNewGamePreview(state))
            throw new InvalidDataException("new-game.visual.requires-initializing-world-without-simulation");
        return _definitions.CaptureNewGamePreview(state)
            ?? throw new InvalidDataException("new-game.visual.world-source-identity");
    }
}
