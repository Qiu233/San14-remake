using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

/// <summary>Actual Frame400 pool inputs. The second backing array uses the
/// source's 28-byte allocation multiplier, while appended dual records use48.</summary>
public readonly record struct OriginalOrdinarySdfOperation0PoolFields(
    uint SphereCapacity4, uint DualCapacity8)
{
    public ulong SphereAllocationBytes => (ulong)SphereCapacity4 * 16;
    public ulong DualAllocationBytes => (ulong)DualCapacity8 * 28;
    public uint SphereByteBudget70 => unchecked(SphereCapacity4 * 16);
    public uint DualByteBudget74 => unchecked(DualCapacity8 * 28);
}

/// <summary>One retained View1C60 CPU bank. Only source-written semantic bytes
/// are exposed: operation0 reads16 sphere bytes and32 of each48-byte dual row.</summary>
public sealed class OriginalOrdinarySdfOperation0Bank
{
    private readonly byte[] scratchSpheres, scratchDuals, publishedSpheres, publishedDuals;
    private readonly bool[] scratchSphereWritten, scratchDualWritten, publishedSphereWritten, publishedDualWritten;
    private ulong used18, used38;
    private bool scratchAvailable;

    internal OriginalOrdinarySdfOperation0Bank(OriginalOrdinarySdfOperation0PoolFields fields)
    {
        Fields = fields;
        scratchSpheres = new byte[checked((int)fields.SphereAllocationBytes)];
        scratchDuals = new byte[checked((int)fields.DualAllocationBytes)];
        publishedSpheres = new byte[scratchSpheres.Length];
        publishedDuals = new byte[scratchDuals.Length];
        scratchSphereWritten = new bool[scratchSpheres.Length / 16];
        scratchDualWritten = new bool[scratchDuals.Length / 48];
        publishedSphereWritten = new bool[scratchSphereWritten.Length];
        publishedDualWritten = new bool[scratchDualWritten.Length];
        // D06990 explicitly initializes the two published counts withQWORD80=0.
        Count80 = Count84 = 0; ResetAcquiredScratch();
    }

    public OriginalOrdinarySdfOperation0PoolFields Fields { get; }
    public uint Count80 { get; private set; }
    public uint Count84 { get; private set; }
    public ulong UsedSpheres18 => used18;
    public ulong UsedDuals38 => used38;

    public void ResetAcquiredScratch()
    { used18 = used38 = 0; scratchAvailable = true; }

    /// <summary>D65F30 reserves the two entire generated batches separately.
    /// The first arena remains advanced if the second reservation fails.</summary>
    public bool AppendCurrentShapes(OriginalOrdinarySdfModelShapeRecords actualShapes)
    {
        ArgumentNullException.ThrowIfNull(actualShapes);
        if (!scratchAvailable) throw new InvalidOperationException("SDF View bank needs its current acquire/reset.");
        ulong sphereSize = checked((ulong)actualShapes.Spheres.Count * 16);
        ulong dualSize = checked((ulong)actualShapes.DualSpheres.Count * 48);
        ulong sphereOffset = used18, dualOffset = used38;
        ulong sphereEnd = unchecked(used18 + sphereSize), dualEnd = unchecked(used38 + dualSize);
        bool spherePresent = sphereEnd <= Fields.SphereByteBudget70;
        bool dualPresent = dualEnd <= Fields.DualByteBudget74;
        if (spherePresent) used18 = sphereEnd;
        if (dualPresent) used38 = dualEnd;
        if (!spherePresent || !dualPresent) return false;
        for (int i = 0; i < actualShapes.Spheres.Count; i++)
        {
            WriteVector(scratchSpheres.AsSpan(checked((int)sphereOffset) + i * 16, 16), actualShapes.Spheres[i]);
            scratchSphereWritten[checked((int)(sphereOffset / 16)) + i] = true;
        }
        for (int i = 0; i < actualShapes.DualSpheres.Count; i++)
        {
            var row = scratchDuals.AsSpan(checked((int)dualOffset) + i * 48, 48);
            var pair = actualShapes.DualSpheres[i];
            WriteVector(row[..16], pair.First); WriteVector(row.Slice(16, 16), pair.Second);
            BinaryPrimitives.WriteSingleLittleEndian(row[32..], pair.ExtraRadius20);
            scratchDualWritten[checked((int)(dualOffset / 48)) + i] = true;
            // The remaining12 bytes are not written by D65F30 or read by D7C450.
        }
        return true;
    }

    /// <summary>D1E1B0 publishes the two collected arrays/counts, then clears
    /// scratch pointers and used sizes. A later acquire preserves these counts.</summary>
    public void FinalizeCurrentCollection()
    {
        if (!scratchAvailable) throw new InvalidOperationException("SDF View bank collection was already finalized.");
        if (used18 != 0) scratchSpheres.AsSpan(0, checked((int)used18)).CopyTo(publishedSpheres);
        if (used38 != 0) scratchDuals.AsSpan(0, checked((int)used38)).CopyTo(publishedDuals);
        Count80 = unchecked((uint)(used18 / 16)); Count84 = unchecked((uint)(used38 / 48));
        scratchSphereWritten.AsSpan(0, checked((int)Count80)).CopyTo(publishedSphereWritten);
        scratchDualWritten.AsSpan(0, checked((int)Count84)).CopyTo(publishedDualWritten);
        used18 = used38 = 0; scratchAvailable = false;
    }

