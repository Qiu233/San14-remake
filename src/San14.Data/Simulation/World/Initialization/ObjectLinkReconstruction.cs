using San14.Data.Formats;

namespace San14.Data;

public enum ObjectLinkTargetKind { City, Gate }

public sealed record ObjectLinkWrite(ObjectLinkTargetKind Kind,
    int TargetPhysicalSlot, ushort ObjectPhysicalSlot);

/// <summary>
/// Only the proved 330D80/330ED9 post-load city/gate object-link write. The
/// caller supplies the CURRENT registered-object sequence and an explicit
/// current-target writer. This does not default source rows to current state,
/// reconstruct other map/list effects, or establish first-player readiness.
/// </summary>
public static class ObjectLinkReconstruction
{
    /// <summary>
    /// Preserves registry-list order and repeated objects. Nonzero staticFlag
    /// writes target word+4E before native target validity would be checked;
    /// raw city+69 is therefore not an eligibility gate here. The callback is
    /// responsible for known target pointers and mutation. Input/writer failure
    /// leaves earlier callback mutations intact; no rollback or fallback occurs.
    /// </summary>
    public static IReadOnlyList<ObjectLinkWrite> ApplyRegisteredObjects(
        IReadOnlyList<S14ConditionNativeObject> registeredObjects, int staticFlag,
        Action<ObjectLinkWrite> writeTarget)
    {
        if (registeredObjects is null)
            throw new InvalidDataException("Current registered-object sequence is unknown.");
        if (writeTarget is null)
            throw new InvalidDataException("Current object-link target writer is unknown.");
        var writes = new List<ObjectLinkWrite>();
        for (int index = 0; index < registeredObjects.Count; index++)
        {
            S14ConditionNativeObject? current = registeredObjects[index];
            if (current is null)
                throw new InvalidDataException($"Current registered object {index} is unknown.");
            int slot = current.PhysicalSlot;
            if (slot is < 0 or > 3000)
                throw new InvalidDataException($"Current registered object {index}, physical slot {slot}, is outside the supported native table.");
            if (slot == 0 || current.RawField10 >= 48400 || current.RawField12 == 0)
                continue;
            ObjectLinkTargetKind kind;
            int target;
            if (slot <= 51) { kind = ObjectLinkTargetKind.City; target = slot; }
            else if (slot <= 61) { kind = ObjectLinkTargetKind.Gate; target = slot - 51; }
            else continue;
            if (staticFlag == 0) continue;
            // Preserve the original signed clamp then low-byte zero extension.
            // The supported city/gate branches restrict slot to1..61, so the
            // conversion does not change it; it is not a general u16 copy rule.
            ushort link = unchecked((byte)Math.Clamp(slot, 0, 3000));
            var write = new ObjectLinkWrite(kind, target, link);
            writeTarget(write);
            writes.Add(write);
        }
        return writes.AsReadOnly();
    }
}
