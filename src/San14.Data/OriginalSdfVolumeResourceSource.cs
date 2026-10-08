using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Actual D40 Model ordinal19. This is neither an A8 Material D0
/// texture group nor a declaration about the current original process.</summary>
public sealed class LoadedOriginalSdfModelDeclaration
{
    internal LoadedOriginalSdfModelDeclaration(LoadedOriginalSdfVolumeResourceSource native,
        LoadedObjects dictionary, uint modelObjectId, uint resourceHash)
        => (NativeSource, Dictionary, ModelObjectId, SdfResourceHash) = (native, dictionary, modelObjectId, resourceHash);
    public LoadedOriginalSdfVolumeResourceSource NativeSource { get; }
    public LoadedObjects Dictionary { get; }
    public uint ModelObjectId { get; }
    public uint SdfResourceHash { get; }
    public string SourceEvidence => NativeSource.SourceEvidence + ";" + Dictionary.Source.ResourceUri +
        "@" + Dictionary.Source.DecodedSha256 + ";Model=" + ModelObjectId.ToString("X8") +
        ";ordinal19/SDFResourceHash=" + SdfResourceHash.ToString("X8");

    /// <summary>At this owned Model's actual fresh registration. Zero is the
    /// source loader's omitted dependency4, so the registered D0 is null.
    /// Nonzero requires its actual completed type20 payload. This API never
    /// turns an unimplemented lookup or unavailable resource into null.</summary>
    public OriginalSdfModelVolumeRegistration RegisterFreshOwnedModel(
        Func<uint, OriginalSdfVolumeResourcePayload> readActualReadyDependency4)
    {
        if (SdfResourceHash == 0) return new(this, null);
        ArgumentNullException.ThrowIfNull(readActualReadyDependency4);
        var payload = readActualReadyDependency4(SdfResourceHash) ??
            throw new NotSupportedException("Nonzero Model SDF reference requires its actual ready type20 resource payload.");
        if (payload.SourceResourceHash != SdfResourceHash)
            throw new InvalidDataException("Model dependency4 belongs to another SDF resource.");
        return new(this, payload.ReadActualFirstTexture());
    }
}

/// <summary>The finite current D0 subset of an owned Model registration.
/// It does not certify the rest of Model ready or a GPU population upload.</summary>
public sealed record OriginalSdfModelVolumeRegistration(
    LoadedOriginalSdfModelDeclaration Source, OriginalSdfVolumeResourceTexture? VolumeD0);

/// <summary>One actual ready texture from F546A0's type20 payload. The image
/// and source attribute28 directory are supplied by its true device creator.
/// A G1MG bound or a regular texture group cannot create this record.</summary>
public sealed class OriginalSdfVolumeResourceTexture
{
    private readonly byte[] header28;
    private readonly Action requireActualCurrentOwner;
    private OriginalSdfVolumeResourceTexture(byte[] header28, object actualImage,
        Action requireActualCurrentOwner)
        => (this.header28, ActualImageResource20, this.requireActualCurrentOwner) =
            (header28, actualImage, requireActualCurrentOwner);
    public object ActualImageResource20 { get; }
    public ReadOnlyMemory<byte> ReadActualSourceHeader28()
    { requireActualCurrentOwner(); return header28; }
    public void RequireCurrent() => requireActualCurrentOwner();

    /// <summary>1092E80 reads WORD30/32/34 and the first matching attribute15
    /// payload. Attribute records use WORD tag, BYTE wordCount, BYTE lastFlag.
    /// Scale and offset are copied as source words, preserving NaN/sign bits.</summary>
    public static OriginalSdfVolumeResourceTexture CaptureActualReadyTexture(
        ushort actualWidth30, ushort actualHeight32, ushort actualDepth34,
        ReadOnlySpan<byte> actualAttributeDirectory28, object actualImageResource,
        Action requireActualCurrentOwner)
    {
        ArgumentNullException.ThrowIfNull(actualImageResource);
        ArgumentNullException.ThrowIfNull(requireActualCurrentOwner);
        requireActualCurrentOwner();
        ReadOnlySpan<byte> attribute = ReadActualAttribute15(actualAttributeDirectory28);
        if (attribute.Length < 16)
            throw new InvalidDataException("Actual SDF attribute15 has fewer than four source words.");
        byte[] header = new byte[28];
        BinaryPrimitives.WriteUInt32LittleEndian(header, actualWidth30);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4), actualHeight32);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(8), actualDepth34);
        attribute.Slice(12,4).CopyTo(header.AsSpan(12));
        attribute[..12].CopyTo(header.AsSpan(16));
        return new(header, actualImageResource, requireActualCurrentOwner);
    }

    private static ReadOnlySpan<byte> ReadActualAttribute15(ReadOnlySpan<byte> directory)
    {
        int cursor = 0;
        while (true)
        {
            if (cursor > directory.Length - 4)
                throw new InvalidDataException("Actual SDF texture attribute directory is truncated or has no terminal record.");
            ushort tag = BinaryPrimitives.ReadUInt16LittleEndian(directory[cursor..]);
            int count = directory[cursor + 2] * 4;
            if (cursor + 4 > directory.Length - count)
                throw new InvalidDataException("Actual SDF texture attribute payload lies outside its directory.");
            if (tag == 15) return directory.Slice(cursor + 4, count);
            if (directory[cursor + 3] != 0)
                throw new NotSupportedException("Actual ready SDF texture has no attribute15 source descriptor.");
            cursor += 4 + count;
        }
    }
}

