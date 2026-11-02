using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

public sealed record AudioAsset(uint FileId, string ResourceUri, long PayloadBytes, string ContainerPath);
public sealed record AudioCatalog(IReadOnlyList<AudioAsset> Assets, SourceProvenance IndexSource);
public sealed record KtacInfo(uint CodecType, int SampleRate, int Channels, int FrameSamples,
    int EncoderDelay, int EndPadding, uint TotalSamples, uint LoopStartFrame, uint LoopEndFrame,
    int LoopStartAdjust, int LoopEndPadding, int TableOffset, int TableEntries, int DataOffset, int DataBytes)
{
    public long PlayableSamples => (long)TotalSamples - EncoderDelay - EndPadding;
    public bool HasLoop => LoopEndFrame > 0;
    public long LoopStartSample => (long)LoopStartFrame * FrameSamples + LoopStartAdjust - EncoderDelay;
    public long LoopEndSample => ((long)LoopEndFrame + 1) * FrameSamples - LoopEndPadding - EncoderDelay;
}
public sealed record AudioStream(int Index, uint SectionId, string Codec, int EncodedBytes,
    long PayloadOffset, KtacInfo? Ktac);
public sealed record AudioDirectory(AudioAsset Asset, IReadOnlyList<AudioStream> Streams,
    SourceProvenance IndexSource, string PhysicalPath, bool IsOverride);
public sealed record LoadedAudioStream(AudioDirectory Directory, AudioStream Stream,
    byte[] Bytes, string ResourceUri, string EncodedSha256)
{
    /// <summary>The selected encoded byte range, rather than an invented full-container hash.</summary>
    public SourceProvenance EncodedSource => new(ResourceUri, Bytes.Length, EncodedSha256)
    {
        ResourceUri = this.ResourceUri, PhysicalPath = Directory.PhysicalPath,
        IsOverride = Directory.IsOverride
    };
}

/// <summary>
/// Reads the installed SRST/KTSR stream family on demand. Catalog reads RDB metadata only;
/// directory reads skip encoded media; LoadStream reads only the selected stream. No decoded
/// audio or game bytes are bundled. A payload override uses the existing RDB URI convention.
/// Names, BGM/voice roles, current sound selection and loop policy are not inferred.
/// </summary>
public sealed class AudioRepository
{
    public const uint StreamResourceTypeId = 0x0D34474D;
    private const int MaximumStreams = 65536;
    private const int MaximumStreamBytes = 64 * 1024 * 1024;
    private readonly string _sourceDirectory, _archiveName, _rdbPath;
    private readonly ResourceOverrides _overrides;
    private readonly object _indexGate = new();
    private RdbIndex? _index;
    private SourceProvenance? _indexSource;

    public AudioRepository(string? sourceDirectory = null, string? overridesDirectory = null,
        string archiveName = "RRPreview")
    {
        _ = GameResourceId.Rdb(archiveName, 0);
        _sourceDirectory = Path.GetFullPath(GameResourceRepository.ResolveConfiguredSourceDirectory(sourceDirectory));
        _archiveName = archiveName;
        _rdbPath = Path.Combine(_sourceDirectory, "0002", archiveName + ".rdb");
        _overrides = new ResourceOverrides(overridesDirectory ?? Environment.GetEnvironmentVariable("SAN14_OVERRIDE_DIR"));
    }

    public AudioCatalog ReadCatalog()
    {
        EnsureIndex();
        AudioAsset[] assets = _index!.Records.Where(r => r.TypeId == StreamResourceTypeId)
            .Select(r => new AudioAsset(r.FileId, GameResourceId.Rdb(_archiveName, r.FileId).ToString(),
                r.DecodedSize, Path.GetRelativePath(_sourceDirectory, SafeContainer(r)).Replace('\\', '/'))).ToArray();
        return new(assets, _indexSource!);
    }

    public AudioDirectory ReadDirectory(uint fileId)
    {
        using Payload input = OpenPayload(fileId);
        return ParseDirectory(input);
    }

