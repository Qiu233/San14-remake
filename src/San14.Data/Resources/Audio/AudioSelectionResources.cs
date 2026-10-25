using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace San14.Data;

public sealed record AudioSelectionSection(uint TypeId, int Offset, int Bytes);
public sealed record AudioNamedCue(uint CueId, string? Name, uint Flags, int SectionOffset, int SectionBytes);
public sealed record AudioSampleHeader(uint TypeId, int HeaderOffset, int HeaderBytes,
    int Channels, int SampleRate, int DeclaredSamples, int LoopStartSample,
    bool IsExternal, IReadOnlyList<long> DataOffsets, IReadOnlyList<int> DataSizes);
public sealed record AudioNamedSample(uint SampleId, string? Name, uint Flags,
    int SectionOffset, int SectionBytes, IReadOnlyList<AudioSampleHeader> Headers);
public sealed record AudioExternalSampleReference(string Name, uint SampleId,
    uint StreamResourceId, int StreamIndex, string StreamResourceUri,
    long StreamPayloadOffset, int StreamEncodedBytes, LoadedRdbAsset SelectionSource);
public sealed record AudioSelectionBank(LoadedRdbAsset Source, uint StringSeed, byte Platform,
    IReadOnlyList<AudioNamedCue> Cues, IReadOnlyList<AudioNamedSample> Samples,
    IReadOnlyList<AudioSelectionSection> Sections)
{
    public string? PhysicalPath { get; init; }
}
public sealed record AudioSelectedInternalSample(string Name, uint SampleId,
    LoadedRdbAsset Source, int SourceSectionOffset, int SourceSectionBytes, string SourceSectionSha256,
    byte[] DecoderBytes, string DecoderSha256, string DecoderExtension, string Codec,
    int SampleRate, int Channels, long Samples, bool HasLoop, long LoopStartSample, long LoopEndSample)
{
    public SourceProvenance EncodedSource { get; init; } = new("", 0, "");
    public AudioDecodeInput AsDecodeInput() => new(DecoderBytes, DecoderExtension, Codec,
        DecoderSha256, EncodedSource, new(SampleRate, Channels, Samples),
        HasLoop ? LoopStartSample : null, HasLoop ? LoopEndSample : null)
        { IndexSource = Source.IndexSource };
}

/// <summary>
/// Reads explicit ASRS/KTSR cue and sample names, including names encrypted with the bank seed.
/// Unknown section IDs remain visible in Sections. It does not infer a cue's graph, game callsite,
/// mixer gain, active BGM, localization fallback, or a relation to an arbitrary SRST bank.
/// </summary>
public static class AudioSelectionResources
{
    public const uint InstalledUiBankId = 0x17047C02;
    public const uint InstalledMusicBankId = 0x3A160928;
    public const uint InstalledAdditionalMusicBankId = 0xB090FFEC;
    public const string ButtonSelectName = "SE_000_BUTTON_SELECT";
    public const string CancelName = "SE_000_CANCEL";
    private const uint CueSection = 0x368C88BD, SampleSection = 0x70CBCCC5;
    private const uint InternalHeader = 0xE96FD86A, ExternalHeader = 0x7D43D038;
    private const int MaximumBankBytes = 64 * 1024 * 1024;

    public static AudioSelectionBank Load(GameResourceRepository repository, uint fileId,
        string archiveName = "RRPreview")
    {
        ArgumentNullException.ThrowIfNull(repository);
        LoadedRdbAsset source = repository.LoadRdbAsset(archiveName, fileId);
        return Read(source) with { PhysicalPath = source.OverrideSource?.PhysicalPath ??
            Path.Combine(repository.SourceDirectory, source.SourceContainerPath) };
    }

