using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

/// <summary>Only reached constructors/resets establish zero or empty facts.
/// Restoring an older archive never calls these writers.</summary>
internal static class OriginalAiStrategyMemoryLifecycle
{
    internal static GameState InitializeFresh(GameState state)
    {
        // The normal new-campaign host has already called PlayableSkeleton.Start.
        // Fresh construction is owned by CreateFreshOwnedSimulation, not by the
        // world's turn-phase enum. Restore never enters that constructor.
        if (state.OriginalAiStrategyMemory is not null)
            throw new InvalidDataException("original_ai.strategy_memory.fresh_constructor");
        // The fixed group table constructs and resets all 501 receivers before
        // scenario population. This fresh-only boundary follows installation
        // of the actual lifecycle owner; restore and ordinary reads never fill
        // unknown objectives from an older archive.
        var lifecycle = state.ReadArmyLifecycle()
            ?? throw new InvalidDataException("original_ai.troops.fresh_lifecycle_owner");
        var groups = lifecycle.ArmyGroups
            ?? throw new InvalidDataException("original_ai.troops.fresh_group_owner");
        if (groups.Objectives is not null)
            throw new InvalidDataException("original_ai.troops.fresh_objectives_already_initialized");
        state = state.WithArmyLifecycle(lifecycle with { ArmyGroups = groups with
        {
            Objectives = Enumerable.Range(0, 501).ToImmutableDictionary(
                id => id, _ => ArmyGroupObjectiveState.Reset)
        } });
        // All table receivers are constructed before the scenario is loaded.
        // The domain excludes the original invalid table-zero sentinels.
        state = OriginalAiStrategyParameters.InstallFresh(state);
        return state with { OriginalAiStrategyMemory = new(
            Enumerable.Range(1, 61).ToImmutableDictionary(id => id, _ => ZeroFoothold()),
            Enumerable.Range(1, 51).ToImmutableDictionary(id => id, _ => EmptyForce())) };
    }

    internal static GameState ResetForce(GameState state, int forceId)
    {
        if (forceId == 0) return state;
        if (forceId is < 1 or > 51) throw new ArgumentOutOfRangeException(nameof(forceId));
        var memory = state.OriginalAiStrategyMemory ?? Unknown();
        return state with { OriginalAiStrategyMemory = memory with
            { Forces = memory.Forces.SetItem(forceId, EmptyForce()) } };
    }

    internal static GameState ResetFoothold(GameState state, int locationId)
    {
        if (locationId == 0) return state;
        if (locationId is < 1 or > 61) throw new ArgumentOutOfRangeException(nameof(locationId));
        var memory = state.OriginalAiStrategyMemory ?? Unknown();
        return state with { OriginalAiStrategyMemory = memory with
            { Footholds = memory.Footholds.SetItem(locationId, ZeroFoothold()) } };
    }

    private static OriginalAiStrategyMemory Unknown() => new(
        ImmutableDictionary<int, OriginalAiFootholdMemory>.Empty,
        ImmutableDictionary<int, OriginalAiForceFootholds>.Empty);
    private static OriginalAiFootholdMemory ZeroFoothold() => new(0, 0, 0, 0)
        { ThreateningFootholdLocationId = 0, FriendlyRouteAreaId = 0, TransportTroopCeiling = 0 };
    private static OriginalAiForceFootholds EmptyForce() => new([], [])
        { DeepInteriorLocationIds = [] };
}

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The completed frontier/rear pass of strategy preparation.
    /// This does not perform or acknowledge the preceding crisis pass.</summary>
    public GameState RefreshOriginalAiForceFootholdsCurrent(GameState state)
    {
        var memory = state.OriginalAiStrategyMemory
            ?? throw new InvalidDataException("original_ai.strategy_memory.owner");
        var forces = memory.Forces.ToBuilder();
        foreach (var row in CaptureOriginalAiForceMemoryCurrent(state))
        {
            if (row.ForceId == 0) continue;
            // This writer owns only its two lists. It must not erase the
            // independently produced third list during an isolated refresh.
            var before = forces.GetValueOrDefault(row.ForceId);
            forces[row.ForceId] = new(row.FrontierFootholds.Select(x => x.Id).ToImmutableArray(),
                row.RearFootholds.Select(x => x.Id).ToImmutableArray())
                { DeepInteriorLocationIds = before?.DeepInteriorLocationIds };
        }
        return state with { OriginalAiStrategyMemory = memory with { Forces = forces.ToImmutable() } };
    }
}
