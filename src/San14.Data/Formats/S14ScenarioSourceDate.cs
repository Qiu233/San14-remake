using System.Buffers.Binary;

namespace San14.Data.Formats;

/// <summary>A five-byte static source date. No mapping to the simulation clock is implied.</summary>
public sealed record S14ScenarioSourceDate(int SourceOffset, short RawYear, sbyte RawMonth,
    sbyte RawDay, sbyte RawSubdayPart, byte[] RawBytes)
{
    public static S14ScenarioSourceDate Read(ReadOnlySpan<byte> world, int offset)
    {
        if (offset < 0 || offset > world.Length - 5)
            throw new InvalidDataException("Static world date is outside its source bytes.");
        ReadOnlySpan<byte> date = world.Slice(offset, 5);
        return new(offset, BinaryPrimitives.ReadInt16LittleEndian(date),
            unchecked((sbyte)date[2]), unchecked((sbyte)date[3]), unchecked((sbyte)date[4]), date.ToArray());
    }
}