    public static AudioSelectionBank Read(LoadedRdbAsset source)
    {
        ArgumentNullException.ThrowIfNull(source);
        byte[] b = source.Bytes;
        if (b.Length is < 80 or > MaximumBankBytes || !b.AsSpan(0, 8).SequenceEqual("ASRS\0\0\0\0"u8) ||
            BinaryPrimitives.ReadUInt64LittleEndian(b.AsSpan(8)) != (ulong)b.Length ||
            !b.AsSpan(16, 4).SequenceEqual("KTSR"u8) || U32(b, 20) != 0x1A487B77 ||
            U32(b, 40) != b.Length - 16 || U32(b, 44) != b.Length - 16)
            throw new InvalidDataException("Unsupported audio selection bank header or size.");
        uint seed = U32(b, 28);
        var cues = new List<AudioNamedCue>();
        var samples = new List<AudioNamedSample>();
        var sections = new List<AudioSelectionSection>();
        int p = 80;
        while (p < b.Length)
        {
            if (sections.Count >= 65536 || b.Length - p < 8)
                throw new InvalidDataException("Audio selection section count or range is invalid.");
            uint type = U32(b, p);
            int size = Size(U32(b, p + 4));
            if (size < 8 || size > b.Length - p)
                throw new InvalidDataException("Audio selection section extends outside the bank.");
            sections.Add(new(type, p, size));
            if (type == CueSection)
            {
                RequireSize(size, 44);
                uint flags = U32(b, p + 12);
                cues.Add(new(U32(b, p + 8), ReadName(b, p, size, U32(b, p + 40), flags, 0x200, seed), flags, p, size));
            }
            else if (type == SampleSection)
            {
                RequireSize(size, 28);
                uint flags = U32(b, p + 12);
                int count = Size(U32(b, p + 16)), pointers = Size(U32(b, p + 20));
                if (count is < 1 or > 64 || pointers < 28 || pointers > size || count > (size - pointers) / 4)
                    throw new InvalidDataException("Audio sample header pointer table is invalid.");
                var headers = new List<AudioSampleHeader>();
                for (int i = 0; i < count; i++)
                {
                    int h = checked(p + Size(U32(b, p + pointers + i * 4)));
                    if (h < p + 28 || h > p + size - 8)
                        throw new InvalidDataException("Audio sample subheader is outside its section.");
                    uint subtype = U32(b, h);
                    int headerSize = Size(U32(b, h + 4));
                    if (headerSize < 8 || headerSize > p + size - h)
                        throw new InvalidDataException("Audio sample subheader length is invalid.");
                    if (subtype == InternalHeader)
                    {
                        RequireSize(headerSize, 56);
                        int channels = Size(U32(b, h + 12)), rate = I32(b, h + 20), sampleCount = I32(b, h + 24);
                        ValidateSamples(channels, rate, sampleCount);
                        int starts = Size(U32(b, h + 48)), sizes = Size(U32(b, h + 52));
                        if (starts < 56 || starts > headerSize || channels > (headerSize - starts) / 4 ||
                            sizes < 56 || sizes > headerSize || channels > (headerSize - sizes) / 4)
                            throw new InvalidDataException("Internal audio channel pointer table is invalid.");
                        var offsets = new long[channels]; var lengths = new int[channels];
                        for (int ch = 0; ch < channels; ch++)
                        {
                            long offset = (long)h + U32(b, h + starts + ch * 4);
                            int length = Size(U32(b, h + sizes + ch * 4));
                            if (offset < h || offset > h + headerSize || length <= 0 || length > h + headerSize - offset)
                                throw new InvalidDataException("Internal audio channel data extends outside its subheader.");
                            offsets[ch] = offset; lengths[ch] = length;
                        }
                        headers.Add(new(subtype, h, headerSize, channels, rate, sampleCount, I32(b, h + 32), false, offsets, lengths));
                    }
                    else if (subtype == ExternalHeader)
                    {
                        RequireSize(headerSize, 64);
                        int channels = Size(U32(b, h + 12)), rate = I32(b, h + 24), sampleCount = I32(b, h + 28);
                        ValidateSamples(channels, rate, sampleCount);
                        headers.Add(new(subtype, h, headerSize, channels, rate, sampleCount, I32(b, h + 36), true,
                            new long[] { U32(b, h + 52) }, new[] { Size(U32(b, h + 56)) }));
                    }
                    else
                    {
                        // Expose the verified boundary without guessing an unknown codec's fields.
                        headers.Add(new(subtype, h, headerSize, 0, 0, 0, -1, false, Array.Empty<long>(), Array.Empty<int>()));
                    }
                }
                samples.Add(new(U32(b, p + 8), ReadName(b, p, size, U32(b, p + 24), flags, 8, seed),
                    flags, p, size, headers.AsReadOnly()));
            }
            p = checked(p + size);
        }
        if (p != b.Length) throw new InvalidDataException("Audio selection sections do not cover the bank.");
        return new(source, seed, b[27], cues.AsReadOnly(), samples.AsReadOnly(), sections.AsReadOnly());
    }

