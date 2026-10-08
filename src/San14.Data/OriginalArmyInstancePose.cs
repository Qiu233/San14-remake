using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>A complete explicit source record at ArmyModel+C0+i*40.
/// Current position is +10, target +20, heading +30, speed +34, delay +38
/// and elapsed +3C. Neither static scenario bytes nor descriptor counts supply it.</summary>
public sealed record OriginalArmyInstancePoseRecord(int RecordIndex, Vector4 CurrentPosition,
    Vector4 TargetPosition, float Heading, float Speed, int Delay, int Elapsed, byte[] RawBytes);

public sealed record OriginalArmyInstancePoseSnapshot(
    IReadOnlyList<OriginalArmyInstancePoseRecord> Records, string CallerSource,
    OriginalArmyInstancePoseCodeSource CodeSource);

/// <summary>Arguments to the actual model virtual position +68 and rotation +70
/// setters. Rotation keeps native Q0..Q3 storage; the WorldPQ consumer interprets it.
/// The ordinary writer makes no scale write.</summary>
public sealed record OriginalArmyInstancePoseWrite(int RecordIndex, int DescriptorOffset,
    int DescriptorInstanceIndex, Vector4 PositionArgument, Vector4 RotationArgument,
    Vector4? RawScaleArgument = null)
{
    public Vector3? ScaleArgument => RawScaleArgument is { } value ? new(value.X, value.Y, value.Z) : null;
    public IReadOnlyList<int> VirtualSetterOffsets => RawScaleArgument.HasValue
        ? Array.AsReadOnly(new[] { 0x78, 0x68, 0x70 }) : Array.AsReadOnly(new[] { 0x68, 0x70 });
}

public sealed record OriginalArmyInstancePoseWrites(
    IReadOnlyList<OriginalArmyInstancePoseWrite> Writes,
    IReadOnlyList<int> MissingModelRecordIndices, string CallerSource,
    OriginalArmyInstancePoseCodeSource CodeSource)
{
    public bool Complete => MissingModelRecordIndices.Count == 0;
    public bool OriginalCurrentInstanceStateVerified => false;
}

public sealed record OriginalArmyInstanceFlagMissingModel(int DescriptorOffset,
    int DescriptorInstanceIndex, int RecordIndex);
public sealed record OriginalArmyInstanceFlagPoseWrites(
    IReadOnlyList<OriginalArmyInstancePoseWrite> Writes,
    IReadOnlyList<OriginalArmyInstanceFlagMissingModel> MissingModels,
    string CallerSource, OriginalArmyInstancePoseCodeSource CodeSource)
{
    public bool Complete => MissingModels.Count == 0;
    public bool OriginalCurrentInstanceStateVerified => false;
}

/// <summary>Ports the native nineteen-record initialization and ordinary mode-0
/// WORLD setter arguments. Caller current state, random draws, model readiness,
/// retained scale and parent composition remain explicit source inputs.</summary>
public static class OriginalArmyInstancePose
{
    public const int RecordCount = 19;
    public const int RecordBytes = 64;

    public static OriginalArmyInstancePoseSnapshot ReadRecords(ReadOnlySpan<byte> completeRecords,
        string callerSource, string language)
    {
        Label(callerSource);
        if (completeRecords.Length != RecordCount * RecordBytes)
            throw Error("requires nineteen complete 64-byte current records");
        var records = new OriginalArmyInstancePoseRecord[RecordCount];
        for (int i = 0; i < records.Length; i++)
        {
            ReadOnlySpan<byte> raw = completeRecords.Slice(i * RecordBytes, RecordBytes);
            Vector4 current = Vector(raw, 0x10), target = Vector(raw, 0x20);
            float heading = Float(raw, 0x30), speed = Float(raw, 0x34);
            if (!Finite(current.X, current.Y, current.Z) || !float.IsFinite(heading))
                throw Error("requires finite current XYZ and heading for every consumed record");
            records[i] = new(i, current, target, heading, speed,
                BinaryPrimitives.ReadInt32LittleEndian(raw[0x38..]),
                BinaryPrimitives.ReadInt32LittleEndian(raw[0x3C..]), raw.ToArray());
        }
        return new(Array.AsReadOnly(records), callerSource,
            OriginalArmyInstancePoseCodeSources.ForLanguage(language));
    }

