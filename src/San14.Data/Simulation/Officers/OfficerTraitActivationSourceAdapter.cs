using San14.Core;

namespace San14.Data;

/// <summary>Translates the source's activation bitmap and duration vector at
/// the current-world boundary. The domain owner does not use this layout.</summary>
internal static class OfficerTraitActivationSourceAdapter
{
    public const int TraitSlotCount = 9;
    private const int ActivationBitCount = 16;

    public static OfficerTraitActivation Read(SourceWorldContext source, int physical)
    {
        ushort? mask = source.ReadPersonTraitMask1BC(physical);
        ushort?[] durations = Enumerable.Range(0, TraitSlotCount)
            .Select(index => source.ReadPersonTraitExtra1BE(physical, index)).ToArray();
        return new(ReadSlots(mask), durations.All(value => value.HasValue)
            ? durations.Select(value => (int)value!.Value).ToArray() : null);
    }

    public static int[]? ReadSlots(ushort? mask) => mask is ushort bits
        ? Enumerable.Range(0, ActivationBitCount).Where(slot => (bits & (1 << slot)) != 0).ToArray()
        : null;

    public static ushort? Mask(OfficerTraitActivation? activation)
    {
        if (activation?.ActiveSlots is not { } slots) return null;
        ushort mask = 0;
        foreach (int slot in slots)
        {
            if (slot >= ActivationBitCount)
                throw new InvalidDataException("Trait slot is outside the source activation representation.");
            mask |= (ushort)(1 << slot);
        }
        return mask;
    }

    public static void Write(SourceWorldContext source, int physical, OfficerTraitActivation? activation)
    {
        source.SetPersonTraitMask1BC(physical, Mask(activation));
        if (activation?.DurationSlotCount is int count && count != TraitSlotCount)
            throw new InvalidDataException("Trait duration slots differ from the source representation.");
        for (int index = 0; index < TraitSlotCount; index++)
            source.SetPersonTraitExtra1BE(physical, index, activation?.RemainingDuration(index) is int value
                ? checked((ushort)value) : null);
    }
}
