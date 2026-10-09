using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

/// <summary>The actual D11190 Aux8/3F4 gate. An absent Aux has no3F4
/// value; a present Aux cannot borrow an absent/default integer.</summary>
public readonly record struct DeferredEarlyShadowDirectionAuxFields(bool Present, uint Flags8, int? Signed3F4)
{
    public bool RequiresAngleClamp(DeferredEarlyShadowPreparationArguments actualArguments)
    {
        if (!Present) return false;
        if (Signed3F4 is null) throw new InvalidDataException("Present shadow Aux requires its current3F4 writer.");
        bool active = (Flags8 & 0x04000000) != 0 || Signed3F4 >= 0;
        return active && (actualArguments.DirectionEntry30 > -90 || actualArguments.DirectionEntry38 < 90);
    }
}

public sealed class LoadedDeferredEarlyShadowDirectionAuxSource
{
    internal LoadedDeferredEarlyShadowDirectionAuxSource(uint flags8, int signed3F4, string evidence)
        => (SourceFlags8, SourceSigned3F4, SourceEvidence) = (flags8, signed3F4, evidence);
    public uint SourceFlags8 { get; }
    public int SourceSigned3F4 { get; }
    public string SourceEvidence { get; }
    public DeferredEarlyShadowDirectionAuxFields CaptureCurrent(MapFreshCommonResourceSnapshot actualCommon)
    {
        ArgumentNullException.ThrowIfNull(actualCommon);
        if (!actualCommon.CommonViewResource240Present) return new(false, actualCommon.CommonViewFlags8, null);
        if (actualCommon.CopyCurrentAuxiliary460() is { } actualAux)
            return new(true, actualCommon.CommonViewFlags8, BinaryPrimitives.ReadInt32LittleEndian(actualAux.AsSpan(0x3F4)));
        if (actualCommon.Scope != MapFreshCommonResourceScope.SourceCreatedNoCallbackBeforeAmbientPublication ||
            actualCommon.CommonViewFlags8 != SourceFlags8)
            throw new NotSupportedException("Current shadow angle gate needs its actual3F4 or the retained source-loader scope.");
        return new(true, actualCommon.CommonViewFlags8, SourceSigned3F4);
    }
}

public readonly record struct DeferredEarlyShadowDirectionNumericFields(
    Vector4 Camera160, Vector4 Camera170, float ScaleE0, Vector4 BasisB0, Vector4 BasisC0, Vector4 DirectionD0)
{
    /// <summary>Actual D12470 direction/type/W stores plus A02770 numeric
    /// results. Camera240..38F and matrices5D0 retain their own producer.</summary>
    public void WriteOwnedCpuNumericFields(Span<byte> actualOwnedCpu)
    {
        if (actualOwnedCpu.Length < 0x3C0)
            throw new ArgumentException("Shadow direction numeric stores require the actual own CPU through3BF.", nameof(actualOwnedCpu));
        BinaryPrimitives.WriteUInt32LittleEndian(actualOwnedCpu[0x230..], 0);
        BinaryPrimitives.WriteSingleLittleEndian(actualOwnedCpu[0x24C..], 1);
        BinaryPrimitives.WriteSingleLittleEndian(actualOwnedCpu[0x25C..], 1);
        BinaryPrimitives.WriteUInt32LittleEndian(actualOwnedCpu[0x390..], 1);
        V(actualOwnedCpu, 0x3A0, Camera160); V(actualOwnedCpu, 0x3B0, Camera170);
        BinaryPrimitives.WriteSingleLittleEndian(actualOwnedCpu[0xE0..], ScaleE0);
        V(actualOwnedCpu, 0xB0, BasisB0); V(actualOwnedCpu, 0xC0, BasisC0); V(actualOwnedCpu, 0xD0, DirectionD0);
    }
    private static void V(Span<byte> bytes, int at, Vector4 value)
    {
        BinaryPrimitives.WriteSingleLittleEndian(bytes[at..], value.X);
        BinaryPrimitives.WriteSingleLittleEndian(bytes[(at + 4)..], value.Y);
        BinaryPrimitives.WriteSingleLittleEndian(bytes[(at + 8)..], value.Z);
        BinaryPrimitives.WriteSingleLittleEndian(bytes[(at + 12)..], value.W);
    }
}

