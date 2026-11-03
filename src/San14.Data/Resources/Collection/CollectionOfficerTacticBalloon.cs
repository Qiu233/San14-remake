using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class CollectionOfficerCatalog
{
    // The gallery keeps its real title-loaded row; campaign consumers supply
    // their independent current fields to the same pure content formatter.
    private TacticBalloonContent ReadTacticBalloon(S14TacticDefinition tactic)
    {
        var raw = tactic.RawBytes;
        if (raw.Length != 94 || raw[88] > 2)
            throw new InvalidDataException("collection.officer.tactic_balloon_definition");
        return TacticBalloonContentSource.Create(raw[88], raw[51],
            BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(49, 2)), raw[80], raw[81], Data);
    }
}
