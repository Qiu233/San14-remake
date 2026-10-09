namespace San14.Data;

/// <summary>Exact D5BDA0 reader for the original fifteen uint16 primitive
/// sort fields. This decodes an actual caller descriptor; it does not select
/// one, calculate native keys, sort primitives or certify a source order.</summary>
public static class WorldPrimitiveSortDescriptor
{
    public static IReadOnlyList<int> ReadActualFields(ReadOnlySpan<ushort> actualDescriptor)
    {
        if (actualDescriptor.Length != 15)
            throw new InvalidDataException("Original primitive sorting needs the complete fifteen-word current descriptor.");
        uint members = 0, successors = 0;
        int count = 0;
        for (int field = 0; field < 15; field++)
        {
            if ((actualDescriptor[field] >> 13) == 0) continue;
            int next = (actualDescriptor[field] >> 8) & 31;
            if (next > 15) throw new InvalidDataException("Original primitive descriptor has an unsupported successor field.");
            members |= 1u << field; successors |= 1u << next; count++;
        }
        if (count == 0) return Array.Empty<int>();
        uint difference = members ^ successors;
        uint starts = difference & 0x7FFF;
        if (System.Numerics.BitOperations.PopCount(starts) != 1 || (difference & 0x8000) == 0)
            throw new InvalidDataException("Original primitive descriptor has no unique finite chain ending at field15.");
        int current = System.Numerics.BitOperations.TrailingZeroCount(starts);
        var result = new int[count]; uint reached = 0;
        for (int ordinal = 0; ordinal < count; ordinal++)
        {
            if (current >= 15 || (members & (1u << current)) == 0 || (reached & (1u << current)) != 0)
                throw new InvalidDataException("Original primitive descriptor has a cycle, disabled successor or short chain.");
            reached |= 1u << current; result[ordinal] = current;
            current = (actualDescriptor[current] >> 8) & 31;
        }
        if (current != 15 || reached != members)
            throw new InvalidDataException("Original primitive descriptor does not cover every actual enabled sort field.");
        return Array.AsReadOnly(result);
    }
}
