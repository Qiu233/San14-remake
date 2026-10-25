using System.Buffers.Binary;

namespace San14.Data.Formats;

/// <summary>CE fixed section36.23D670 reads four literal name units,
/// six sort units and five independent definition bytes per country.</summary>
internal static class S14ForeignCountryReader
{
    internal static IReadOnlyList<string> ReadNames(S14Document document)
    {
        const int header = 355076, start = header + 8, stride = 25, count = 5;
        ReadOnlySpan<byte> bytes = document.DecodedPayload;
        int end = start + stride * count;
        if (document.FormatMarker != 0x66 || bytes.Length < end + 8 ||
            BinaryPrimitives.ReadInt32LittleEndian(bytes[header..]) != 36 ||
            BinaryPrimitives.ReadInt32LittleEndian(bytes[(header + 4)..]) != count ||
            BinaryPrimitives.ReadInt32LittleEndian(bytes[end..]) != 37 ||
            BinaryPrimitives.ReadInt32LittleEndian(bytes[(end + 4)..]) != 9)
            throw new InvalidDataException("Foreign countries require the CE section36/5/25-byte definition profile.");
        var names = new string[count];
        for (int slot = 0; slot < count; slot++)
            names[slot] = S14ForceNameDefinitionReader.ReadLiteralUnits(bytes.Slice(start + slot * stride, 8));
        return Array.AsReadOnly(names);
    }
}