    public LoadedAudioStream LoadStream(uint fileId, int streamIndex)
    {
        using Payload input = OpenPayload(fileId);
        AudioDirectory directory = ParseDirectory(input);
        if ((uint)streamIndex >= directory.Streams.Count)
            throw new ArgumentOutOfRangeException(nameof(streamIndex), "Audio stream ordinal is outside this resource.");
        return LoadSelected(input, directory, directory.Streams[streamIndex]);
    }

    /// <summary>Resolves a source sample ID inside one explicitly chosen stream resource.
    /// Duplicate IDs are an error; no archive-wide first-match choice is made.</summary>
    public LoadedAudioStream LoadStreamBySectionId(uint fileId, uint sectionId)
    {
        using Payload input = OpenPayload(fileId);
        AudioDirectory directory = ParseDirectory(input);
        AudioStream[] matches = directory.Streams.Where(s => s.SectionId == sectionId).Take(2).ToArray();
        if (matches.Length == 0) throw new KeyNotFoundException($"Audio sample {sectionId:X8} is absent from {directory.Asset.ResourceUri}.");
        if (matches.Length != 1) throw new InvalidDataException($"Audio sample {sectionId:X8} has ambiguous stream entries in {directory.Asset.ResourceUri}.");
        return LoadSelected(input, directory, matches[0]);
    }

    private static LoadedAudioStream LoadSelected(Payload input, AudioDirectory directory,
        AudioStream stream)
    {
        if (stream.Codec is not ("KTAC" or "KOVS"))
            throw new NotSupportedException($"Audio codec {stream.Codec} is unsupported in {directory.Asset.ResourceUri}.");
        byte[] bytes = input.Read(stream.PayloadOffset, stream.EncodedBytes);
        if (stream.Ktac is not null) ValidateKtacPackets(bytes, stream.Ktac);
        if (stream.Codec == "KOVS") ValidateKovs(bytes);
        return new(directory, stream, bytes, directory.Asset.ResourceUri + $"#stream={stream.Index}",
            Convert.ToHexString(SHA256.HashData(bytes)));
    }

    /// <summary>Returns standard Ogg Vorbis bytes for a KOVS stream. KTAC requires an external decoder.</summary>
    public static byte[] DecodeKovsContainer(LoadedAudioStream stream)
    {
        if (stream.Stream.Codec != "KOVS")
            throw new NotSupportedException($"{stream.Stream.Codec} is not a KOVS/Vorbis container.");
        ValidateKovs(stream.Bytes);
        int size = I32(stream.Bytes, 4);
        byte[] ogg = stream.Bytes.AsSpan(32, size).ToArray();
        for (int i = 0; i < Math.Min(256, ogg.Length); i++) ogg[i] ^= (byte)i;
        if (!ogg.AsSpan(0, 4).SequenceEqual("OggS"u8)) throw new InvalidDataException("KOVS decoded stream lacks OggS magic.");
        return ogg;
    }