/// <summary>F546A0 requires raw resource kind20 and reads the first payload
/// texture after its signed count20 is positive. This owner is supplied only
/// after the actual resource producer completed; it does not upload a G1T.</summary>
public sealed class OriginalSdfVolumeResourcePayload
{
    private readonly OriginalSdfVolumeResourceTexture[] textures;
    private readonly Action requireActualCurrentOwner;
    private OriginalSdfVolumeResourcePayload(uint hash, OriginalSdfVolumeResourceTexture[] textures,
        Action requireActualCurrentOwner)
        => (SourceResourceHash, this.textures, this.requireActualCurrentOwner) =
            (hash, textures, requireActualCurrentOwner);
    public uint SourceResourceHash { get; }
    public static OriginalSdfVolumeResourcePayload CaptureActualReadyType20(
        uint actualSourceResourceHash, uint actualResourceKind4,
        IReadOnlyList<OriginalSdfVolumeResourceTexture> actualTextures28,
        Action requireActualCurrentOwner)
    {
        if ((actualResourceKind4 & 63) != 20)
            throw new InvalidDataException("Model SDF dependency4 must have the actual original resource kind20.");
        ArgumentNullException.ThrowIfNull(actualTextures28);
        ArgumentNullException.ThrowIfNull(requireActualCurrentOwner);
        requireActualCurrentOwner();
        var copy = actualTextures28.ToArray();
        if (copy.Length == 0)
            throw new NotSupportedException("Present SDF payload with count20 zero has no supported source header producer.");
        foreach (var texture in copy)
        { ArgumentNullException.ThrowIfNull(texture); texture.RequireCurrent(); }
        return new(actualSourceResourceHash, copy, requireActualCurrentOwner);
    }
    public OriginalSdfVolumeResourceTexture ReadActualFirstTexture()
    { requireActualCurrentOwner(); var first = textures[0]; first.RequireCurrent(); return first; }
}

public sealed class LoadedOriginalSdfVolumeResourceSource
{
    private readonly Dictionary<LoadedObjects,DokDocument> importedDictionaries =
        new(ReferenceEqualityComparer.Instance);
    internal LoadedOriginalSdfVolumeResourceSource(string locale, string evidence)
        => (Locale, SourceEvidence) = (locale, evidence);
    public string Locale { get; }
    public string SourceEvidence { get; }

    /// <summary>Load/import boundary only. Reparse checked source bytes once,
    /// then retain the actual ordinal19 scalar; normal getters do not hash.</summary>
    public LoadedOriginalSdfModelDeclaration ImportModel(LoadedObjects actualDictionary,
        uint actualModelObjectId)
    {
        ArgumentNullException.ThrowIfNull(actualDictionary);
        if (!importedDictionaries.TryGetValue(actualDictionary,out var document))
        {
            string sha = Convert.ToHexString(SHA256.HashData(actualDictionary.Source.Bytes));
            if (sha != actualDictionary.Source.DecodedSha256)
                throw new InvalidDataException("SDF Model declaration differs from its loaded source bytes.");
            document = DokReader.Read(actualDictionary.Source.Bytes);
            importedDictionaries.Add(actualDictionary,document);
        }
        var model = document.Objects.Single(o => o.ObjectId == actualModelObjectId);
        if (model.Kind != DokObjectKind.Inline || model.ClassId != 0xD40B3C8F || model.Properties.Count <= 19)
            throw new InvalidDataException("SDF import requires the actual typed D40 Model declaration.");
        var p = model.Properties[19];
        if (p.Index != 19 || p.NameHash != 0x53457DEF || p.TypeCode != 5 || p.ElementCount != 1 || p.RawValue.Length != 4)
            throw new InvalidDataException("Model SDFResourceHash differs from the source ordinal19 DWORD schema.");
        return new(this, actualDictionary, actualModelObjectId, BinaryPrimitives.ReadUInt32LittleEndian(p.RawValue));
    }
}

public static class OriginalSdfVolumeResourceSource
{
    public static LoadedOriginalSdfVolumeResourceSource Load(OriginalGameRepository repository, string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return new(locale, OriginalDeferredSamplerSources.LoadSdfVolumeResourceNativeSource(repository.SourceDirectory, locale));
    }
}
