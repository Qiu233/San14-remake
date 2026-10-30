using System.Collections.Immutable;

namespace San14.Core;

/// <summary>Current planning quantities produced by the actual economic and
/// needs-estimation operations. Signed estimates are not resource stocks.
/// Null preserves an unknown quantity; construction/reset writes its known
/// values explicitly rather than acquiring defaults from this record.</summary>
public sealed record OriginalAiFootholdMemory(int? FoodNeed, int? GoldNeed,
    int? GoldForecast, int? FoodForecast)
{
    /// <summary>Captured threatening City/Gate identity. Zero is a known
    /// absent target; null preserves unknown pre-refresh knowledge.</summary>
    public int? ThreateningFootholdLocationId { get; init; }
    /// <summary>Captured friendly route Area identity. Zero is a known
    /// absent route; only the actual construction/reset writes that zero.</summary>
    public int? FriendlyRouteAreaId { get; init; }
    /// <summary>Produced transport troop capacity, independent of current troop
    /// stock. Unknown remains null until an actual constructor/reset/estimate.</summary>
    public int? TransportTroopCeiling { get; init; }
}

/// <summary>The ordered frontier and rear classifications captured at real
/// strategy preparation. Location identities are the ordinary City/Gate IDs,
/// not registry handles. Commands do not continuously rebuild these lists.</summary>
public sealed record OriginalAiForceFootholds(ImmutableArray<int> FrontierLocationIds,
    ImmutableArray<int> RearLocationIds)
{
    /// <summary>The ordered deep-interior classification from actual strategy
    /// preparation. Null is unknown, distinct from a produced empty list.</summary>
    public ImmutableArray<int>? DeepInteriorLocationIds { get; init; }
}

/// <summary>World-owned planning facts shared by every strategy. Entry absence
/// is unknown, and a present empty classification is a known source result.
/// Immutable collections survive candidate/worker copies without sharing
/// mutable state. There is no policy, delegate or blackboard stored here.</summary>
public sealed record OriginalAiStrategyMemory(
    ImmutableDictionary<int, OriginalAiFootholdMemory> Footholds,
    ImmutableDictionary<int, OriginalAiForceFootholds> Forces);

public sealed partial record GameState
{
    // Legacy saves remain unknown. Only reached construction/reset/refresh
    // producers install or replace planning facts; information queries do not.
    public OriginalAiStrategyMemory? OriginalAiStrategyMemory { get; init; }
}