    private AudioDirectory ParseDirectory(Payload input)
    {
        byte[] wrapper = input.Read(0, 16);
        if (!wrapper.AsSpan(0, 8).SequenceEqual("TSRS\0\0\0\0"u8) ||
            BinaryPrimitives.ReadUInt64LittleEndian(wrapper.AsSpan(8)) != (ulong)input.Length)
            throw new InvalidDataException("Audio payload has an invalid SRST wrapper or length.");
        byte[] header = input.Read(16, 64);
        if (!header.AsSpan(0, 4).SequenceEqual("KTSR"u8) || U32(header, 4) != 0xFCDD9402 ||
            U32(header, 24) != input.Length - 16 || U32(header, 28) != input.Length - 16)
            throw new InvalidDataException("Audio payload has an unsupported KTSR header or compressed length.");
        // Source family has no key in its reserved header. Never silently read encrypted streams.
        if (header.AsSpan(32, 32).IndexOfAnyExcept((byte)0) >= 0)
            throw new NotSupportedException("KTSR reserved/encryption header is unsupported.");
        var streams = new List<AudioStream>();
        long position = 80;
        while (position < input.Length)
        {
            if (streams.Count >= MaximumStreams) throw new InvalidDataException("Audio stream count exceeds the read budget.");
            byte[] section = input.Read(position, 20);
            uint type = U32(section, 0);
            int size = I32(section, 4), pointer = I32(section, 12), length = I32(section, 16);
            if (size < 20 || size > input.Length - position)
                throw new InvalidDataException("KTSR section extends outside its resource.");
            if (type != 0x15F4D409)
                throw new NotSupportedException($"KTSR section type {type:X8} is unsupported; expected stream sample.");
            if (pointer < 20 || length < 4 || length > MaximumStreamBytes || pointer > size || length > size - pointer)
                throw new InvalidDataException("KTSR stream pointer or size is invalid.");
            long streamOffset = position + pointer;
            byte[] codecHeader = input.Read(streamOffset, Math.Min(64, length));
            string codec = System.Text.Encoding.ASCII.GetString(codecHeader.AsSpan(0, 4));
            KtacInfo? ktac = codec == "KTAC" ? ParseKtac(codecHeader, length) : null;
            streams.Add(new(streams.Count, U32(section, 8), codec, length, streamOffset, ktac));
            position += size;
        }
        if (streams.Count == 0 || position != input.Length) throw new InvalidDataException("KTSR has no complete stream directory.");
        var asset = new AudioAsset(input.Record.FileId, GameResourceId.Rdb(_archiveName, input.Record.FileId).ToString(),
            input.Length, Path.GetRelativePath(_sourceDirectory, input.SourcePath).Replace('\\', '/'));
        return new(asset, streams.AsReadOnly(), _indexSource!, input.File.Name, input.IsOverride);
    }

    private static KtacInfo ParseKtac(byte[] h, int length)
    {
        if (h.Length != 64 || U32(h, 4) != 0x01000000 || U32(h, 8) != length)
            throw new NotSupportedException("KTAC version or declared file length is unsupported.");
        var info = new KtacInfo(U32(h, 20), I32(h, 24), U16(h, 32), U16(h, 34), U16(h, 36), U16(h, 38),
            U32(h, 28), U32(h, 40), U32(h, 44), U16(h, 48), U16(h, 50), I32(h, 56), I32(h, 60), I32(h, 12), I32(h, 16));
        if (info.SampleRate <= 0 || info.SampleRate > 192000 || info.Channels is < 1 or > 8 ||
            info.FrameSamples <= 0 || info.TableEntries is < 1 or > 1000000 || info.TableOffset < 64 ||
            info.TableOffset > length || (long)info.TableEntries * 4 > length - info.TableOffset ||
            info.DataOffset < info.TableOffset + (long)info.TableEntries * 4 || info.DataOffset > length ||
            info.DataBytes < 0 || info.DataBytes != length - info.DataOffset ||
            (long)info.FrameSamples * info.TableEntries != info.TotalSamples || info.PlayableSamples < 0)
            throw new InvalidDataException("KTAC sample metadata or frame ranges are invalid.");
        if (info.CodecType is not (0 or 2)) throw new NotSupportedException($"KTAC type {info.CodecType} requires a separate decoder contract.");
        if (info.HasLoop && (info.LoopStartSample < 0 || info.LoopEndSample > info.PlayableSamples || info.LoopStartSample >= info.LoopEndSample))
            throw new InvalidDataException("KTAC loop sample range is invalid.");
        return info;
    }

    private static void ValidateKtacPackets(byte[] bytes, KtacInfo info)
    {
        long total = 0;
        for (int i = 0; i < info.TableEntries; i++)
        {
            int size = I32(bytes, info.TableOffset + i * 4);
            if (size <= 0 || size > info.DataBytes - total) throw new InvalidDataException("KTAC frame table exceeds encoded data.");
            total += size;
        }
        if (total != info.DataBytes) throw new InvalidDataException("KTAC frame table does not cover encoded data.");
    }

    private static void ValidateKovs(byte[] bytes)
    {
        if (bytes.Length < 36 || !bytes.AsSpan(0, 4).SequenceEqual("KOVS"u8) || I32(bytes, 4) != bytes.Length - 32)
            throw new InvalidDataException("KOVS stream header or length is invalid.");
    }