    public Vector4 ReadPublishedSphere(uint index)
    {
        if (index >= Count80) throw new ArgumentOutOfRangeException(nameof(index));
        if (!publishedSphereWritten[index]) throw new InvalidOperationException("This source reservation was published without a known semantic record write.");
        return ReadVector(publishedSpheres.AsSpan(checked((int)index * 16), 16));
    }

    public OriginalOrdinarySdfDualSphere ReadPublishedDual(uint index)
    {
        if (index >= Count84) throw new ArgumentOutOfRangeException(nameof(index));
        if (!publishedDualWritten[index]) throw new InvalidOperationException("This source reservation was published without a known semantic record write.");
        var row = publishedDuals.AsSpan(checked((int)index * 48), 48);
        return new(ReadVector(row[..16]), ReadVector(row.Slice(16, 16)), BinaryPrimitives.ReadSingleLittleEndian(row[32..]));
    }

    internal static Vector4 ReadVector(ReadOnlySpan<byte> row) => new(
        BinaryPrimitives.ReadSingleLittleEndian(row), BinaryPrimitives.ReadSingleLittleEndian(row[4..]),
        BinaryPrimitives.ReadSingleLittleEndian(row[8..]), BinaryPrimitives.ReadSingleLittleEndian(row[12..]));

    internal static void WriteVector(Span<byte> row, Vector4 value)
    {
        BinaryPrimitives.WriteSingleLittleEndian(row, value.X); BinaryPrimitives.WriteSingleLittleEndian(row[4..], value.Y);
        BinaryPrimitives.WriteSingleLittleEndian(row[8..], value.Z); BinaryPrimitives.WriteSingleLittleEndian(row[12..], value.W);
    }
}

/// <summary>The actual95CF30 result: byte count is independent of map storage.
/// ByteCount0 still reaches95D2B0; it does not initialize a device buffer.</summary>
public readonly record struct OriginalOrdinarySdfOperation0Mapping(uint ByteCount, Memory<byte> Bytes);

public interface IOriginalOrdinarySdfOperation0Transport
{
    OriginalOrdinarySdfOperation0Mapping MapCurrentBuffer(uint actualSide, uint sourceMapMode);
    void EndCurrentMap(uint actualSide);
}

public readonly record struct OriginalOrdinarySdfOperation0Publication(
    OriginalOrdinarySdfOperation0Counts Counts, Vector4 BoundsMin90, Vector4 BoundsMaxD0,
    bool MapAttempted, bool MappedBytesWritten);

/// <summary>AAF5E0/AB0A00's retained CPU ring. Four actual device buffers remain
/// with the transport owner. Constructor selector0 does not invent counts0.</summary>
public sealed class OriginalOrdinarySdfOperation0Ring
{
    private readonly uint?[] sphereCounts = new uint?[4], dualCounts = new uint?[4];
    private readonly Vector4[] min = new Vector4[4], max = new Vector4[4];
    private readonly byte[] sphereScratch, dualScratch;
    private bool currentWriteOpen;

    internal OriginalOrdinarySdfOperation0Ring(uint sphereCapacity, uint dualCapacity)
    {
        SphereCapacity70 = sphereCapacity; DualCapacity74 = dualCapacity;
        sphereScratch = new byte[checked((int)((ulong)sphereCapacity * 16))];
        dualScratch = new byte[checked((int)((ulong)dualCapacity * 32))];
        // AAF66E writes oneQWORD68: both selectors are explicitly0.
        WriteSelector68 = PublishedSelector6C = 0;
    }

    public uint SphereCapacity70 { get; }
    public uint DualCapacity74 { get; }
    public uint DeviceBufferByteCount => unchecked((SphereCapacity70 + DualCapacity74 * 2) * 16);
    public uint WriteSelector68 { get; private set; }
    public uint PublishedSelector6C { get; private set; }

    public OriginalOrdinarySdfOperation0Counts CaptureCurrentCounts()
    {
        int side = (int)PublishedSelector6C;
        return new(PublishedSelector6C,
            sphereCounts[side] ?? throw new InvalidOperationException("Source operation0 selected sphere count has no writer yet."),
            dualCounts[side] ?? throw new InvalidOperationException("Source operation0 selected dual count has no writer yet."));
    }

    public void ResetNextWriteSide()
    {
        WriteSelector68 = (PublishedSelector6C + 1) & 3;
        int side = (int)WriteSelector68; sphereCounts[side] = dualCounts[side] = 0;
        min[side] = new(float.MaxValue); max[side] = new(-float.MaxValue); currentWriteOpen = true;
    }