    /// <summary>SC/TC 1C5910: initialize the current/target positions and headings,
    /// speed=0, elapsed=0 and the source randomized delay. All untouched record
    /// bytes retain the caller template. It projects this record writer only;
    /// it does not create native nodes or substitute a guessed random sequence.</summary>
    public static OriginalArmyInstancePoseSnapshot InitializeRecords(Vector4 explicitCenter,
        float explicitHeading, IReadOnlyList<int> explicitRandomResults,
        ReadOnlySpan<byte> completeRecordTemplates, string callerSource, string language)
    {
        ArgumentNullException.ThrowIfNull(explicitRandomResults);
        Label(callerSource);
        if (completeRecordTemplates.Length != RecordCount * RecordBytes ||
            explicitRandomResults.Count != RecordCount ||
            explicitRandomResults.Any(value => value < 0) ||
            !Finite(explicitCenter.X, explicitCenter.Y, explicitCenter.Z) ||
            !float.IsFinite(explicitHeading))
            throw Error("initialization requires complete templates, nineteen explicit nonnegative native random results, finite center XYZ and heading");
        var source = OriginalArmyInstancePoseCodeSources.ForLanguage(language);
        var (sin, cos) = OriginalArmyAnimationSampler.SourceSinCos(explicitHeading);
        byte[] bytes = completeRecordTemplates.ToArray();
        for (int i = 0; i < RecordCount; i++)
        {
            Vector3 row = OriginalArmyInstancePoseCodeSources.FormationRows[i];
            float u = Product(row.X, 90), v = Product(row.Y, 90);
            Vector4 position = new(Add(Add(Product(v, sin), Product(u, cos)), explicitCenter.X),
                Add(0, explicitCenter.Y),
                Add(Subtract(Product(v, cos), Product(u, sin)), explicitCenter.Z), 1);
            float delayScale = Add(Divide(explicitRandomResults[i] % 3 - 1, 10), 1);
            float delayValue = Product(delayScale, Product(row.Z, 400));
            if (!Finite(position.X, position.Y, position.Z) || !float.IsFinite(delayValue) ||
                delayValue < int.MinValue || delayValue >= 2147483648f)
                throw Error("initialization produced unsupported nonfinite or out-of-range source values");
            Span<byte> record = bytes.AsSpan(i * RecordBytes, RecordBytes);
            WriteVector(record, 0x10, position); WriteVector(record, 0x20, position);
            WriteFloat(record, 0x30, explicitHeading); WriteFloat(record, 0x34, 0);
            BinaryPrimitives.WriteInt32LittleEndian(record[0x38..], (int)delayValue);
            BinaryPrimitives.WriteInt32LittleEndian(record[0x3C..], 0);
        }
        return ReadRecords(bytes, callerSource, source.Language);
    }

