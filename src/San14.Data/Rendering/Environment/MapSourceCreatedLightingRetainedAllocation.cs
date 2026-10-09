using System.Security.Cryptography;

namespace San14.Data;

/// <summary>A complete native retained extent, read from a current owned
/// allocation or a current writer. Its provenance describes that byte source;
/// the class66 source declaration supplies no value for this extent.</summary>
public sealed record MapSourceCreatedLightingRetainedByteSource(
    int Offset, ReadOnlyMemory<byte> Bytes, string CurrentAllocationOrWriterProvenance);

/// <summary>The current owned allocation identity and all twelve native holes.
/// Missing allocation contents cannot be recovered from the HDRI properties.</summary>
public sealed record MapSourceCreatedLightingRetainedAllocationInputs(
    string AllocationOwnerIdentity, long AllocationRevision,
    IReadOnlyList<MapSourceCreatedLightingRetainedByteSource> CompleteRetained210);

/// <summary>An immutable capture of the210 constructor/loader-retained bytes
/// on the proved already-committed stack branch. The native LightData is a loader-stack local; neither
/// its stack reservation nor the later CPU allocation establishes these bytes.
/// This capture requires explicit current owned values for every retained byte.
/// It never claims that the original source constructed their values.</summary>
public sealed class MapSourceCreatedLightingRetainedAllocation
{
    private readonly MapSourceCreatedLightingRetainedByteSource[] retained;
    private readonly string resourceUri;
    private readonly string resourceSha256;
    private static readonly (int Offset, int Length)[] NativeLayout =
    [
        (0x164, 76), (0x2BC, 4), (0xAD5, 3), (0xB6F, 1), (0xB95, 3),
        (0xDC9, 3), (0xDD5, 3), (0xDE1, 3), (0xE01, 3),
        (0xF21, 71), (0x1090, 32), (0x10B8, 8)
    ];

    private MapSourceCreatedLightingRetainedAllocation(
        LoadedMainLightBindingSource source,
        MapSourceCreatedLightingRetainedAllocationInputs input,
        MapSourceCreatedLightingRetainedByteSource[] bytes, uint contextFlags3E4)
    {
        retained = bytes;
        resourceUri = source.Resource.Source.ResourceUri;
        resourceSha256 = source.Resource.Source.DecodedSha256;
        AllocationOwnerIdentity = input.AllocationOwnerIdentity;
        AllocationRevision = input.AllocationRevision;
        ContextFlags3E4 = contextFlags3E4;
        byte[] packed = bytes.SelectMany(value => value.Bytes.ToArray()).ToArray();
        RetainedSha256 = Convert.ToHexString(SHA256.HashData(packed));
        SourceProvenance = FormattableString.Invariant(
            $"{resourceUri}#{MainLightBindingSource.ObjectId:X8}@{resourceSha256};native=CBD460-stack-LightData+80/CDBEC0/CB6140-full-copy;owned-current-allocation={AllocationOwnerIdentity};revision={AllocationRevision};context={ContextFlags3E4:X8};retained210={RetainedSha256};") +
            string.Join(';', bytes.Select(value =>
                $"extent={value.Offset:X4}/{value.Bytes.Length}:{value.CurrentAllocationOrWriterProvenance}" +
                $"@{Convert.ToHexString(SHA256.HashData(value.Bytes.Span))}"));
    }

    public string AllocationOwnerIdentity { get; }
    public long AllocationRevision { get; }
    public uint ContextFlags3E4 { get; }
    public int RetainedByteCount => 210;
    public string RetainedSha256 { get; }
    public string SourceProvenance { get; }

    /// <summary>Detached byte/provenance copies. Mutating their backing arrays
    /// does not alter this owned generation.</summary>
    public IReadOnlyList<MapSourceCreatedLightingRetainedByteSource> RetainedByteSources =>
        Array.AsReadOnly(retained.Select(value => value with { Bytes = value.Bytes.ToArray() }).ToArray());

    /// <summary>Detached ranges accepted by the existing source device owner.</summary>
    public IReadOnlyList<MapSourceCreatedLightingRetainedRange> RetainedRanges =>
        Array.AsReadOnly(retained.Select(value =>
            new MapSourceCreatedLightingRetainedRange(value.Offset, value.Bytes.ToArray())).ToArray());

