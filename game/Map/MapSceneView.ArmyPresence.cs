using San14.Core;
using San14.Data;

public partial class MapSceneView
{
    // The rules own the immutable per-World capture. Rendering and picking
    // borrow the same result, including while a period checkpoint is lent.
    private ArmyMapPresenceFrame? CaptureCurrentArmyPresence(GameState world)
    {
        if (world.NativeArmySimulation is null) return null;
        return _d3d11ArmyPresenceQueries?.Invoke(world)
            ?? (world.Units.Count == 0 ? null
                : throw new InvalidDataException("The current Army map-presence query owner is unavailable."));
    }
}
