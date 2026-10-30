using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The source strategy preparation boundary, in its actual pass
    /// order. This neither invokes an AI tree nor installs a planner. Missing
    /// inputs fail explicitly; the caller publishes only the returned world.</summary>
    public GameState PrepareOriginalAiStrategyCurrent(GameState state)
    {
        if (Seed.Language != "TC")
            throw new InvalidDataException("original_ai.strategy_preparation.SC_source_contract");
        var working = ResetOriginalAiStrategyCautions(state);
        // Each publication can change its current owner (including reached
        // lifecycle tails in forecast quotes). Borrow the actual next owner.
        working = RefreshOriginalAiForecastsCurrent(working,
            CaptureOwnedArmyReturnWorld(working).Context);
        working = RefreshOriginalAiCrisisCurrent(working);
        working = RefreshOriginalAiForceFootholdsCurrent(working);
        return RefreshOriginalAiDeepInteriorFootholdsCurrent(working);
    }

    /// <summary>Only the second forecast/crisis pass writes these two Base
    /// memories. Skipped receivers retain their previous values; actual null
    /// pointers become known zero, independently of unknown legacy fields.</summary>
    public GameState RefreshOriginalAiCrisisCurrent(GameState state)
    {
        var memory = state.OriginalAiStrategyMemory
            ?? throw new InvalidDataException("original_ai.crisis.saved_memory");
        var rows = memory.Footholds.ToBuilder();
        foreach (var row in CaptureOriginalAiCrisisCurrent(state))
        {
            if (row.LocationId == 0) continue; // Domain excludes the native sentinel.
            if (!rows.TryGetValue(row.LocationId, out var before))
                throw new InvalidDataException("original_ai.crisis.saved_foothold." + row.LocationId);
            rows[row.LocationId] = before with
            {
                ThreateningFootholdLocationId = row.EnemyLocationId ?? 0,
                FriendlyRouteAreaId = row.TargetAreaId ?? 0
            };
        }
        return state with { OriginalAiStrategyMemory = memory with { Footholds = rows.ToImmutable() } };
    }
}
