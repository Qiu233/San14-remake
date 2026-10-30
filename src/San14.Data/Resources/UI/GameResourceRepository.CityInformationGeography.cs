using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class GameResourceRepository
{
    //The existing growth reader already owns the twelve Region display
    //names. This normal fixed-resource field is their separate sorting text.
    internal IReadOnlyList<string> LoadCityInformationRegionSortText(string relativePath)
    {
        var (bytes, _) = ReadSource(relativePath);
        var document = S14Reader.Read(bytes, false);
        var payload = document.DecodedPayload;
        if (document.FormatMarker != 0x66 || payload.Length < 248 ||
            BinaryPrimitives.ReadInt32LittleEndian(payload) != 0x66 ||
            BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(4)) != 12)
            throw new InvalidDataException("city-information.Region_definition_format");
        return Array.AsReadOnly(Enumerable.Range(0, 12).Select(slot =>
            S14ForceNameDefinitionReader.ReadLiteralUnits(payload.AsSpan(8 + slot * 20 + 6, 14))).ToArray());
    }
}
