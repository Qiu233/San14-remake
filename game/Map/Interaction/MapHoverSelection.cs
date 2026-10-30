using San14.Data;

internal enum MapHoverRayStatus { Unavailable, NoWrite, Ready }
internal readonly record struct MapHoverRayResult(MapHoverRayStatus Status, int? Slot = null);

/// <summary>SceneView's current hover slots. They are independent of the
/// command selection and are initialized only with this actual view.</summary>
internal sealed class MapHoverSelection
{
    internal int RawHex { get; private set; } = 48400;
    internal int SelectedHex { get; private set; } = 48400;

    internal SceneryMapHexSelection Clear()
    {
        RawHex = SelectedHex = 48400;
        return new(true, null);
    }

    internal static bool IsFrozen(int activeButton,uint heldFrames,int pressX,int pressY,int currentX,int currentY)
    {
        if(activeButton==-1)return false;
        int x=unchecked(currentX-pressX),y=unchecked(currentY-pressY);
        return heldFrames>15||unchecked(x*x+y*y)>100;
    }

    internal SceneryMapHexSelection Update(SourceWorldContext current, bool? frozen, int? auxiliary,
        Func<MapHoverRayResult> readRay, Func<HexReference?, bool?> allow)
    {
        if (frozen is null) return new(false, null);
        if (frozen.Value) return ReadSelected(current);
        if (auxiliary is null) return new(false, null);
        int? slot;
        if ((uint)auxiliary.Value < 48400) slot = auxiliary;
        else
        {
            var ray = readRay();
            if (ray.Status == MapHoverRayStatus.Unavailable) return new(false, null);
            if (ray.Status == MapHoverRayStatus.NoWrite) return ReadSelected(current);
            slot = ray.Slot;
        }
        HexReference? candidate = null;
        if (slot is int key)
        {
            var pointer = current.ReadHexTableReference(key);
            if (pointer is null) return new(false, null);
            if (pointer.PhysicalSlot is not null) candidate = pointer;
        }
        RawHex = candidate?.PhysicalSlot ?? 48400;
        bool? permitted = allow(candidate);
        if (permitted is null) return new(false, null);
        SelectedHex = permitted.Value ? RawHex : 48400;
        return ReadSelected(current);
    }

    private SceneryMapHexSelection ReadSelected(SourceWorldContext current)
    {
        if ((uint)SelectedHex >= 48400) return new(true, null);
        // The source slot reader looks up the stored physical index again.
        var pointer = current.ReadHexTableReference(SelectedHex);
        return pointer is null ? new(false, null) : new(true, pointer);
    }
}