    public static MapSourceCreatedLightingRetainedAllocation CaptureGameDefault(
        LoadedMainLightBindingSource source,
        MapSourceCreatedLightingRetainedAllocationInputs input, uint contextFlags3E4)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.CompleteRetained210);
        if (string.IsNullOrWhiteSpace(input.AllocationOwnerIdentity) || input.AllocationRevision < 1)
            throw new InvalidDataException("Retained lighting requires a current allocation owner and positive revision.");
        ValidateLayout(source, contextFlags3E4);
        var values = input.CompleteRetained210;
        if (values.Count != NativeLayout.Length || values.Any(value => value is null) ||
            values.Select(value => value.Offset).Distinct().Count() != NativeLayout.Length)
            throw new InvalidDataException("Current lighting allocation requires all twelve exact retained extents once.");
        var ordered = new MapSourceCreatedLightingRetainedByteSource[NativeLayout.Length];
        for (int index = 0; index < NativeLayout.Length; index++)
        {
            var extent = NativeLayout[index];
            var value = values.SingleOrDefault(value => value.Offset == extent.Offset);
            if (value is null || value.Bytes.Length != extent.Length ||
                string.IsNullOrWhiteSpace(value.CurrentAllocationOrWriterProvenance))
                throw new InvalidDataException($"Current allocation bytes/provenance are incomplete at L+{extent.Offset:X4}/{extent.Length}.");
            ordered[index] = value with { Bytes = value.Bytes.ToArray() };
        }
        return new(source, input, ordered, contextFlags3E4);
    }

    /// <summary>Captures the native holes from a complete current owned byte
    /// image before the LightData constructor. No input byte value is inferred.</summary>
    public static MapSourceCreatedLightingRetainedAllocation CaptureCompleteGameDefault(
        LoadedMainLightBindingSource source, string allocationOwnerIdentity,
        long allocationRevision, ReadOnlySpan<byte> currentOwnedAllocation10C0,
        string currentAllocationProvenance, uint contextFlags3E4)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (currentOwnedAllocation10C0.Length != MainLightConstruction.LightDataBytes ||
            string.IsNullOrWhiteSpace(currentAllocationProvenance))
            throw new InvalidDataException("Current lighting allocation capture requires its complete10C0 byte image and provenance.");
        var values = new MapSourceCreatedLightingRetainedByteSource[NativeLayout.Length];
        for (int index = 0; index < NativeLayout.Length; index++)
        {
            var extent = NativeLayout[index];
            values[index] = new(extent.Offset,
                currentOwnedAllocation10C0.Slice(extent.Offset, extent.Length).ToArray(), currentAllocationProvenance);
        }
        return CaptureGameDefault(source, new(allocationOwnerIdentity, allocationRevision, values), contextFlags3E4);
    }

    /// <summary>Creates an intermediate constructor input. Its4078 known bytes
    /// are subsequently replaced by the exact source constructor/loader writes.</summary>
    public byte[] CreateConstructorTemplate(LoadedMainLightBindingSource source)
    {
        ValidateSource(source);
        return MapSourceCreatedLightingRetainedSource.BuildGameDefaultTemplate(
            source, RetainedRanges, ContextFlags3E4);
    }

    public MainLightConstructionPacket ApplyConstructor(
        LoadedMainLightBindingSource source,
        IReadOnlyList<MainLightConstructionResourceBinding> currentResourceBindings)
    {
        return MainLightConstruction.Apply(source, CreateConstructorTemplate(source),
            new(ContextFlags3E4, SourceProvenance, currentResourceBindings));
    }

    private void ValidateSource(LoadedMainLightBindingSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Resource.Source.ResourceUri != resourceUri ||
            !source.Resource.Source.DecodedSha256.Equals(resourceSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Current retained lighting allocation belongs to a different source declaration.");
        ValidateLayout(source, ContextFlags3E4);
    }

    private static void ValidateLayout(LoadedMainLightBindingSource source, uint flags)
    {
        var actual = MapSourceCreatedLightingRetainedSource.ReadGameDefaultLayout(source, flags);
        if (actual.Count != NativeLayout.Length || actual.Where((value, index) =>
            value.Offset != NativeLayout[index].Offset || value.Length != NativeLayout[index].Length).Any())
            throw new InvalidDataException("Source lighting constructor coverage differs from the paired twelve-extent210-byte contract.");
    }
}
