namespace San14.Data;

// Missing reference is unknown; a reference with no identity is an observed null pointer.
public sealed record SceneryReference(int? PhysicalSlot);
public sealed record SceneryLayoutReference(int? Identity);

public interface IHexOwnershipState
{
    byte? ReadHexSurfaceCode(HexReference hex);
    void WriteHexForceSlot(HexReference hex, byte force);
    byte? ReadHexForceSlot(HexReference hex);
    ForceReference? ReadCurrentPlayerForce();
    int? ReadForcePhysicalSlot(ForceReference force);
    bool RefreshAreaOwnershipService();
    SceneryLayoutReference? ReadSceneryLayoutReference();
    byte? ReadHexScenerySlot(HexReference hex);
    SceneryReference? ReadSceneryTableReference(int slot);
    bool? ReadSceneryNativeValidity(SceneryReference scenery);
    byte? ReadSceneryFlags47(SceneryReference scenery);
    void WriteSceneryLayoutDirty(SceneryLayoutReference layout, int value);
}

/// <summary>
/// Complete consumed effects of 2406F0: force14, conditional area-count service,
/// and current scenery layout dirty state. This does not execute all of 330D80.
/// </summary>
public static class HexOwnership
{
    public static void Apply(HexReference hex, int requestedForce, IHexOwnershipState state)
    {
        ArgumentNullException.ThrowIfNull(hex); ArgumentNullException.ThrowIfNull(state);
        if (hex.PhysicalSlot is not int physical || physical is < 0 or >= 48400)
            throw new InvalidDataException("Current hex receiver is null or outside the supported native profile.");
        byte force = (byte)Math.Clamp(requestedForce, 0, 51);
        byte surface = Known(state.ReadHexSurfaceCode(hex), "hex surface10");
        state.WriteHexForceSlot(hex, force);
        if (surface is >= 15 and <= 18 && force is >= 1 and <= 51)
        {
            ForceReference player = Pointer(state.ReadCurrentPlayerForce(), "player force");
            if (!player.PhysicalSlot.HasValue)
                throw new InvalidDataException("Current player force is null; native slot12 would be unsafe.");
            int identity = Known(state.ReadForcePhysicalSlot(player), "player physical identity");
            if (identity is < 0 or >= 52)
                throw new InvalidDataException("Current player force identity is outside the supported CE profile.");
            if (Known(state.ReadHexForceSlot(hex), "current hex force14") == identity)
                state.RefreshAreaOwnershipService();
        }

        SceneryLayoutReference layout = Pointer(state.ReadSceneryLayoutReference(), "scenery layout");
        if (!layout.Identity.HasValue) return;
        byte scenerySlot = Known(state.ReadHexScenerySlot(hex), "hex scenery1E");
        SceneryReference scenery = Pointer(state.ReadSceneryTableReference(scenerySlot <= 100 ? scenerySlot : 0), "scenery");
        if (!Known(state.ReadSceneryNativeValidity(scenery), "scenery validity")) return;
        scenerySlot = Known(state.ReadHexScenerySlot(hex), "current hex scenery1E");
        scenery = Pointer(state.ReadSceneryTableReference(scenerySlot <= 100 ? scenerySlot : 0), "current scenery");
        if (!scenery.PhysicalSlot.HasValue)
            throw new InvalidDataException("Current scenery is null; native flags47 read would be unsafe.");
        if ((Known(state.ReadSceneryFlags47(scenery), "current scenery flags47") & 1) == 0) return;
        layout = Pointer(state.ReadSceneryLayoutReference(), "current scenery layout");
        if (!layout.Identity.HasValue)
            throw new InvalidDataException("Current scenery layout became null before native dirty write.");
        state.WriteSceneryLayoutDirty(layout, 1);
    }

    private static T Known<T>(T? value, string field) where T : struct => value
        ?? throw new InvalidDataException($"Current hex ownership {field} is unknown.");
    private static T Pointer<T>(T? value, string field) where T : class => value
        ?? throw new InvalidDataException($"Current hex ownership {field} pointer is unknown.");
}
