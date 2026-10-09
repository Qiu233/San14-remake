namespace San14.Data;

public interface IAreaHexIteratorState
{
    int? ReadAreaPhysicalSlot(AreaReference area);
    HexReference? ReadHexTableReference(int slot);
    ushort? ReadHexAreaSlot(HexReference hex);
}

/// <summary>Shared current2312E0/3339F0 selector. Callers retain begin/end read order.</summary>
public static class AreaHexIterator
{
    public static int FindNextIndex(int start, int capturedAreaPhysical, IAreaHexIteratorState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (start is < 0 or > 48400)
            throw new InvalidDataException("Area hex iterator cursor is outside the supported native table.");
        // 3339F0 rejects the sentinel key before reading any hex payload.
        if (capturedAreaPhysical is < 1 or > 500) return 48400;
        for (int slot = start; slot < 48400; slot++)
        {
            HexReference hex = state.ReadHexTableReference(slot)
                ?? throw new InvalidDataException($"Current area hex iterator table lookup {slot} is unknown.");
            ushort area = state.ReadHexAreaSlot(hex)
                ?? throw new InvalidDataException("Current area hex iterator raw12 is unknown.");
            if (area == capturedAreaPhysical) return slot;
        }
        return 48400;
    }
}
