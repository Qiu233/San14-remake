using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

/// <summary>The real CF4 scratch allocation and D123E0 work130 binding for
/// the supported successful own-camera path. Record7C is capacity; CF4
/// publishes exactly one bounded matrix and separately increments394.</summary>
public sealed class DeferredEarlyShadowCurrentWorkPublication
{
    private readonly byte[] arena;
    private readonly IReadOnlyList<Matrix4x4> matrices;
    private bool consumed;
    internal DeferredEarlyShadowCurrentWorkPublication(
        DeferredEarlyShadowCurrentWorkVolumeFields actualWork,
        DeferredEarlyShadowRecordNumericFields actualRecord,
        DeferredEarlyShadowCurrentCompactResetFields actualReset)
    {
        OwnCamera = actualWork.OwnCamera; SelectedRecord = actualRecord; Reset = actualReset;
        WorkCapacity234 = actualRecord.Record7C;
        var layout = DeferredEarlyShadowWorkPublicationSource.PrepareNumericLayout(WorkCapacity234);
        arena = new byte[layout.ArenaByteLength];
        WorkMatrixByteOffset = layout.WorkMatrixByteOffset;
        StreamCursorByteOffset = layout.StreamCursorByteOffset;
        StreamWordCount20 = layout.StreamWordCount20;
        BinaryPrimitives.WriteUInt32LittleEndian(arena.AsSpan(StreamCursorByteOffset), layout.WorkAllocationDwordCount);
        actualWork.Numeric.WriteWorkMatrix(arena.AsSpan(WorkMatrixByteOffset, 64));
        matrices = Array.AsReadOnly(new[] { actualWork.Numeric.WorkMatrix });
    }
    public DeferredEarlyShadowCurrentOwnCameraFields OwnCamera { get; }
    public DeferredEarlyShadowRecordNumericFields SelectedRecord { get; }
    public DeferredEarlyShadowCurrentCompactResetFields Reset { get; }
    public uint Type390 => 1;
    public uint WorkCapacity234 { get; }
    public uint WorkCount394 => 1;
    public int WorkMatrixByteOffset { get; }
    public int StreamCursorByteOffset { get; }
    public ulong StreamWordCount20 { get; }
    public int ArenaByteLength => arena.Length;
    public IReadOnlyList<Matrix4x4> CurrentWorkMatrices => !consumed ? matrices :
        throw new InvalidOperationException("ShadowCPU130 work inventory was consumed by its actual v48 matrix preparation.");
    public string SourceEvidence => Reset.SourceEvidence + ";CF4CC7..D7D-owned-scratch/record18/20/28;" +
        "D123E0-v78-success/type3901/234=record7C/394=0/130-aligned-arena;" +
        "CF4F84..FA5-D19B10-one-work/inc394;not-caster-count";

    /// <summary>The callback represents the actual matrix producer. Only
    /// successful completion retires the managed130 binding.</summary>
    public void ConsumeCurrentMatrices(Action<IReadOnlyList<Matrix4x4>> actualMatrixProducer)
    {
        ArgumentNullException.ThrowIfNull(actualMatrixProducer);
        actualMatrixProducer(CurrentWorkMatrices); consumed = true;
    }
}

public readonly record struct DeferredEarlyShadowWorkNumericLayout(int ArenaByteLength,
    int WorkMatrixByteOffset, int StreamCursorByteOffset, ulong StreamWordCount20, uint WorkAllocationDwordCount);

public static class DeferredEarlyShadowWorkPublicationSource
{
    /// <summary>The actual normal CF4 path after D12470 and v78 succeeded.
    /// Zero source capacity never fabricates an available first work matrix.</summary>
    public static DeferredEarlyShadowCurrentWorkPublication? PrepareCurrent(
        DeferredEarlyShadowCurrentWorkVolumeFields actualWork,
        DeferredEarlyShadowRecordNumericFields actualRecord)
        => PrepareCurrent(actualWork, actualRecord, DeferredEarlyShadowCompactResetSource.PrepareCurrent(
            DeferredEarlyShadowCompactSource.PrepareCurrent(actualWork.OwnCamera)));

    public static DeferredEarlyShadowCurrentWorkPublication? PrepareCurrent(
        DeferredEarlyShadowCurrentWorkVolumeFields actualWork,
        DeferredEarlyShadowRecordNumericFields actualRecord,
        DeferredEarlyShadowCurrentCompactResetFields actualReset)
    {
        ArgumentNullException.ThrowIfNull(actualWork); ArgumentNullException.ThrowIfNull(actualRecord); ArgumentNullException.ThrowIfNull(actualReset);
        if (!ReferenceEquals(actualReset.Compact.OwnCamera, actualWork.OwnCamera) || !actualReset.SourceV78Succeeded || actualReset.Type390 != 1)
            throw new InvalidDataException("Work130 allocation needs the same selected compact controller's actual successful v78 reset.");
        var selected = actualWork.OwnCamera.Direction.Record;
        if (actualRecord.Record.RecordOwnerIdentity != selected.RecordOwnerIdentity ||
            actualRecord.Record.CurrentQueueIndex != selected.CurrentQueueIndex || actualRecord.Record.Mask78 != selected.RecordWord78)
            throw new InvalidDataException("Work130 requires the own Shadow Camera's same selected current record7C.");
        return actualRecord.Record7C == 0 ? null : new(actualWork, actualRecord, actualReset);
    }

    /// <summary>Finite CF4 scratch arithmetic. The managed arena is aligned
    /// by offsets; it never publishes an original-process pointer.</summary>
    public static DeferredEarlyShadowWorkNumericLayout PrepareNumericLayout(uint actualRecord7C)
    {
        if (actualRecord7C == 0) throw new ArgumentOutOfRangeException(nameof(actualRecord7C));
        uint matrixBytes = unchecked((actualRecord7C << 6) + 0x13u) & 0xFFFFFFFCu;
        uint largerBytes = unchecked(((actualRecord7C * 3u) << 10) + 0x13u) & 0xFFFFFFFCu;
        uint allocated = unchecked(Math.Max(matrixBytes, largerBytes) + 0x10u + 0xFu) & 0xFFFFFFF0u;
        uint words = unchecked((actualRecord7C << 6) + 0x13u) >> 2;
        int cursor = checked(4 + (int)words * 4);
        int length = checked((int)allocated);
        if (cursor + 4 > length || 16 + 64 > length)
            throw new InvalidDataException("Actual record7C overflowed the finite original work arena layout.");
        return new(length, 16, cursor, 1ul + words, words);
    }
}