    public bool AppendCurrentSphere(Vector4 sphere)
    {
        int side = RequireWrite(); uint count = sphereCounts[side]!.Value;
        if (count >= SphereCapacity70) return false;
        OriginalOrdinarySdfOperation0Bank.WriteVector(sphereScratch.AsSpan(checked((int)count * 16), 16), sphere);
        sphereCounts[side] = count + 1; UpdateFirstBounds(side, sphere); return true;
    }

    public bool AppendCurrentDual(Vector4 first, Vector4 second)
    {
        int side = RequireWrite(); uint count = dualCounts[side]!.Value;
        // This check precedes the original same-center sphere merge.
        if (count >= DualCapacity74) return false;
        if (first.X == second.X && first.Y == second.Y && first.Z == second.Z)
        {
            // MAXSS selects its second operand for equality or unordered.
            float radius = first.W > second.W ? first.W : second.W;
            return AppendCurrentSphere(new(first.X, first.Y, first.Z, radius));
        }
        var row = dualScratch.AsSpan(checked((int)count * 32), 32);
        OriginalOrdinarySdfOperation0Bank.WriteVector(row[..16], first);
        OriginalOrdinarySdfOperation0Bank.WriteVector(row[16..], second);
        dualCounts[side] = count + 1; UpdateFirstBounds(side, first); UpdateSecondBounds(side, second); return true;
    }

    /// <summary>D7C450 ignores individual append failure and completes the
    /// entire current View collection before its normal upload/publication.</summary>
    public OriginalOrdinarySdfOperation0Publication ApplyCurrentBank(
        OriginalOrdinarySdfOperation0Bank actualCurrentViewBank,
        IOriginalOrdinarySdfOperation0Transport actualTransport)
    {
        ArgumentNullException.ThrowIfNull(actualCurrentViewBank);
        ResetNextWriteSide();
        for (uint i = 0; i < actualCurrentViewBank.Count80; i++) AppendCurrentSphere(actualCurrentViewBank.ReadPublishedSphere(i));
        for (uint i = 0; i < actualCurrentViewBank.Count84; i++)
        { var pair = actualCurrentViewBank.ReadPublishedDual(i); AppendCurrentDual(pair.First, pair.Second); }
        return PublishCurrent(actualTransport);
    }

    public OriginalOrdinarySdfOperation0Publication PublishCurrent(IOriginalOrdinarySdfOperation0Transport actualTransport)
    {
        ArgumentNullException.ThrowIfNull(actualTransport);
        int side = RequireWrite(); uint spheres = sphereCounts[side]!.Value, pairs = dualCounts[side]!.Value;
        bool attempted = unchecked(spheres + pairs) != 0, written = false;
        if (attempted)
        {
            var mapping = actualTransport.MapCurrentBuffer(WriteSelector68, 3);
            if (mapping.ByteCount != 0)
            {
                var bytes = mapping.Bytes.Span[..checked((int)mapping.ByteCount)];
                bytes.Clear();
                int firstSize = checked((int)((ulong)spheres * 16));
                int secondSize = checked((int)((ulong)pairs * 32));
                sphereScratch.AsSpan(0, firstSize).CopyTo(bytes);
                dualScratch.AsSpan(0, secondSize).CopyTo(bytes[firstSize..]); written = true;
            }
            // Source calls End even for map result0, then publishes6C.
            actualTransport.EndCurrentMap(WriteSelector68);
        }
        PublishedSelector6C = WriteSelector68; currentWriteOpen = false;
        return new(CaptureCurrentCounts(), min[side], max[side], attempted, written);
    }

    private int RequireWrite()
    { if (!currentWriteOpen) throw new InvalidOperationException("SDF operation0 needs its actual write-side reset."); return (int)WriteSelector68; }
    private void UpdateFirstBounds(int side, Vector4 sphere)
    {
        Vector4 a = min[side], b = max[side];
        static float MinSs(float prior, float value) => prior < value ? prior : value;
        static float BranchMax(float prior, float value) => prior >= value ? prior : value;
        min[side] = new(MinSs(a.X, sphere.X - sphere.W), MinSs(a.Y, sphere.Y - sphere.W), MinSs(a.Z, sphere.Z - sphere.W), a.W);
        max[side] = new(BranchMax(b.X, sphere.X + sphere.W), BranchMax(b.Y, sphere.Y + sphere.W), BranchMax(b.Z, sphere.Z + sphere.W), b.W);
    }
    private void UpdateSecondBounds(int side, Vector4 sphere)
    {
        Vector4 a = min[side], b = max[side];
        static float BranchMin(float prior, float value) => value >= prior ? prior : value;
        static float BranchMax(float prior, float value) => prior >= value ? prior : value;
        min[side] = new(BranchMin(a.X, sphere.X - sphere.W), BranchMin(a.Y, sphere.Y - sphere.W), BranchMin(a.Z, sphere.Z - sphere.W), a.W);
        max[side] = new(BranchMax(b.X, sphere.X + sphere.W), BranchMax(b.Y, sphere.Y + sphere.W), BranchMax(b.Z, sphere.Z + sphere.W), b.W);
    }
}
