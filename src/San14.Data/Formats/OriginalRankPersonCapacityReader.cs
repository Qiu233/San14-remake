using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;

namespace San14.Data.Formats;

public sealed record OriginalRankPersonCapacityCatalog(IReadOnlyList<int> Capacities,string ProfileId);

/// <summary>Rank capacity parameters from exact, separately verified CE executable profiles.</summary>
public static class OriginalRankPersonCapacityReader
{
    private const string LegacySc = "2CAE6D6ACB9FF362CAA19162A6158AFDB73E1A3E957A6347ED52AD902EB8AA3B";
    private const string CurrentSc = "CEEE6A94EB939B40C329B8EC5AECD938E9D3A51A6663E8E7D27A1498627AFB88";
    private const string CurrentTc = "0751B4FED7A5783371B9541674C0587A6AFD3140E60E706E32EAF35A9432F464";
    private const string CurrentTable = "EB13B50D0A233B9CCAE82ED2A5122621B6CB101FFBD83EB31269EA727BFB6AAA";

    public static OriginalRankPersonCapacityCatalog Read(byte[] executable)
    {
        ArgumentNullException.ThrowIfNull(executable);
        string hash = Convert.ToHexString(SHA256.HashData(executable));
        (int tableRva, string profileId, bool verifyTable) = hash switch
        {
            LegacySc => (0x1A4AB70, "CE-SC-2CAE6D6A-rank-person-capacity", false),
            CurrentSc => (0x1A4AB70, "CE-SC-CEEE6A94-rank-person-capacity", true),
            CurrentTc => (0x1A49B40, "CE-TC-0751B4FE-rank-person-capacity", true),
            _ => throw new InvalidDataException("Unsupported executable profile for original rank person capacities.")
        };
        using var stream = new MemoryStream(executable, false);
        using var pe = new PEReader(stream);
        byte[] raw = pe.GetSectionData(tableRva).GetContent(0, 84).ToArray();
        if (verifyTable && Convert.ToHexString(SHA256.HashData(raw)) != CurrentTable)
            throw new InvalidDataException("Current rank capacity source table differs from its verified bytes.");
        int Value(int index) => BinaryPrimitives.ReadInt32LittleEndian(raw.AsSpan(index * 12, 4));
        int common = Value(6);
        int[] values = [common, common, common, Value(5), Value(4), Value(3),
            Value(2), Value(1), Value(0), common, common, common, common];
        return new(Array.AsReadOnly(values), profileId);
    }
}