    private Payload OpenPayload(uint fileId)
    {
        EnsureIndex();
        RdbRecord record = _index!.Records.SingleOrDefault(r => r.FileId == fileId)
            ?? throw new KeyNotFoundException($"Audio RDB asset {_archiveName}/{fileId:X8} is missing.");
        if (record.TypeId != StreamResourceTypeId) throw new NotSupportedException($"RDB type {record.TypeId:X8} is not the supported stream audio family.");
        string originalPath = SafeContainer(record);
        string? over = _overrides.Find(GameResourceId.Rdb(_archiveName, fileId));
        var file = new FileStream(over ?? originalPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            if (over is not null) return new(file, record, 0, file.Length, originalPath, true);
            if ((record.Flags & 0x00F00000) != 0) throw new NotSupportedException("Compressed/encrypted RDB audio is not supported for range reads.");
            long offset = record.Address?.Offset ?? 0, length = record.Address?.Length ?? file.Length;
            if (offset < 0 || length < 48 || offset > file.Length || length > file.Length - offset)
                throw new InvalidDataException("Audio RDB entry extends outside its container.");
            file.Position = offset; byte[] header = new byte[48]; file.ReadExactly(header);
            long entry = BinaryPrimitives.ReadInt64LittleEndian(header.AsSpan(8)), content = BinaryPrimitives.ReadInt64LittleEndian(header.AsSpan(16));
            long decoded = BinaryPrimitives.ReadInt64LittleEndian(header.AsSpan(24));
            if (!header.AsSpan(0, 8).SequenceEqual("IDRK0000"u8) || U32(header, 36) != fileId || U32(header, 40) != record.TypeId ||
                entry < 48 || entry > length || content < 80 || content > entry - 48 || decoded != record.DecodedSize || decoded != content)
                throw new InvalidDataException("Audio inner RDB entry disagrees with its index.");
            return new(file, record, offset + entry - content, content, originalPath, false);
        }
        catch { file.Dispose(); throw; }
    }

    private string SafeContainer(RdbRecord record)
    {
        string path = Path.GetFullPath(RdbArchiveReader.ContainerPath(_rdbPath, _index!, record));
        string prefix = Path.TrimEndingDirectorySeparator(_sourceDirectory) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Audio RDB container escapes the source root.");
        return path;
    }

    private void EnsureIndex()
    {
        lock (_indexGate)
        {
        if (_index is not null) return;
        GameResourceId id = GameResourceId.File($"0002/{_archiveName}.rdb");
        string? over = _overrides.Find(id);
        using var file = new FileStream(over ?? _rdbPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length is < 24 or > 16 * 1024 * 1024) throw new InvalidDataException("Audio RDB index exceeds the read budget.");
        byte[] bytes = new byte[checked((int)file.Length)]; file.ReadExactly(bytes);
        RdbIndex index = RdbIndexReader.Read(bytes);
        _indexSource = new(id.Key, bytes.Length, Convert.ToHexString(SHA256.HashData(bytes)))
        { ResourceUri = id.ToString(), PhysicalPath = file.Name, IsOverride = over is not null };
        _index = index;
        }
    }

    private static int I32(byte[] b, int at) => BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(at, 4));
    private static uint U32(byte[] b, int at) => BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(at, 4));
    private static ushort U16(byte[] b, int at) => BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(at, 2));

    private sealed class Payload(FileStream file, RdbRecord record, long start, long length, string originalPath, bool isOverride) : IDisposable
    {
        public FileStream File => file;
        public RdbRecord Record => record;
        public long Length => length;
        public string SourcePath => originalPath;
        public bool IsOverride => isOverride;
        public byte[] Read(long offset, int size)
        {
            if (offset < 0 || size < 0 || offset > length || size > length - offset) throw new InvalidDataException("Audio metadata or media range is truncated.");
            file.Position = checked(start + offset);
            byte[] bytes = new byte[size]; file.ReadExactly(bytes); return bytes;
        }
        public void Dispose() => file.Dispose();
    }
}
