using System.Buffers.Binary;

namespace San14.Data.Formats;

/// <summary>CE329 Force personnel fields at the normal resource boundary.
/// Shared by the fresh saved owner and the pre-start read-only page. This
/// decoder neither creates a simulation bank nor changes a GameState.</summary>
internal sealed record ForcePolicyPersonnelFields(ushort Ruler, ushort Advisor,
    ushort Field16A, ushort AdvisorPending, IReadOnlyList<ForcePolicyDepartmentFields> Departments)
{
    internal static ForcePolicyPersonnelFields Read(ForcePreviewRecord row)
    {
        if (row.RemainingBytesHex.Length != 327 * 2)
            throw new InvalidDataException("CE Force personnel requires its complete329-byte normal row.");
        byte[] tail;
        try { tail = Convert.FromHexString(row.RemainingBytesHex); }
        catch (FormatException error) { throw new InvalidDataException("CE Force source bytes are malformed.", error); }
        var bytes = new byte[329];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, row.RulerSlot);
        tail.CopyTo(bytes, 2);
        var groups = new ForcePolicyDepartmentFields[5];
        for (int group = 0; group < groups.Length; group++)
        {
            var members = new ushort[7]; var pending = new ushort[7];
            for (int ordinal = 0; ordinal < members.Length; ordinal++)
            {
                members[ordinal] = Word(61 + group * 29 + ordinal * 2);
                pending[ordinal] = Word(75 + group * 29 + ordinal * 2);
            }
            groups[group] = new(Array.AsReadOnly(members), Array.AsReadOnly(pending), bytes[89 + group * 29]);
        }
        return new(row.RulerSlot, Word(59), Word(322), Word(324), Array.AsReadOnly(groups));
        ushort Word(int at) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(at, 2));
    }

    //Only the government page copies through6C00C0, which clamps the advisor
    //reference to6000. The installed saved owner retains the actual raw word.
    internal ForcePolicyPersonnelFields ForGovernmentPage() => this with { Advisor = Math.Min(Advisor, (ushort)6000) };
}

internal sealed record ForcePolicyDepartmentFields(IReadOnlyList<ushort> Members,
    IReadOnlyList<ushort> PendingValues, byte Expansion);
