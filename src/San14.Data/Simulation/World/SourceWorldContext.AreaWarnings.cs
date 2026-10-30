namespace San14.Data;

public sealed partial class SourceWorldContext
{
    // Area73 is independent of the representative Hex owner. Null retains
    // a legacy unknown input; zero is the source-proven reset value.
    private readonly Dictionary<int, byte?> _areaOccupationClaimForceSlots = [];

    public void SetAreaOccupationClaimForceSlot(int physicalSlot, byte? value)
    {
        Check(physicalSlot, 501, "Area occupation-claim receiver");
        _areaOccupationClaimForceSlots[physicalSlot] = value;
    }

    public byte? ReadAreaOccupationClaimForceSlot(AreaReference area) =>
        _areaOccupationClaimForceSlots.GetValueOrDefault(
            MapPhysical(area.PhysicalSlot, 501, "Area occupation-claim receiver"));

    public void WriteAreaOccupationClaimForceSlot(AreaReference area, byte value) =>
        SetAreaOccupationClaimForceSlot(
            MapPhysical(area.PhysicalSlot, 501, "Area occupation-claim receiver"), value);
}
