using San14.Core;
using San14.Data.AI.Original;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The actual strategy-preparation caution reset. Each registered
    /// relation is cleared before the ordered registry is emptied. Other
    /// caution rows, group labels/members and army receivers retain their owners.</summary>
    public GameState ResetOriginalAiStrategyCautions(GameState state)
    {
        var lifecycle = state.ReadArmyLifecycle()
            ?? throw new InvalidDataException("original_ai.strategy_cautions.lifecycle");
        if (!lifecycle.Indexes.Registries.TryGetValue(WorldEntityKind.ArmyCaution, out var registered) || registered is null)
            throw new InvalidDataException("original_ai.strategy_cautions.registry");
        if (registered.Count == 0) return state;
        var cautions = lifecycle.ArmyCautions
            ?? throw new InvalidDataException("original_ai.strategy_cautions.relations_owner");
        var reset = OriginalAiStrategyCautions.ResetRegistered(cautions, registered);
        var registries = new Dictionary<WorldEntityKind, List<WorldEntityReference>?>(lifecycle.Indexes.Registries)
        {
            [WorldEntityKind.ArmyCaution] = []
        };
        return state.WithArmyLifecycle(lifecycle with
        {
            ArmyCautions = reset,
            Indexes = lifecycle.Indexes with { Registries = registries }
        });
    }
}
