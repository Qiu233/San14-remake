using San14.Core;

namespace San14.Data;

/// <summary>The CE fresh constructors allocate unused expeditions and clear
/// each province's expedition sequence. This is never a saved-world bind.</summary>
internal static class CampaignExpeditionFreshSource
{
    internal static GameState InstallFresh(GameState state)
    {
        if (state.Phase != TurnPhase.Initializing || state.Units.Count != 0 || state.CampaignExpeditions is not null)
            throw new InvalidDataException("Fresh expedition initialization cannot replace an existing controller.");
        //226B90 resets every Campaign target to no province. CStateData's
        //221A30 constructor clears the word containing its sequence byte;
        //the static province resource reader does not overwrite that byte.
        return state with { CampaignExpeditions = new([], Enumerable.Range(0, 23).ToDictionary(id => id, _ => 0)) };
    }
}
