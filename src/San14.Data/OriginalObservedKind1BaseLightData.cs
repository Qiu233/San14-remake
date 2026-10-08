using System.Buffers.Binary;
using System.Security.Cryptography;

namespace San14.Data;

/// <summary>Explicit saved-capture identity. Addresses are opaque observation
/// tokens; this component never dereferences them or identifies a source CPU.</summary>
public sealed record OriginalObservedKind1BaseLightDataIdentity(string CaptureUri,
    string CaptureSha256, int WindowOrdinal, ulong ViewAddress, ulong TaggedRowAddress,
    ulong RecordAddress, uint RawMask78);

/// <summary>An immutable owned copy of only a captured plain kind1 L[0,1B0).
/// The caller must verify the enclosing capture and window before supplying
/// these buffers. The unread10C0 tail stays unknown; this is not a Game-default
/// construction, postmodifier packet, selected Main owner or final GPU state.</summary>
public sealed class OriginalObservedKind1BaseLightData
{
    public const int PrefixByteCount = 0x1B0;
    public const int DeclaredPayloadByteCount = 0x10C0;
    public const int PlainHeaderByteCount = 0x90;
    public const int TaggedRowByteCount = 0x10;
    private readonly byte[] prefix;

    private OriginalObservedKind1BaseLightData(OriginalObservedKind1BaseLightDataIdentity identity,
        ReadOnlySpan<byte> row, ReadOnlySpan<byte> header, ReadOnlySpan<byte> prefix)
    {
        Identity = identity; this.prefix = prefix.ToArray();
        RawTaggedWord = BinaryPrimitives.ReadUInt64LittleEndian(row);
        PayloadAddress = BinaryPrimitives.ReadUInt64LittleEndian(header.Slice(8, 8));
        PrefixSha256 = Convert.ToHexString(SHA256.HashData(this.prefix));
        RowSha256 = Convert.ToHexString(SHA256.HashData(row));
        HeaderSha256 = Convert.ToHexString(SHA256.HashData(header));
        SourceProvenance = $"{identity.CaptureUri}@{identity.CaptureSha256};window={identity.WindowOrdinal};" +
            $"View={identity.ViewAddress:X16};row={identity.TaggedRowAddress:X16};record={identity.RecordAddress:X16};" +
            $"payload={PayloadAddress:X16};mask78={identity.RawMask78:X8};prefix={PrefixSha256};" +
            "owned-copy;known=L[0,1B0);tail=unknown;baseLightDataOnly=true;sourceCpu=unidentified;" +
            "postmodifier=false;activeMain=false;gpuFrame=false";
    }

    public OriginalObservedKind1BaseLightDataIdentity Identity { get; }
    public ulong RawTaggedWord { get; }
    public ulong PayloadAddress { get; }
    public string PrefixSha256 { get; }
    public string RowSha256 { get; }
    public string HeaderSha256 { get; }
    public string SourceProvenance { get; }
    public bool BaseLightDataOnly => true;
    public int KnownByteCount => PrefixByteCount;
    public int UnknownTailByteCount => DeclaredPayloadByteCount - PrefixByteCount;
    public bool CompleteLightDataAvailable => false;

