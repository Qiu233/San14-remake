namespace San14.Data;

public interface IMapOverlayInitializationState : IHexOverlayState
{
    IReadOnlyList<ObjectReference?>? ReadRegisteredObjectReferences();
    int? ReadObjectPhysicalSlot(ObjectReference reference);
    HexReference? ReadHexTableReference(int slot);
    BaseReference? ReadCityTableReference(int slot);
    BaseReference? ReadGateTableReference(int slot);
    void WriteBaseObjectLink(BaseReference reference, ushort value);
    bool? ReadBaseNativeValidity(BaseReference reference);
    HexListNodeReference? ReadBaseHexListHead(BaseReference reference);
    HexReference? ReadHexListNodePayload(HexListNodeReference node);
    HexListNodeReference? ReadHexListNodeNext(HexListNodeReference node);
    void ApplyStaticOverlayOwnership(int staticFlag);
    IReadOnlyList<SceneryReference?>? ReadRegisteredSceneryReferences();
    int? ReadSceneryPhysicalSlot(SceneryReference reference);
}

/// <summary>
/// 330D80 against explicit current registered sequences and live base78 nodes:
/// objects and their footprints, the static ownership phase, then scenery links.
/// Registration, base-footprint construction and other32F910 phases remain external.
/// </summary>
public static class MapOverlayInitialization
{
    public static bool Apply(int staticFlag, IMapOverlayInitializationState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        ApplyObjects(staticFlag, state);
        if (staticFlag != 0) state.ApplyStaticOverlayOwnership(staticFlag);
        ApplySceneries(state);
        return true;
    }

    public static void ApplyObjects(int staticFlag, IMapOverlayInitializationState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        IReadOnlyList<ObjectReference?> objects = state.ReadRegisteredObjectReferences()
            ?? throw new InvalidDataException("Current registered object list is unknown.");
        foreach (ObjectReference? entry in objects)
        {
            ObjectReference obj = Pointer(entry, "registered object");
            if (!obj.PhysicalSlot.HasValue)
                throw new InvalidDataException("Registered object is null at the native raw10 read.");
            ushort rawHex = Known(state.ReadObjectHexIndex(obj), "object hex10");
            HexReference hex = Pointer(state.ReadHexTableReference(rawHex <= 48399 ? rawHex : 0), "object hex");
            if (!Known(state.ReadObjectNativeValidity(obj), "registered object validity")) continue;
            int savedPhysical = ObjectPhysical(obj, state);
            HexOverlay.AssignObject(hex, savedPhysical, 1, state);
            int categoryPhysical = ObjectPhysical(obj, state); //231A80 recomputes the current physical identity.
            BaseReference target;
            if (categoryPhysical is >= 1 and <= 51)
                target = Pointer(state.ReadCityTableReference(savedPhysical <= 51 ? savedPhysical : 0), "city");
            else
            {
                categoryPhysical = ObjectPhysical(obj, state); //232000 is a separate fresh query.
                if (categoryPhysical is < 52 or > 61) continue;
                int gate = savedPhysical is >= 52 and <= 61 ? savedPhysical - 51 : 0;
                target = Pointer(state.ReadGateTableReference(gate), "gate");
            }
            if (staticFlag != 0)
            {
                int currentPhysical = ObjectPhysical(obj, state);
                if (!target.PhysicalSlot.HasValue)
                    throw new InvalidDataException("Current base is null at the native static link4E write.");
                state.WriteBaseObjectLink(target, unchecked((byte)Math.Clamp(currentPhysical, 0, 3000)));
            }
            if (!target.PhysicalSlot.HasValue) continue;
            if (!Known(state.ReadBaseNativeValidity(target), "current base validity")) continue;
            HexListNodeReference node = Pointer(state.ReadBaseHexListHead(target), "base hex list head");
            while (node.Identity.HasValue)
            {
                HexListNodeReference head = Pointer(state.ReadBaseHexListHead(target), "current base hex list head");
                int checkPosition = node.Identity == head.Identity ? 1 : 0;
                int currentPhysical = ObjectPhysical(obj, state);
                HexReference member = Pointer(state.ReadHexListNodePayload(node), "base hex node payload");
                HexOverlay.AssignObject(member, currentPhysical, checkPosition, state);
                node = Pointer(state.ReadHexListNodeNext(node), "base hex node next");
            }
        }
    }

    public static void ApplySceneries(IMapOverlayInitializationState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        IReadOnlyList<SceneryReference?> sceneries = state.ReadRegisteredSceneryReferences()
            ?? throw new InvalidDataException("Current registered scenery list is unknown.");
        foreach (SceneryReference? entry in sceneries)
        {
            SceneryReference scenery = Pointer(entry, "registered scenery");
            if (!scenery.PhysicalSlot.HasValue) continue;
            if (!Known(state.ReadSceneryNativeValidity(scenery), "registered scenery validity")) continue;
            ushort rawHex = Known(state.ReadSceneryHexIndex(scenery), "current scenery hex44");
            HexReference hex = Pointer(state.ReadHexTableReference(rawHex <= 48399 ? rawHex : 0), "scenery hex");
            int physical = Known(state.ReadSceneryPhysicalSlot(scenery), "scenery physical identity");
            if (physical is < 0 or >= 101)
                throw new InvalidDataException("Scenery identity is outside the supported CE profile.");
            HexOverlay.AssignScenery(hex, physical, 1, state);
        }
    }

    private static int ObjectPhysical(ObjectReference obj, IMapOverlayInitializationState state)
    {
        int physical = Known(state.ReadObjectPhysicalSlot(obj), "object physical identity");
        if (physical is < 0 or >= 3001)
            throw new InvalidDataException("Object identity is outside the supported native profile.");
        return physical;
    }
    private static T Known<T>(T? value, string field) where T : struct => value
        ?? throw new InvalidDataException($"Current map overlay {field} is unknown.");
    private static T Pointer<T>(T? value, string field) where T : class => value
        ?? throw new InvalidDataException($"Current map overlay {field} pointer is unknown.");
}