    /// <summary>SC 1CD231..1CD39B / paired TC mode 0. Readiness is the caller's
    /// resolved model pointer result from each native pool node. Failed models
    /// are reported; source order advances even for a missing model. Existing
    /// model scale is retained because this branch never invokes virtual +78.</summary>
    public static OriginalArmyInstancePoseWrites WriteOrdinary(
        OriginalArmyInstancePoseSnapshot snapshot, IReadOnlyList<bool> explicitModelResolved)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(explicitModelResolved);
        if (snapshot.CodeSource is null || snapshot.Records is null ||
            snapshot.CodeSource != OriginalArmyInstancePoseCodeSources.ForLanguage(snapshot.CodeSource.Language) ||
            snapshot.Records.Count != RecordCount || explicitModelResolved.Count != RecordCount)
            throw Error("requires a supported source profile and complete nineteen-record model resolution snapshot");
        Label(snapshot.CallerSource);
        var writes = new List<OriginalArmyInstancePoseWrite>();
        var missing = new List<int>();
        for (int i = 0; i < RecordCount; i++)
        {
            OriginalArmyInstancePoseRecord record = snapshot.Records[i];
            if (record is null || record.RecordIndex != i || record.RawBytes is null || record.RawBytes.Length != RecordBytes ||
                !SameBits(record.CurrentPosition, Vector(record.RawBytes, 0x10)) ||
                BitConverter.SingleToUInt32Bits(record.Heading) !=
                    BinaryPrimitives.ReadUInt32LittleEndian(record.RawBytes.AsSpan(0x30)))
                throw Error("record projection differs from its complete raw source snapshot");
            if (!explicitModelResolved[i]) { missing.Add(i); continue; }
            if (!Finite(record.CurrentPosition.X, record.CurrentPosition.Y, record.CurrentPosition.Z) ||
                !float.IsFinite(record.Heading))
                throw Error("has nonfinite consumed current position or heading");
            Vector4 rotation = YawArgument(record.Heading);
            int role = OriginalArmyInstancePoseCodeSources.RecordRoles[i];
            writes.Add(new(i, role == 0 ? 0x778 : 0x750, role == 0 ? i : i - 15,
                new(record.CurrentPosition.X, record.CurrentPosition.Y, record.CurrentPosition.Z, 1), rotation));
        }
        return new(writes.AsReadOnly(), missing.AsReadOnly(), snapshot.CallerSource, snapshot.CodeSource);
    }

    /// <summary>SC 1CD57E..1CD837, paired TC 1CD58E..1CD847. The caller supplies
    /// actual ordered pool-node model resolution results, including a known empty
    /// pool. Descriptor7F0 may contain any supplied node count; every node uses
    /// source record7. Descriptor7C8 consumes table records2,11,13 in pool order.
    /// It accepts only this proven three-entry table extent. Flags write scale
    /// first, then position, then raw yaw without the soldier normalization.</summary>
    public static OriginalArmyInstanceFlagPoseWrites WriteFlags(OriginalArmyInstancePoseSnapshot snapshot,
        int explicitCurrentMode, IReadOnlyList<bool> explicitBannerModelsResolved,
        IReadOnlyList<bool> explicitFlagModelsResolved)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(explicitBannerModelsResolved);
        ArgumentNullException.ThrowIfNull(explicitFlagModelsResolved);
        if (snapshot.CodeSource is null || snapshot.Records is null || snapshot.Records.Count != RecordCount ||
            snapshot.CodeSource != OriginalArmyInstancePoseCodeSources.ForLanguage(snapshot.CodeSource.Language) ||
            explicitCurrentMode is < 0 or > 3 ||
            explicitFlagModelsResolved.Count > OriginalArmyInstancePoseCodeSources.FlagRecordIndices.Count)
            throw Error("flag writes require a supported source snapshot, explicit mode and an ordered pool within the proven flag-table extent");
        Label(snapshot.CallerSource);
        var writes = new List<OriginalArmyInstancePoseWrite>();
        var missing = new List<OriginalArmyInstanceFlagMissingModel>();
        for (int i = 0; i < explicitBannerModelsResolved.Count; i++)
            WriteFlag(0x7F0, i, OriginalArmyInstancePoseCodeSources.BannerRecordIndex,
                explicitBannerModelsResolved[i], explicitCurrentMode == 1 ? null : OriginalArmyInstancePoseCodeSources.FlagHeightOffset,
                OriginalArmyInstancePoseCodeSources.BannerScaleArgument);
        for (int i = 0; i < explicitFlagModelsResolved.Count; i++)
            WriteFlag(0x7C8, i, OriginalArmyInstancePoseCodeSources.FlagRecordIndices[i], explicitFlagModelsResolved[i],
                OriginalArmyInstancePoseCodeSources.FlagHeightOffset, OriginalArmyInstancePoseCodeSources.FlagScaleArgument);
        return new(writes.AsReadOnly(), missing.AsReadOnly(), snapshot.CallerSource, snapshot.CodeSource);

        void WriteFlag(int descriptor, int instance, int index, bool resolved, float? heightOffset, Vector4 scale)
        {
            if (!resolved) { missing.Add(new(descriptor, instance, index)); return; }
            OriginalArmyInstancePoseRecord record = snapshot.Records[index];
            if (record is null || record.RecordIndex != index || record.RawBytes is null || record.RawBytes.Length != RecordBytes ||
                !SameBits(record.CurrentPosition, Vector(record.RawBytes, 0x10)) ||
                BitConverter.SingleToUInt32Bits(record.Heading) != BinaryPrimitives.ReadUInt32LittleEndian(record.RawBytes.AsSpan(0x30)) ||
                !Finite(record.CurrentPosition.X, record.CurrentPosition.Y, record.CurrentPosition.Z) || !float.IsFinite(record.Heading))
                throw Error("flag record projection differs from its consumed finite source state");
            float y = heightOffset is { } offset ? Add(record.CurrentPosition.Y, offset) : record.CurrentPosition.Y;
            if (!float.IsFinite(y)) throw Error("flag height addition produced unsupported nonfinite values");
            var (sin, cos) = OriginalArmyAnimationSampler.SourceSinCos(Product(record.Heading, 0.5f));
            writes.Add(new(index, descriptor, instance, new(record.CurrentPosition.X, y, record.CurrentPosition.Z, 1),
                new(0, sin, 0, cos), scale));
        }
    }

    /// <summary>Source scalar sin/cos of heading/2, followed by 1CF8B0's
    /// separate scalar squared-length, sqrt, division and four multiplies.
    /// It preserves the native approximate trigonometry and normalization.</summary>
    public static Vector4 YawArgument(float heading)
    {
        var (sin, cos) = OriginalArmyAnimationSampler.SourceSinCos(Product(heading, 0.5f));
        float squared = Add(Add(Add(Product(0, 0), Product(sin, sin)), Product(0, 0)), Product(cos, cos));
        float factor = Divide(1, MathF.Sqrt(squared));
        Vector4 result = new(Product(0, factor), Product(factor, sin), Product(factor, 0), Product(factor, cos));
        if (!Finite(result.X, result.Y, result.Z) || !float.IsFinite(result.W))
            throw Error("yaw argument normalization produced unsupported nonfinite values");
        return result;
    }

    private static Vector4 Vector(ReadOnlySpan<byte> bytes, int offset) =>
        new(Float(bytes, offset), Float(bytes, offset + 4), Float(bytes, offset + 8), Float(bytes, offset + 12));
    private static float Float(ReadOnlySpan<byte> bytes, int offset) =>
        BitConverter.UInt32BitsToSingle(BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]));
    private static void WriteVector(Span<byte> bytes, int offset, Vector4 v)
    { WriteFloat(bytes, offset, v.X); WriteFloat(bytes, offset + 4, v.Y); WriteFloat(bytes, offset + 8, v.Z); WriteFloat(bytes, offset + 12, v.W); }
    private static void WriteFloat(Span<byte> bytes, int offset, float value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(bytes[offset..], BitConverter.SingleToUInt32Bits(value));
    private static bool Finite(float x, float y, float z) => float.IsFinite(x) && float.IsFinite(y) && float.IsFinite(z);
    private static bool SameBits(Vector4 a, Vector4 b) =>
        BitConverter.SingleToUInt32Bits(a.X) == BitConverter.SingleToUInt32Bits(b.X) &&
        BitConverter.SingleToUInt32Bits(a.Y) == BitConverter.SingleToUInt32Bits(b.Y) &&
        BitConverter.SingleToUInt32Bits(a.Z) == BitConverter.SingleToUInt32Bits(b.Z) &&
        BitConverter.SingleToUInt32Bits(a.W) == BitConverter.SingleToUInt32Bits(b.W);
    private static void Label(string label)
    { if (string.IsNullOrWhiteSpace(label)) throw Error("requires an explicit caller source label"); }
    private static InvalidDataException Error(string message) => new($"Original Army instance pose {message}.");
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Product(float a, float b) => a * b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Add(float a, float b) => a + b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Subtract(float a, float b) => a - b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Divide(float a, float b) => a / b;
}
