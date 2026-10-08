using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;

namespace San14.Data;

public sealed record OriginalAudioSelectorQuery(int ReturnValue, int OutValue)
{
    public bool SucceededWithNonzeroValue => ReturnValue != 0 && OutValue != 0;
}

/// <summary>Raw input to the original conditional ruler-definition branch. The field's
/// game meaning is deliberately not assigned before its native definition reader is closed.</summary>
public sealed record OriginalAudioRulerSlotInput(bool UseDefinitionBranch,
    bool RulerObjectIsValid, int? RawDefinitionIndex);
public sealed record OriginalAudioMusicSourceSlot(bool ShouldSubmit, int? SourceSlot,
    OriginalAudioRulerSlotInput Input);
public sealed record OriginalAudioMusicTableSource(SourceProvenance Source, int Rva, long FileOffset);
/// <summary>CueHash is the stock identity-map table result. An explicit native mapping
/// entry can change the commit request; use PrepareCommit when that mapping is supplied.</summary>
public sealed record OriginalAudioPreparedMusicSelection(int SourceSlot, sbyte SignedSetting,
    int ConfiguredMusicIndex, int EffectiveMusicIndex, bool UsedSettingSentinel,
    bool UsedQueryFallback, bool CanQueue, uint? CueHash,
    OriginalAudioSelectorQuery FirstQuery, OriginalAudioSelectorQuery? IndexedQuery);
public sealed record OriginalAudioMusicCueMapping(int RawKind, int RawTableRowIndex);
public sealed record OriginalAudioPreparedMusicCommit(bool ShouldCommit, int MusicIndex,
    uint? RequestOpcode, uint? CueHash, OriginalAudioMusicCueMapping? Mapping);

/// <summary>
/// Ports the verified original source-slot, signed-setting and query-result branches as a
/// pure prepared selection. Tables are read from a hash-pinned SC/TC executable; no native
/// function is called. It does not infer the caller's current scenario, ruler definition,
/// season, custom music remap, playback state, gain or audio mixing.
/// </summary>
public sealed class OriginalAudioMapMusicSelector
{
    public const int SourceSlotCount = 28;
    public const int MusicIndexCount = 117;
    public const string DefaultTableSha256 = "DC27F8E8EE2D08A2BCCBB2DBD6C8E07FFBA194101FC3458C34DED55F72C0971A";
    public const string CueHashTableSha256 = "30CCE133EAA1D37CECC946A6900E578C4FADDE4FEC0ABB9B2BD71CFFCFA8EEAD";
    public IReadOnlyList<byte> DefaultMusicIndices { get; }
    public IReadOnlyList<uint> CueHashes { get; }
    public SourceProvenance ExecutableSource { get; }
    public OriginalAudioMusicTableSource DefaultTableSource { get; }
    public OriginalAudioMusicTableSource CueHashTableSource { get; }

    private OriginalAudioMapMusicSelector(byte[] defaults, uint[] hashes, SourceProvenance executable,
        OriginalAudioMusicTableSource defaultSource, OriginalAudioMusicTableSource hashSource)
    {
        DefaultMusicIndices = Array.AsReadOnly(defaults);
        CueHashes = Array.AsReadOnly(hashes);
        ExecutableSource = executable;
        DefaultTableSource = defaultSource;
        CueHashTableSource = hashSource;
    }

