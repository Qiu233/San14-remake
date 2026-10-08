using San14.Core;

namespace San14.Data;

public sealed partial class OriginalOwnedArmyCampaignRules
{
    /// <summary>One actual pending batch uses one writable definition/arena
    /// capture. No whole-state JSON clone or per-Army map/PE loading is needed.</summary>
    public OriginalArmyChangedListsResult CompleteChangedTransportRoutes(GameState state)
    {
        if (state.NativeArmyLifecycle is not { ChangedLists: { } owner } arena ||
            owner.Pending18.Identity is not int pending ||
            !arena.Pool.Descriptors.TryGetValue(pending, out var descriptor) || descriptor.Head is null)
            return OriginalArmyChangedListsSource.TryAdvanceFinite(state);
        try
        {
            var current = CaptureWritableTransportRouteCurrent(state);
            var continued = OriginalArmyChangedListsSource.TryAdvanceFinite(state, current, OriginalTransportArmyRouteSource.Rewrite);
            if (continued.State is { } completed) StateValidator.Validate(completed);
            return continued;
        }
        catch (InvalidDataException error) { return new(null, null, error.Message); }
    }

    private OriginalEventInitializationContext CaptureWritableTransportRouteCurrent(GameState state)
    {
        var arena = state.NativeArmyLifecycle ?? throw new InvalidDataException("current.transport_route_saved_arena");
        var current = OriginalOwnedArmyReturnCurrentFields.CaptureCurrent(state, _definitions);
        _ = OriginalCurrentListPool.ImportOwnedState(current, arena.Pool);
        current.RestoreOwnedArmyReturnBindings(arena);
        current.RestoreOwnedArmyReturnSlots(state, arena);
        OriginalOwnedArmyDestructorFlags.BindCurrent(arena, current);
        OriginalOwnedArmyLifecycleTargets.BindCurrent(arena, current);
        OriginalBaseCaptureCurrentInputs.BindCurrent(state, current);
        return current;
    }
}
