using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal GameState? PrepareOrdinaryImmediateRoute(GameState state, UnitState army)
    {
        if (state.Units.GetValueOrDefault(army.Id) != army ||
            army.March?.NextStep != ArmyMarchDestination.None) return null;
        try
        {
            var current = CaptureWritableTransportRouteCurrent(state);
            // Immediate bank0 and worker bank1 select1845B0's independent
            // buffers/locks only. ArmyRouteSearch owns private nodes per call.
            // Unlike the changed-list worker, this call preserves a live cache.
            var rewritten = OrdinaryArmyRouteSource.Rewrite(state, current, army, refresh: false);
            if (rewritten.Receipt is not { } route || route.ArmyPhysical != army.NativeArmyOrigin?.NativeSlotIndex)
                return null;
            var changed = army with
            {
                NativeRouteGoal1F8 = route.CachedGoalWrite ?? army.NativeRouteGoal1F8,
                March = route.NextHexWrite is ushort next
                    ? army.March with { NextStep = ArmyMarchSourceAdapter.DecodeDestination(next) } : army.March
            };
            var result = state with { Units = new(state.Units) { [army.Id] = changed } };
            var pool = current.ReadOwnedCurrentListPool()!;
            return result.WithArmyLifecycle(result.ReadArmyLifecycle()! with { Indexes = pool.ExportOwnedState() });
        }
        catch (InvalidDataException) { return null; }
    }

    /// <summary>One actual pending batch uses one writable definition/arena
    /// capture for ordinary and transport actors, in source list order.</summary>
    public ArmyChangedListsResult CompleteChangedTransportRoutes(GameState state)
    {
        if (state.ReadArmyLifecycle() is not { } arena ||
            arena.Indexes.ChangedPendingUnitIds is not { Count: > 0 })
            return ArmyChangedListsSource.TryAdvanceFinite(state);
        try
        {
            var current = CaptureWritableTransportRouteCurrent(state);
            var continued = ArmyChangedListsSource.TryAdvanceFinite(state, current, TransportArmyRouteSource.Rewrite,
                (world, definitions, army) => OrdinaryArmyRouteSource.Rewrite(world, definitions, army));
            return continued;
        }
        catch (InvalidDataException error) { return new(null, null, error.Message); }
    }

    private SourceWorldContext CaptureWritableTransportRouteCurrent(GameState state)
    {
        var arena = state.ReadArmyLifecycle() ?? throw new InvalidDataException("current.transport_route_saved_arena");
        var current = OwnedArmyReturnCurrentFields.CaptureCurrent(state, _definitions);
        _ = WorldIndexContext.ImportOwnedState(current, arena.Indexes, state);
        current.BindArmyMapOccupancy(state);
        current.RestoreOwnedArmyReturnSlots(state, arena);
        OwnedArmyDestructorFlags.BindCurrent(arena, current);
        OwnedArmyLifecycleTargets.BindCurrent(state, current);
        BaseCaptureCurrentInputs.BindCurrent(state, current);
        return current;
    }
}