    public static OriginalAudioMapMusicSelector Load(string sourceDirectory, string locale)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
        ValidateLocale(locale);
        string path = Path.GetFullPath(Path.Combine(sourceDirectory, $"SAN14CE_{locale}.exe"));
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            128 * 1024, FileOptions.SequentialScan);
        if (stream.Length is < 4096 or > 128 * 1024 * 1024)
            throw new InvalidDataException("Music selector executable exceeds the supported size bound.");
        byte[] executable = new byte[(int)stream.Length]; stream.ReadExactly(executable);
        return ReadExecutable(executable, locale, path);
    }

    public static OriginalAudioMapMusicSelector ReadExecutable(byte[] executable, string locale, string physicalPath)
    {
        ArgumentNullException.ThrowIfNull(executable);
        ArgumentException.ThrowIfNullOrWhiteSpace(physicalPath);
        ValidateLocale(locale);
        if (executable.Length is < 4096 or > 128 * 1024 * 1024)
            throw new InvalidDataException("Music selector executable exceeds the supported size bound.");
        (string expected, int defaultRva, int hashRva) = locale switch
        {
            "SC" => ("CEEE6A94EB939B40C329B8EC5AECD938E9D3A51A6663E8E7D27A1498627AFB88", 0x13A6B50, 0x13D2940),
            "TC" => ("0751B4FED7A5783371B9541674C0587A6AFD3140E60E706E32EAF35A9432F464", 0x13A5B50, 0x13D1B20),
            _ => throw new ArgumentOutOfRangeException(nameof(locale))
        };
        string exeSha = Convert.ToHexString(SHA256.HashData(executable));
        if (exeSha != expected)
            throw new InvalidDataException("Music selector executable differs from its verified source revision.");
        string file = $"SAN14CE_{locale}.exe", path = Path.GetFullPath(physicalPath);
        var exeSource = new SourceProvenance(file, executable.LongLength, exeSha, expected)
            { PhysicalPath = path, ResourceUri = GameResourceId.File(file).ToString() };
        using var pe = new PEReader(new MemoryStream(executable, writable: false));
        var optional = pe.PEHeaders.PEHeader;
        if (optional is null || optional.Magic != PEMagic.PE32Plus || optional.ImageBase != 0x140000000UL ||
            pe.PEHeaders.CoffHeader.Machine != Machine.Amd64)
            throw new InvalidDataException("Music selector requires its verified AMD64 PE32+ source.");
        (byte[] Bytes, OriginalAudioMusicTableSource Source) Table(int rva, int count, string expectedSha, string role)
        {
            var sections = pe.PEHeaders.SectionHeaders.Where(s => rva >= s.VirtualAddress &&
                (long)rva + count <= (long)s.VirtualAddress + s.SizeOfRawData).ToArray();
            if (sections.Length != 1 || sections[0].Name != ".rdata")
                throw new InvalidDataException("Music selector table is outside one original .rdata range.");
            long offset = (long)sections[0].PointerToRawData + rva - sections[0].VirtualAddress;
            if (offset < 0 || offset > executable.Length - count)
                throw new InvalidDataException("Music selector table exceeds executable bytes.");
            byte[] bytes = executable.AsSpan((int)offset, count).ToArray();
            string sha = Convert.ToHexString(SHA256.HashData(bytes));
            if (sha != expectedSha) throw new InvalidDataException("Music selector table source bytes changed.");
            var provenance = new SourceProvenance(file, count, sha, expectedSha)
                { PhysicalPath = path, ResourceUri = GameResourceId.File(file) + $"#rva-{rva:X}/music-selector/{role}" };
            return (bytes, new(provenance, rva, offset));
        }
        var defaults = Table(defaultRva, SourceSlotCount, DefaultTableSha256, "source-defaults");
        var rawHashes = Table(hashRva, MusicIndexCount * 4, CueHashTableSha256, "cue-hashes");
        uint[] hashes = new uint[MusicIndexCount];
        for (int i = 0; i < hashes.Length; i++)
            hashes[i] = BinaryPrimitives.ReadUInt32LittleEndian(rawHashes.Bytes.AsSpan(i * 4, 4));
        return new(defaults.Bytes, hashes, exeSource, defaults.Source, rawHashes.Source);
    }

    /// <summary>TC404B50/SC paired caller: false uses slot 6; invalid ruler suppresses the
    /// submission; valid raw indices 1..6 choose slots 9..14, otherwise slot 6.</summary>
    public static OriginalAudioMusicSourceSlot SelectSourceSlot(OriginalAudioRulerSlotInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!input.UseDefinitionBranch) return new(true, 6, input);
        if (!input.RulerObjectIsValid) return new(false, null, input);
        if (input.RawDefinitionIndex is not int raw)
            throw new ArgumentException("A valid ruler definition branch needs its explicit raw definition index.", nameof(input));
        return new(true, raw is >= 1 and <= 6 ? raw + 8 : 6, input);
    }

    /// <summary>TC4058B0 uses the original world-month season getter: raw seasons 0..3
    /// select source slots 2..5; an out-of-range raw result retains source slot 2.</summary>
    public static int SelectSeasonSourceSlot(int rawSeasonIndex) => rawSeasonIndex switch
        { 1 => 3, 2 => 4, 3 => 5, _ => 2 };

    /// <summary>The caller supplies the month consumed by TC32EC20/SC32EE80. This does
    /// not choose a scenario's calendar field or infer an active original process date.</summary>
    public static int SelectMonthSourceSlot(int sourceMonth) =>
        SelectSeasonSourceSlot(OriginalMapSeasonMath.SelectMonth(sourceMonth).SeasonIndex);

    /// <summary>
    /// Native TC21E860 takes a signed byte at settings+1C+slot; only -1 is the default
    /// sentinel. TC404890 requires first query return/out both nonzero; indices 28..115
    /// additionally require the indexed query return/out both nonzero. Failure chooses
    /// the source default. TC400F20 rejects indices outside 0..116 without clamping.
    /// </summary>
    public OriginalAudioPreparedMusicSelection PrepareFromSlot(int sourceSlot, sbyte signedSetting,
        OriginalAudioSelectorQuery firstQuery, OriginalAudioSelectorQuery? indexedQuery = null)
    {
        if (sourceSlot is < 0 or >= SourceSlotCount) throw new ArgumentOutOfRangeException(nameof(sourceSlot));
        ArgumentNullException.ThrowIfNull(firstQuery);
        bool sentinel = signedSetting == -1;
        int configured = sentinel ? DefaultMusicIndices[sourceSlot] : signedSetting;
        bool fallback = !firstQuery.SucceededWithNonzeroValue;
        if (!fallback && configured is >= 28 and <= 115)
        {
            if (indexedQuery is null)
                throw new ArgumentException("This source music index requires explicit indexed query results.", nameof(indexedQuery));
            fallback = !indexedQuery.SucceededWithNonzeroValue;
        }
        int effective = fallback ? DefaultMusicIndices[sourceSlot] : configured;
        bool canQueue = effective is >= 0 and < MusicIndexCount;
        return new(sourceSlot, signedSetting, configured, effective, sentinel, fallback, canQueue,
            canQueue ? CueHashes[effective] : null, firstQuery, indexedQuery);
    }

    /// <summary>TC403860 builds the stock map as music-index keys with {kind=0,row=index}.
    /// This returns that verified initialization entry, not a mutable original mapping read.</summary>
    public static OriginalAudioMusicCueMapping StockMappingForIndex(int musicIndex)
    {
        if (musicIndex is < 0 or >= MusicIndexCount) throw new ArgumentOutOfRangeException(nameof(musicIndex));
        return new(0, musicIndex);
    }

    /// <summary>TC404960 commits only a valid pending index. Its explicit mapping entry
    /// selects descriptor+4 (cueHash); absent/nonzero-kind/invalid-row entries retain zero.
    /// Request opcode 62h is prepared without invoking the original proxy or a mixer.</summary>
    public OriginalAudioPreparedMusicCommit PrepareCommit(OriginalAudioPreparedMusicSelection selection,
        OriginalAudioMusicCueMapping? mapping)
    {
        ArgumentNullException.ThrowIfNull(selection);
        int index = selection.EffectiveMusicIndex;
        if (!selection.CanQueue || index is < 0 or >= MusicIndexCount)
            return new(false, index, null, null, mapping);
        uint cueHash = mapping is { RawKind: 0, RawTableRowIndex: >= 0 and < MusicIndexCount }
            ? CueHashes[mapping.RawTableRowIndex] : 0;
        return new(true, index, 0x62, cueHash, mapping);
    }

    private static void ValidateLocale(string locale)
    {
        if (locale is not ("SC" or "TC"))
            throw new ArgumentOutOfRangeException(nameof(locale), locale, "Music selector supports verified SC/TC revisions only.");
    }
}