public sealed class DeferredEarlyShadowCurrentDirectionFields
{
    internal DeferredEarlyShadowCurrentDirectionFields(DeferredEarlyShadowCurrentMainFields main,
        TerrainSelectedKind1Record record, MapFreshCommonResourceSnapshot common,
        DeferredEarlyShadowDirectionAuxFields aux, DeferredEarlyShadowDirectionNumericFields numeric, string evidence)
        => (Main, Record, Common, Aux, Numeric, SourceEvidence) = (main, record, common, aux, numeric, evidence);
    public DeferredEarlyShadowCurrentMainFields Main { get; }
    public TerrainSelectedKind1Record Record { get; }
    public MapFreshCommonResourceSnapshot Common { get; }
    public DeferredEarlyShadowDirectionAuxFields Aux { get; }
    public DeferredEarlyShadowDirectionNumericFields Numeric { get; }
    public string SourceEvidence { get; }
}

/// <summary>D11190's actual record8/A04 selection, D12470 own direction
/// tail and A02770/A02810 plane basis. Main orientation is not used for light
/// direction. The currently unused angle-reconstruction branch stays separate.</summary>
public static class DeferredEarlyShadowDirectionSource
{
    public static LoadedDeferredEarlyShadowDirectionAuxSource ReadSourceCreatedTemplate(LoadedObjects actualCommonSource)
    {
        var actual = MapFreshCommonResources.ReadSourceTemplate(actualCommonSource);
        var field = actualCommonSource.Document.Objects.Single(value => value.ObjectId == MapFreshCommonResources.SourceObjectId).Properties[24];
        if (field.Index != 24 || field.NameHash != 0x00D3B791 || field.TypeCode != 4 || field.ElementCount != 1 ||
            field.ElementSize != 4 || field.RawValue.Length != 4)
            throw new InvalidDataException("Shadow Aux3F4 differs from its actual saved Common p24 binding.");
        return new(actual.Flags8, BinaryPrimitives.ReadInt32LittleEndian(field.RawValue), actual.SourceIdentity +
            ";TC:D223C0-pointertable2A0/360-index24->scratchRBP-58;D23099/D2309C->Aux3F4;D1E719/D1E720-preserved-copy");
    }

    public static DeferredEarlyShadowCurrentDirectionFields PrepareCurrent(
        DeferredEarlyShadowCurrentMainFields actualMain,
        TerrainSelectedKind1Record actualSelectedRecord,
        MapSourceCreatedLightingPacket actualLighting,
        MapFreshCommonResourceSnapshot actualCommon,
        LoadedDeferredEarlyShadowDirectionAuxSource actualLoadedAux,
        DeferredEarlyShadowPreparationArguments actualArguments)
    {
        ArgumentNullException.ThrowIfNull(actualMain); ArgumentNullException.ThrowIfNull(actualSelectedRecord);
        ArgumentNullException.ThrowIfNull(actualLighting); ArgumentNullException.ThrowIfNull(actualCommon);
        ArgumentNullException.ThrowIfNull(actualLoadedAux);
        DeferredEarlyShadowCurrentMainFields.RequireActualMain(actualMain.ActualMainInputs, actualMain.PreparedMain);
        actualSelectedRecord.ValidateFor(actualLighting);
        actualCommon.ValidateFrame(actualMain.PreparedMain.FrameNumber, actualCommon.CommonTableOwnerIdentity, actualSelectedRecord.ViewIdentity);
        if (actualSelectedRecord.FrameNumber != actualMain.PreparedMain.FrameNumber || actualLighting.FrameNumber != actualMain.PreparedMain.FrameNumber)
            throw new InvalidDataException("Shadow direction requires the same accepted Main, selected record and Lighting frame.");
        var aux = actualLoadedAux.CaptureCurrent(actualCommon);
        if (aux.RequiresAngleClamp(actualArguments))
            throw new NotSupportedException("Actual shadow angle clamp requires CB78C0/9B3C00 reconstruction before own direction publication.");
        var numeric = PrepareUnclampedNumericFields(actualSelectedRecord.CompleteLightData.Span,
            actualMain.Values.DirectionSelectorA04, actualMain.Values.DirectionScaleA28);
        return new(actualMain, actualSelectedRecord, actualCommon, aux, numeric, actualLoadedAux.SourceEvidence +
            ";D111F2-record8-first-pair;D112D5/D1130A-actual-A04-selection;D12470-CPU390/3A0/3B0;" +
            "A02770/A02810-native-plane-basis;actual-selected-LightData-not-Main-matrix;finite-direction/not-Camera240-matrices");
    }

