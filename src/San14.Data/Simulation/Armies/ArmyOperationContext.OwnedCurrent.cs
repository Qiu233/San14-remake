using San14.Core;

namespace San14.Data;

public sealed partial class ArmyOperationContext
{
    /// <summary>Completes one private writable current over the actual saved
    /// indexes. No caller sees the intermediate source prefix without a pool.</summary>
    internal SourceWorldContext CaptureWritableOwnedCurrent(GameState state, ArmyLifecycleSourceView arena)
    {
        var current = CaptureWithCurrentFields(state, true)
            ?? throw new InvalidDataException("Return definitions/content identity mismatch.");
        current.OverlayOwnedArmyReturnCurrentFields(state);
        OwnedArmyLifecycleTargets.BindCurrent(state, current);
        current.CurrentPlayerForce = OwnedCurrentPlayerForce.ReadCurrentReference(state, current);
        _ = WorldIndexContext.ImportOwnedState(current, arena.Indexes, state);
        current.BindArmyMapOccupancy(state);
        current.RestoreOwnedArmyReturnSlots(state, arena);
        OwnedArmyDestructorFlags.BindCurrent(arena, current);
        BaseCaptureCurrentInputs.BindCurrent(state, current);
        //Explicit current reset images retain their original final precedence.
        OwnedArmyLifecycleDetachedInputs.BindCurrent(state, current);
        ForceUnregisterInputs.BindCurrent(state, current);
        return current;
    }
}
