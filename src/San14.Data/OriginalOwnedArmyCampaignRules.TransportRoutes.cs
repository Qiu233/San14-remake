using San14.Core;

namespace San14.Data;

public sealed partial class OriginalOwnedArmyCampaignRules
{
    /// <summary>One actual pending batch uses one writable definition/arena
    /// capture. No whole-state JSON clone or per-Army map/PE loading is needed.</summary>
    public OriginalArmyChangedListsResult CompleteChangedTransportRoutes(GameState state)
    {
        if (state.NativeArmyLifecycle is not { } arena ||
            arena.Indexes.ChangedPendingUnitIds is not { Count: > 0 })
            return OriginalArmyChangedListsSource.TryAdvanceFinite(state);
        try
        {
            var current = CaptureWritableTransportRouteCurrent(state);
            var continued = OriginalArmyChangedListsSource.TryAdvanceFinite(state, current, OriginalTransportArmyRouteSource.Rewrite);
            return continued;
        }
        catch (InvalidDataException error) { return new(null, null, error.Message); }
    }

    private OriginalEventInitializationContext CaptureWritableTransportRouteCurrent(GameState state)
    {
        var arena = state.NativeArmyLifecycle ?? throw new InvalidDataException("current.transport_route_saved_arena");
        var current = OriginalOwnedArmyReturnCurrentFields.CaptureCurrent(state, _definitions);
        _ = WorldIndexContext.ImportOwnedState(current, arena.Indexes, state);
        current.BindArmyMapOccupancy(state);
        current.RestoreOwnedArmyReturnSlots(state, arena);
        OriginalOwnedArmyDestructorFlags.BindCurrent(arena, current);
        OriginalOwnedArmyLifecycleTargets.BindCurrent(arena, current);
        OriginalBaseCaptureCurrentInputs.BindCurrent(state, current);
        return current;
    }
}