    /// <summary>Complete unclamped D11190 A04 selection. Unknown selectors
    /// retain the first record vector, exactly like the source switch.</summary>
    public static DeferredEarlyShadowDirectionNumericFields PrepareUnclampedNumericFields(
        ReadOnlySpan<byte> actualRecordLightData, uint actualA04, float actualA28)
    {
        if (actualRecordLightData.Length < 0x80)
            throw new ArgumentException("Shadow direction requires its actual record8 through the original four pairs.", nameof(actualRecordLightData));
        int at = actualA04 switch { 1 => 0x20, 2 => 0x40, 3 => 0x60, _ => 0 };
        var direction = Read(actualRecordLightData, at) with { W = 0 };
        var second = Read(actualRecordLightData, 0x10);
        (Vector4 firstBasis, Vector4 secondBasis) = PlaneBasis(new(direction.X, direction.Y, direction.Z));
        return new(direction, second, actualA28, firstBasis * actualA28, secondBasis * actualA28, direction);
    }

    /// <summary>Original A02810 chooses the least absolute component.
    /// Scalar operation order and tie branches match the three SSE paths.</summary>
    public static (Vector4 First, Vector4 Second) PlaneBasis(Vector3 actualDirection)
    {
        float x = actualDirection.X, y = actualDirection.Y, z = actualDirection.Z;
        float ax = MathF.Abs(x), ay = MathF.Abs(y), az = MathF.Abs(z);
        if (ay > ax && az >= ax)
        {
            float pair = z * z + y * y;
            float inverse = 1 / MathF.Sqrt(pair);
            float inverseSecond = 1 / MathF.Sqrt((x * x + pair) * pair);
            return (new(0, inverse * z, -(inverse * y), 0),
                new(-(inverseSecond * pair), (x * y) * inverseSecond, (z * x) * inverseSecond, 0));
        }
        if (ay <= ax && az > ay)
        {
            float pair = z * z + x * x;
            float inverse = 1 / MathF.Sqrt(pair);
            float inverseSecond = 1 / MathF.Sqrt((y * y + pair) * pair);
            return (new(-(z * inverse), 0, inverse * x, 0),
                new((x * y) * inverseSecond, -(inverseSecond * pair), (z * y) * inverseSecond, 0));
        }
        {
            float pair = y * y + x * x;
            float inverse = 1 / MathF.Sqrt(pair);
            float inverseSecond = 1 / MathF.Sqrt((z * z + pair) * pair);
            return (new(y * inverse, -(inverse * x), 0, 0),
                new((z * x) * inverseSecond, (z * y) * inverseSecond, -(inverseSecond * pair), 0));
        }
    }
    private static Vector4 Read(ReadOnlySpan<byte> data, int at) => new(
        BinaryPrimitives.ReadSingleLittleEndian(data[at..]), BinaryPrimitives.ReadSingleLittleEndian(data[(at + 4)..]),
        BinaryPrimitives.ReadSingleLittleEndian(data[(at + 8)..]), BinaryPrimitives.ReadSingleLittleEndian(data[(at + 12)..]));
}