    /// <summary>
    /// Builds a decoder-only, one-sound SRSA container. Source bytes and their range hashes stay
    /// separate from the derived wrapper, whose three declared sizes are explicitly rewritten.
    /// A same-named cue is evidence of the static key; its playback graph is not executed here.
    /// </summary>
    public static AudioSelectedInternalSample SelectInternalSample(AudioSelectionBank bank, string name)
    {
        ArgumentNullException.ThrowIfNull(bank);
        ArgumentException.ThrowIfNullOrEmpty(name);
        AudioNamedSample[] matches = bank.Samples.Where(s => s.Name == name && (s.Flags & 0x10000) == 0).ToArray();
        if (matches.Length != 1) throw new InvalidDataException($"Audio sample name '{name}' must resolve to exactly one section.");
        AudioNamedSample selected = matches[0];
        if (selected.Headers.Count != 1) throw new NotSupportedException("A composite audio sample requires an explicit playback graph.");
        AudioSampleHeader h = selected.Headers[0];
        byte[] b = bank.Source.Bytes;
        if (h.TypeId != InternalHeader || h.IsExternal || bank.Platform != 1 || b[h.HeaderOffset + 16] != 0)
            throw new NotSupportedException("This selected sound is outside the verified SRSA/MSADPCM decoder contract.");
        if (h.LoopStartSample >= h.DeclaredSamples)
            throw new InvalidDataException("Internal audio loop start is outside playable samples.");
        byte[] section = b.AsSpan(selected.SectionOffset, selected.SectionBytes).ToArray();
        byte[] derived = new byte[checked(80 + section.Length)];
        b.AsSpan(0, 80).CopyTo(derived);
        section.CopyTo(derived, 80);
        BinaryPrimitives.WriteUInt64LittleEndian(derived.AsSpan(8), (ulong)derived.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(derived.AsSpan(40), (uint)derived.Length - 16);
        BinaryPrimitives.WriteUInt32LittleEndian(derived.AsSpan(44), (uint)derived.Length - 16);
        bool loop = h.LoopStartSample >= 0;
        return new(name, selected.SampleId, bank.Source, selected.SectionOffset, selected.SectionBytes,
            Convert.ToHexString(SHA256.HashData(section)), derived, Convert.ToHexString(SHA256.HashData(derived)),
            ".srsa", "SRSA-MSADPCM", h.SampleRate, h.Channels, h.DeclaredSamples,
            loop, loop ? h.LoopStartSample : 0, loop ? h.DeclaredSamples : 0)
        {
            EncodedSource = new(bank.Source.SourceContainerPath + $"#decoded-range={selected.SectionOffset},{selected.SectionBytes}",
                selected.SectionBytes, Convert.ToHexString(SHA256.HashData(section)))
            {
                ResourceUri = bank.Source.ResourceUri + $"#sound={selected.SampleId:X8}",
                PhysicalPath = bank.PhysicalPath ?? bank.Source.OverrideSource?.PhysicalPath ?? "",
                IsOverride = bank.Source.OverrideSource is not null
            }
        };
    }

    /// <summary>Resolves a named external sample only within the caller's explicit stream bank.
    /// Sample ID, stored offset and size must all agree. This never picks the first global match.</summary>
    public static AudioExternalSampleReference ResolveExternalSample(AudioSelectionBank bank,
        string name, AudioDirectory explicitStreamBank)
    {
        ArgumentNullException.ThrowIfNull(bank);
        ArgumentNullException.ThrowIfNull(explicitStreamBank);
        ArgumentException.ThrowIfNullOrEmpty(name);
        // Installed voice banks contain both memory and streaming representations with the
        // same name and ID. The explicitly requested external namespace is bit 16, not a
        // guessed preference for the first of those two physical representations.
        AudioNamedSample[] named = bank.Samples.Where(s => s.Name == name && (s.Flags & 0x10000) != 0).ToArray();
        if (named.Length != 1) throw new InvalidDataException($"External audio name '{name}' must resolve to exactly one selection section.");
        AudioNamedSample sample = named[0];
        if (sample.Headers.Count != 1 || sample.Headers[0] is not { IsExternal: true, TypeId: ExternalHeader } h ||
            (sample.Flags & 0x10000) == 0)
            throw new NotSupportedException("Selected audio is outside the verified external sample reference contract.");
        // Source KTSR external pointers are relative to the inner KTSR. The stream reader's
        // PayloadOffset is relative to its full TSRS wrapper, which is exactly 16 bytes.
        AudioStream[] matches = explicitStreamBank.Streams.Where(s =>
            s.SectionId == sample.SampleId && s.PayloadOffset == h.DataOffsets[0] + 16 &&
            s.EncodedBytes == h.DataSizes[0]).ToArray();
        if (matches.Length != 1)
            throw new InvalidDataException("Explicit stream bank must have one sample matching the original ID, offset and size.");
        AudioStream stream = matches[0];
        if (stream.Ktac is not { } ktac || ktac.Channels != h.Channels || ktac.SampleRate != h.SampleRate)
            throw new InvalidDataException("External audio sample rate or channel count differs between selection and stream banks.");
        return new(name, sample.SampleId, explicitStreamBank.Asset.FileId, stream.Index,
            explicitStreamBank.Asset.ResourceUri + $"#stream={stream.Index}", stream.PayloadOffset,
            stream.EncodedBytes, bank.Source);
    }

    private static string? ReadName(byte[] b, int section, int size, uint rawPointer, uint flags, uint encrypted, uint seed)
    {
        if (rawPointer == 0) return null;
        int pointer = Size(rawPointer);
        if (pointer < 8 || pointer >= size) throw new InvalidDataException("Audio name pointer is outside its section.");
        var name = new List<byte>();
        bool terminated = false;
        for (int i = section + pointer; i < section + size && name.Count < 255; i++)
        {
            byte value = b[i];
            if ((flags & encrypted) != 0)
            {
                unchecked { seed = 0x343FD * seed + 0x269EC3; }
                value ^= (byte)(seed >> 16);
            }
            if (value == 0) { terminated = true; break; }
            name.Add(value);
        }
        if (!terminated) throw new InvalidDataException("Audio name exceeds its terminated string budget.");
        try { return new UTF8Encoding(false, true).GetString(name.ToArray()); }
        catch (DecoderFallbackException error) { throw new NotSupportedException("Audio name is not valid UTF-8.", error); }
    }
    private static void ValidateSamples(int channels, int rate, int count)
    {
        if (channels is < 1 or > 8 || rate is < 1 or > 192000 || count <= 0)
            throw new InvalidDataException("Audio sample rate, channel or sample count is invalid.");
    }
    private static void RequireSize(int actual, int minimum)
    { if (actual < minimum) throw new InvalidDataException("Audio selection section is shorter than its supported header."); }
    private static int Size(uint value) => value <= int.MaxValue ? (int)value : throw new InvalidDataException("Audio selection unsigned size exceeds int32.");
    private static uint U32(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
    private static int I32(byte[] bytes, int offset) => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, 4));
}
