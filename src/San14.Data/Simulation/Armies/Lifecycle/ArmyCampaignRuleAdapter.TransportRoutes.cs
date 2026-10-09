using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>One actual pending batch uses one writable definition/arena
    /// capture. No whole-state JSON clone or per-Army map/PE loading is needed.</summary>
    public ArmyChangedListsResult CompleteChangedTransportRoutes(GameState state)
    {
        if (state.NativeArmyLifecycle is not { } arena ||
            arena.Indexes.ChangedPendingUnitIds is not { Count: > 0 })
            return ArmyChangedListsSource.TryAdvanceFinite(state);
        try
        {
            var current = CaptureWritableTransportRouteCurrent(state);
            var continued = ArmyChangedListsSource.TryAdvanceFinite(state, current, TransportArmyRouteSource.Rewrite);
            return continued;
        }
        catch (InvalidDataException error) { return new(null, null, error.Message); }
    }

    private SourceWorldContext CaptureWritableTransportRouteCurrent(GameState state)
    {
        var arena = state.NativeArmyLifecycle ?? throw new InvalidDataException("current.transport_route_saved_arena");
        var current = OwnedArmyReturnCurrentFields.CaptureCurrent(state, _definitions);
        _ = WorldIndexContext.ImportOwnedState(current, arena.Indexes, state);
        current.BindArmyMapOccupancy(state);
        current.RestoreOwnedArmyReturnSlots(state, arena);
        OwnedArmyDestructorFlags.BindCurrent(arena, current);
        OwnedArmyLifecycleTargets.BindCurrent(arena, current);
        BaseCaptureCurrentInputs.BindCurrent(state, current);
        return current;
    }
}
