namespace San14.Core;

public static class MapObjectLifecycleRules
{
    /// <summary>Only queue membership is unique; this never suppresses a caller's effects.</summary>
    public static GameState EnqueueRetirement(GameState state, MapObjectReceiverId receiver)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.MapObjects?.Find(receiver) is null)
            throw new InvalidDataException("Current map object receiver is unavailable.");
        var lifecycle = state.ArmyLifecycle ?? throw new InvalidDataException("Current world indexes are unavailable.");
        var pending = lifecycle.Indexes.PendingObjectRetirements
            ?? throw new InvalidDataException("Current map object retirement queue is unavailable.");
        if (pending.Contains(receiver)) return state;
        return state with { ArmyLifecycle = lifecycle with { Indexes = lifecycle.Indexes with
            { PendingObjectRetirements = new(pending) { receiver } } } };
    }

    /// <summary>Clear this cell unconditionally, even if another object now occupies it.</summary>
    public static GameState ClearHexBinding(GameState state, int hex)
    {
        ArgumentNullException.ThrowIfNull(state);
        var owner = state.MapObjects ?? throw new InvalidDataException("Current map objects are unavailable.");
        var bindings = owner.HexBindings ?? throw new InvalidDataException("Current map object presence is unavailable.");
        if (hex < 0 || hex >= bindings.TileCount) throw new InvalidDataException("Map object binding cell is invalid.");
        var occupied = new Dictionary<int, MapObjectReceiverId>(bindings.Occupied);
        occupied.Remove(hex);
        return state with { MapObjects = owner with { HexBindings = bindings with { Occupied = occupied } } };
    }
}