    /// <summary>Verify the complete observed tag/header relationship and copy
    /// the exact prefix. The capture SHA is an explicit outer-reader pin; it is
    /// not recomputed from these fragments, nor a claim that the capture passed
    /// its process/arena/window gates.</summary>
    public static OriginalObservedKind1BaseLightData CapturePrefix(
        OriginalObservedKind1BaseLightDataIdentity identity, ReadOnlySpan<byte> completeTaggedRow,
        ReadOnlySpan<byte> completePlainHeader, ReadOnlySpan<byte> completePrefix)
    {
        ArgumentNullException.ThrowIfNull(identity);
        if (string.IsNullOrWhiteSpace(identity.CaptureUri) || identity.CaptureSha256 is null ||
            identity.CaptureSha256.Length != 64 || !identity.CaptureSha256.All(Uri.IsHexDigit) ||
            identity.WindowOrdinal < 0 || identity.ViewAddress == 0 || identity.TaggedRowAddress == 0 ||
            identity.RecordAddress == 0 || identity.RecordAddress > ulong.MaxValue - PlainHeaderByteCount)
            throw new InvalidDataException("Base LightData requires its explicit capture SHA, window and nonnull opaque View/row/record identities.");
        if (completeTaggedRow.Length != TaggedRowByteCount || completePlainHeader.Length != PlainHeaderByteCount ||
            completePrefix.Length != PrefixByteCount)
            throw new InvalidDataException("Base LightData requires the complete16B tag,90B header and exact1B0 prefix; missing or full10C0 inputs are not projected implicitly.");
        ulong tag = BinaryPrimitives.ReadUInt64LittleEndian(completeTaggedRow);
        if (tag >> 28 != 1 || BinaryPrimitives.ReadUInt64LittleEndian(completeTaggedRow.Slice(8, 8)) != identity.RecordAddress ||
            BinaryPrimitives.ReadUInt32LittleEndian(completePlainHeader.Slice(0x88, 4)) != 1 ||
            BinaryPrimitives.ReadUInt32LittleEndian(completePlainHeader.Slice(0x78, 4)) != identity.RawMask78 ||
            BinaryPrimitives.ReadUInt64LittleEndian(completePlainHeader.Slice(8, 8)) != identity.RecordAddress + PlainHeaderByteCount)
            throw new InvalidDataException("Observed kind1 tag/header, raw mask or inline LightData owner identity disagrees.");
        return new(identity with { CaptureSha256 = identity.CaptureSha256.ToUpperInvariant() },
            completeTaggedRow, completePlainHeader, completePrefix);
    }

    public byte[] CopyKnownPrefix() => prefix.ToArray();

    /// <summary>Only captured bytes are available. Requests into unknown tail
    /// fail instead of returning padding or a synthetic complete10C0 packet.</summary>
    public byte[] CopyKnownRange(int offset, int byteCount)
    {
        if (offset < 0 || byteCount < 0 || offset > PrefixByteCount || byteCount > PrefixByteCount - offset)
            throw new InvalidDataException("The requested LightData range contains uncaptured bytes.");
        return prefix.AsSpan(offset, byteCount).ToArray();
    }

    /// <summary>Decode this base prefix using the proven finite reader. Count
    /// is an explicit caller selection, not row+70, queue length or an inferred
    /// postmodifier/native trimming result.</summary>
    public OriginalDeferredLightingInputs DecodeBaseLighting(int explicitDirectionalCount)
    {
        var decoded = OriginalDeferredSceneConstants.ReadPreparedBaseLighting(prefix, explicitDirectionalCount);
        foreach (var light in decoded.Lights)
        { Finite(light.Direction); Finite(light.Color); Finite(light.Occurrence); }
        Finite(decoded.Ambient);
        return decoded;
    }

    /// <summary>Explicit Fog-owner enable is required. This decodes only the
    /// observed base packet; a modifier or final selected owner is not inferred.</summary>
    public OriginalDeferredFogInputs DecodeSharedFog(bool explicitOwnerFogEnabled)
    {
        var decoded = OriginalDeferredSceneConstants.ReadSharedFog(prefix, explicitOwnerFogEnabled);
        Finite(decoded.Color); Finite(decoded.Coefficients);
        foreach (var value in decoded.Scattering) Finite(value);
        return decoded;
    }

    private static void Finite(System.Numerics.Vector4 value)
    { Finite(value.X); Finite(value.Y); Finite(value.Z); Finite(value.W); }
    private static void Finite(float value)
    { if (!float.IsFinite(value)) throw new InvalidDataException("A consumed base LightData value is nonfinite; the captured bits remain unchanged."); }
}
