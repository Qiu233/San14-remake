using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Reflection.PortableExecutable;

namespace San14.Data.Formats;

public sealed record OriginalBaseParameterCatalog(IReadOnlyDictionary<int, int> RawValues, string ProfileId)
{
    /// <summary>Stable semantic key to physical RVA in the selected executable.</summary>
    public IReadOnlyDictionary<int, int> PhysicalRvas { get; init; } =
        new ReadOnlyDictionary<int, int>(new Dictionary<int, int>());
    public int? RuntimeTroopsExtraInitial { get; init; }
    public int? RuntimeTroopsExtraPhysicalRva { get; init; }
}

public static class OriginalBaseParameterReader
{
    private const string LegacySc = "2CAE6D6ACB9FF362CAA19162A6158AFDB73E1A3E957A6347ED52AD902EB8AA3B";
    private const string CurrentSc = "CEEE6A94EB939B40C329B8EC5AECD938E9D3A51A6663E8E7D27A1498627AFB88";
    private const string CurrentTc = "0751B4FED7A5783371B9541674C0587A6AFD3140E60E706E32EAF35A9432F464";

    // Keys identify the 19 already materialized parameter meanings. They were
    // originally SC RVAs; PhysicalRvas records where each version stores them.
    public static IReadOnlyList<int> ParameterRvas { get; } = Array.AsReadOnly(new[]
    {
        0x1A4BC98, 0x1A4BCA4, 0x1A4BCB0, 0x1A4BCB8,
        0x1A4C1FC, 0x1A4C208, 0x1A4C214, 0x1A4C228, 0x1A4C234, 0x1A4C240,
        0x1A4C248, 0x1A4C254, 0x1A4C260, 0x1A4B79C, 0x1A4B7A8,
        0x1344C4C, 0x1A4C644, 0x1A4C650, 0x1A4C65C
    });

    /// <summary>The repository supplies the hash from its existing source read;
    /// this reader does not hash the same executable a second time.</summary>
    public static OriginalBaseParameterCatalog Read(byte[] executable, string sourceSha256)
    {
        ArgumentNullException.ThrowIfNull(executable);
        string profileId = sourceSha256.ToUpperInvariant() switch
        {
            LegacySc => "CE-SC-2CAE6D6A-base-post-load",
            CurrentSc => "CE-SC-CEEE6A94-base-post-load",
            CurrentTc => "CE-TC-0751B4FE-base-post-load",
            _ => throw new InvalidDataException("Unsupported executable profile for original base parameters.")
        };
        using var stream = new MemoryStream(executable, false);
        using var pe = new PEReader(stream);
        var values = new Dictionary<int, int>();
        var physicalRvas = new Dictionary<int, int>();
        foreach (int key in ParameterRvas)
        {
            int physical = sourceSha256.Equals(CurrentTc, StringComparison.OrdinalIgnoreCase)
                ? key - (key == 0x1344C4C ? 0x1000 : 0x1030) : key;
            physicalRvas.Add(key, physical);
            values.Add(key, BinaryPrimitives.ReadInt32LittleEndian(
                pe.GetSectionData(physical).GetContent(0, 4).AsSpan()));
        }
        if (profileId != "CE-SC-2CAE6D6A-base-post-load")
        {
            int[] expected = [250000, 500000, 300000, 9999, 50000, 100000, 50000,
                20000, 50000, 100000, 2000, 1000, 1, 100, 1, 0x42C80000, 75, 50, 25];
            if (!ParameterRvas.Select(key => values[key]).SequenceEqual(expected))
                throw new InvalidDataException("Current base parameter source values differ from the verified profile.");
        }
        int? initialExtra = null, physicalExtra = null;
        if (profileId != "CE-SC-2CAE6D6A-base-post-load")
        {
            physicalExtra = sourceSha256.Equals(CurrentTc, StringComparison.OrdinalIgnoreCase) ? 0x27D3830 : 0x27D4890;
            var section = pe.PEHeaders.SectionHeaders.Single(item => physicalExtra >= item.VirtualAddress &&
                (long)physicalExtra + 4 <= (long)item.VirtualAddress + item.VirtualSize);
            //This global lies in the zero-initialized virtual tail of .data.
            //The owned new allocation uses that loader value once; it is not
            //a read or repair of an already-running game's current scalar.
            if (physicalExtra < (long)section.VirtualAddress + section.SizeOfRawData)
                throw new InvalidDataException("Base runtime troop addend is not in its inspected zero-initialized PE tail.");
            initialExtra = 0;
        }
        return new(new ReadOnlyDictionary<int, int>(values), profileId)
        {
            PhysicalRvas = new ReadOnlyDictionary<int, int>(physicalRvas),
            RuntimeTroopsExtraInitial = initialExtra,
            RuntimeTroopsExtraPhysicalRva = physicalExtra
        };
    }
}
