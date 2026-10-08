namespace San14.Data;

public sealed partial class OriginalEventInitializationContext
{
    /// <summary>Base virtual80: City22B9C0 or Gate22BBF0. The Gate
    /// implementation calls its current virtual78 before indexing District.</summary>
    public OriginalDistrictReference? ReadBaseDistrictReference(OriginalBaseReference target)
    {
        if (target.Kind == OriginalBaseKind.City) return ReadCityDistrictReference(target);
        int? raw = ReadBaseDistrictSlot(target);
        return raw is int slot ? ReadDistrictTableReference(slot is >= 0 and < 52 ? slot : 0) : null;
    }
}
