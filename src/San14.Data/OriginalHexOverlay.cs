namespace San14.Data;

public interface IOriginalHexOverlayState
{
    OriginalObjectReference? ReadObjectTableReference(int slot);
    bool? ReadObjectNativeValidity(OriginalObjectReference reference);
    ushort? ReadObjectHexIndex(OriginalObjectReference reference);
    OriginalSceneryReference? ReadSceneryTableReference(int slot);
    bool? ReadSceneryNativeValidity(OriginalSceneryReference reference);
    ushort? ReadSceneryHexIndex(OriginalSceneryReference reference);
    int? ReadHexPhysicalSlot(OriginalHexReference reference);
    void WriteHexObjectLink(OriginalHexReference reference, ushort value);
    void WriteHexSceneryLink(OriginalHexReference reference, byte value);
}

/// <summary>
/// Complete240C20/240DF0 local setters. Checked calls retain current pointer
/// aliases and re-read object/scenery position after validity. Unchecked calls
/// write the requested low bits directly. Neither setter changes ownership14.
/// </summary>
public static class OriginalHexOverlay
{
    public static bool AssignObject(OriginalHexReference hex, int requestedObject, int checkPosition,
        IOriginalHexOverlayState state)
    {
        ArgumentNullException.ThrowIfNull(hex); ArgumentNullException.ThrowIfNull(state);
        if (checkPosition != 0)
        {
            if (requestedObject < 0)
                throw new InvalidDataException("Negative checked object ID would index before the native pointer table.");
            OriginalObjectReference obj = state.ReadObjectTableReference(requestedObject <= 3000 ? requestedObject : 0)
                ?? throw new InvalidDataException("Current overlay object pointer is unknown.");
            if (!Known(state.ReadObjectNativeValidity(obj), "object validity")) return false;
            int physical = Physical(hex, state);
            if (Known(state.ReadObjectHexIndex(obj), "current object hex10") != physical) return false;
        }
        state.WriteHexObjectLink(hex, unchecked((ushort)requestedObject));
        return true;
    }

    public static bool AssignScenery(OriginalHexReference hex, int requestedScenery, int checkPosition,
        IOriginalHexOverlayState state)
    {
        ArgumentNullException.ThrowIfNull(hex); ArgumentNullException.ThrowIfNull(state);
        if (checkPosition != 0)
        {
            if (requestedScenery < 0)
                throw new InvalidDataException("Negative checked scenery ID would index before the native pointer table.");
            OriginalSceneryReference scenery = state.ReadSceneryTableReference(requestedScenery <= 100 ? requestedScenery : 0)
                ?? throw new InvalidDataException("Current overlay scenery pointer is unknown.");
            if (!Known(state.ReadSceneryNativeValidity(scenery), "scenery validity")) return false;
            int physical = Physical(hex, state);
            if (Known(state.ReadSceneryHexIndex(scenery), "current scenery hex44") != physical) return false;
        }
        state.WriteHexSceneryLink(hex, unchecked((byte)requestedScenery));
        return true;
    }

    private static int Physical(OriginalHexReference hex, IOriginalHexOverlayState state)
    {
        int physical = Known(state.ReadHexPhysicalSlot(hex), "hex physical identity");
        if (physical is < 0 or >= 48400)
            throw new InvalidDataException("Overlay hex identity is outside the supported native profile.");
        return physical;
    }
    private static T Known<T>(T? value, string field) where T : struct => value
        ?? throw new InvalidDataException($"Current overlay {field} is unknown.");
}
