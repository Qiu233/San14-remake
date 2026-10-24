using System.Buffers.Binary;

namespace San14.Data.Formats;

public sealed record S14ScenarioPreviewPerson(short PersonId, byte PositionIndex);

/// <summary>Serialized scenario-list inputs. Resource presence, application
/// unlocks, completion and person capabilities are resolved by their owners.</summary>
public sealed record S14ScenarioSelectionMetadata(byte NativeId, byte FilterKind,
    byte ScenarioKindCode, byte SortSubtypeCode, int? EditionCode,
    IReadOnlyList<S14ScenarioPreviewPerson> PreviewPeople)
{
    internal static S14ScenarioSelectionMetadata Read(ReadOnlySpan<byte> source, uint format)
    {
        // ReadHeader has validated the supported outer header and payload
        // boundary. These are file offsets, independent of native row layout.
        var people = new S14ScenarioPreviewPerson[3];
        for (int index = 0; index < people.Length; index++)
            people[index] = new(BinaryPrimitives.ReadInt16LittleEndian(source.Slice(0x1D7 + index * 2, 2)),
                source[0x1DD + index]);
        // Base-format headers do not serialize the later edition field. Keep
        // that absence distinct from a serialized edition value of zero.
        int? edition = format > 0x56 ? BinaryPrimitives.ReadInt32LittleEndian(source.Slice(0x1E0, 4)) : null;
        return new(source[0x1CE], source[0x1CA], source[0x1D3], source[0x1D4], edition,
            Array.AsReadOnly(people));
    }
}
