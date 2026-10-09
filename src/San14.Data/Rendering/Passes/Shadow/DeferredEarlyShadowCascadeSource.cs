using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Only the numeric stores in1050200/10502D0/104E9C0. The
/// constructor binds its four children to the SAME own Camera240 and
/// controller8F0; it does not initialize that Camera or produce matrices.</summary>
public static class DeferredEarlyShadowCascadeConstructor
{
    public const int NativeCpuByteLength = 0x1410;
    public const int OwnCameraOffset = 0x240;
    public const int ControllerOffset = 0x8F0;
    public static int ChildOffset(int index) => (uint)index < 4u
        ? ControllerOffset + 0x60 + index * 0x290
        : throw new ArgumentOutOfRangeException(nameof(index));

    /// <summary>Finite constructor fragment. Unwritten bytes, base resource
    /// context, backend and child owner pointers retain their actual owners.
    /// In particular, Camera240, output matrices5D0 and mask13A4 are untouched.</summary>
    public static void ApplyOwnedNumericConstructorFragment(Span<byte> actualAllocatedCpu)
    {
        RequireStorage(actualAllocatedCpu.Length);
        W(actualAllocatedCpu, 0x18, 2); W(actualAllocatedCpu, 0x22C, 0x80000003);
        W(actualAllocatedCpu, 0x230, 0); W(actualAllocatedCpu, 0x13A0, 2);
        BinaryPrimitives.WriteUInt16LittleEndian(actualAllocatedCpu[0x1400..], 0x0100);
        BinaryPrimitives.WriteUInt16LittleEndian(actualAllocatedCpu[0x1402..], 0xFFFF);
        BinaryPrimitives.WriteUInt16LittleEndian(actualAllocatedCpu[0x1404..], 0);
        W(actualAllocatedCpu, ControllerOffset, 0x3F7D70A4);
        W(actualAllocatedCpu, ControllerOffset + 4, 0x3F7D70A4);
        BinaryPrimitives.WriteUInt16LittleEndian(actualAllocatedCpu[(ControllerOffset + 8)..], 0x0100);
        actualAllocatedCpu[ControllerOffset + 0x50] = 0;
        for (int i = 0; i < 4; i++)
        {
            int child = ChildOffset(i);
            for (int bounds = 0xC0; bounds <= 0x100; bounds += 0x20)
            {
                for (int component = 0; component < 3; component++)
                {
                    W(actualAllocatedCpu, child + bounds + component * 4, 0x7722B3DB);
                    W(actualAllocatedCpu, child + bounds + 0x10 + component * 4, 0xF722B3DB);
                }
                W(actualAllocatedCpu, child + bounds + 0xC, 0x3F800000);
                W(actualAllocatedCpu, child + bounds + 0x1C, 0x3F800000);
            }
            W(actualAllocatedCpu, child + 0x200, 0x3F); W(actualAllocatedCpu, child + 0x204, 0xFFF);
            W(actualAllocatedCpu, child + 0x208, 0xFF); W(actualAllocatedCpu, child + 0x210, 0);
            W(actualAllocatedCpu, child + 0x214, 0); W(actualAllocatedCpu, child + 0x218, 0x7722B3DB);
            W(actualAllocatedCpu, child + 0x21C, 0xF722B3DB); W(actualAllocatedCpu, child + 0x250, 0);
            W(actualAllocatedCpu, child + 0x258, 0); W(actualAllocatedCpu, child + 0x274, 0);
            W(actualAllocatedCpu, child + 0x278, 0); W(actualAllocatedCpu, child + 0x27C, 0x7F7FFFFF);
        }
    }

    internal static void RequireStorage(int length)
    {
        if (length < NativeCpuByteLength)
            throw new ArgumentException("Original pool6B0 ShadowCPU numeric stores require its1410-byte allocation.");
    }
    private static void W(Span<byte> bytes, int offset, uint value)
        => BinaryPrimitives.WriteUInt32LittleEndian(bytes[offset..], value);
}

/// <summary>Actual retained Main clone fields used by D09550. Far9EC is
/// separate from current Main far144; none of these is a shadow matrix.</summary>
public readonly record struct DeferredEarlyShadowCascadeMainFields(
    float Far9EC, float Split9F4, float Split9F8, float Split9FC,
    float FarOverrideA2C, uint RectangleOverrideA84);

/// <summary>D09550→D6F230's finite own CPU output. Four pass-end slots are
/// always written, including the unused slots filled by the last endpoint.</summary>
public sealed class DeferredEarlyShadowCascadeNumericFields
{
    private readonly float[] ends;
    internal DeferredEarlyShadowCascadeNumericFields(int count, float[] ends, float far)
        => (PassCount, this.ends, FarEC) = (count, ends, far);
    public int PassCount { get; }
    public IReadOnlyList<float> PassEnds1390To139C => Array.AsReadOnly(ends);
    public uint Mask22C => 0x80000000u | ((1u << PassCount) - 1u);
    public uint Mask230 => 0;
    public float FarEC { get; }

    /// <summary>Only actual count18/13A0, endpoints1390..139C, mask22C/230,
    /// rectangle flag1405 and farEC. Atlas/resource and own Camera stores
    /// keep their source producer order.</summary>
    public void WriteOwnedCpuNumericFields(Span<byte> actualOwnedCpu)
    {
        DeferredEarlyShadowCascadeConstructor.RequireStorage(actualOwnedCpu.Length);
        BinaryPrimitives.WriteInt32LittleEndian(actualOwnedCpu[0x18..], PassCount);
        BinaryPrimitives.WriteInt32LittleEndian(actualOwnedCpu[0x13A0..], PassCount);
        BinaryPrimitives.WriteUInt32LittleEndian(actualOwnedCpu[0x22C..], Mask22C);
        BinaryPrimitives.WriteUInt32LittleEndian(actualOwnedCpu[0x230..], Mask230);
        BinaryPrimitives.WriteSingleLittleEndian(actualOwnedCpu[0xEC..], FarEC);
        for (int i = 0; i < 4; i++)
            BinaryPrimitives.WriteSingleLittleEndian(actualOwnedCpu[(0x1390 + i * 4)..], ends[i]);
        actualOwnedCpu[0x1405] = 0;
    }
}

public sealed class DeferredEarlyShadowCurrentCascadeFields
{
    internal DeferredEarlyShadowCurrentCascadeFields(MapFreshCommonResourceSnapshot common,
        MapSourceCreatedViewCameraFields fields, MapCameraConstructionPacket camera,
        DeferredEarlyShadowCascadeNumericFields numeric, string evidence)
        => (Common, CameraFields, AcceptedMain, Numeric, SourceEvidence) = (common, fields, camera, numeric, evidence);
    public MapFreshCommonResourceSnapshot Common { get; }
    public MapSourceCreatedViewCameraFields CameraFields { get; }
    public MapCameraConstructionPacket AcceptedMain { get; }
    public DeferredEarlyShadowCascadeNumericFields Numeric { get; }
    public string SourceEvidence { get; }
}

/// <summary>Imported once from Main's actual properties21/23/238/239/743/578.
/// OwnShadow CPU preparation uses its actual current mode and accepted Main
/// far144. This fragment does not complete CF4C90 or caster publication.</summary>
public sealed class LoadedDeferredEarlyShadowCascadeSource
{
    internal LoadedDeferredEarlyShadowCascadeSource(DeferredEarlyShadowCascadeMainFields fields, string evidence)
        => (SourceRetainedMainFields, SourceEvidence) = (fields, evidence);
    public DeferredEarlyShadowCascadeMainFields SourceRetainedMainFields { get; }
    public string SourceEvidence { get; }

    public DeferredEarlyShadowCurrentCascadeFields PrepareSourceCreatedRetainedMain(
        MapFreshCommonResourceSnapshot actualCommon,
        MapSourceCreatedViewCameraFields actualCameraFields,
        MapCameraConstructionPacket actualAcceptedMain)
    {
        ArgumentNullException.ThrowIfNull(actualCommon); ArgumentNullException.ThrowIfNull(actualCameraFields);
        ArgumentNullException.ThrowIfNull(actualAcceptedMain);
        if (actualCommon.Scope != MapFreshCommonResourceScope.SourceCreatedNoCallbackBeforeAmbientPublication ||
            actualCommon.FrameNumber != actualCameraFields.FrameNumber ||
            actualCameraFields.CameraObjectId != DefaultView0Source.MainCameraObjectId)
            throw new InvalidDataException("Shadow cascade source retention requires the actual fresh Common and same-frame Main tail.");
        actualCameraFields.Validate(actualAcceptedMain);
        float actualMainFar144 = BinaryPrimitives.ReadSingleLittleEndian(actualAcceptedMain.Prefix.Span[0x144..]);
        var numeric = PrepareNumericFields(actualCameraFields.ViewA3C, SourceRetainedMainFields, actualMainFar144);
        return new(actualCommon, actualCameraFields, actualAcceptedMain, numeric,
            SourceEvidence + ";actual-retained-Main-A3C/current-prefix144->D09550/D6F230;finite-CPU-fields/not-matrices/not-casters");
    }

    /// <summary>The complete D09550 mode/count and split gates with the
    /// D6F230 monotonic endpoint stores, limited to the actual A84=0 branch.
    /// Rectangle overrides require their original current atlas matrix writer.</summary>
    public static DeferredEarlyShadowCascadeNumericFields PrepareNumericFields(uint actualCameraA3C,
        DeferredEarlyShadowCascadeMainFields actualFields, float actualMainFar144)
    {
        int count = actualCameraA3C switch
        {
            1 => 1, 6 => 2, 7 => 3, 8 => 4,
            _ => throw new NotSupportedException("D09550 returns null for this actual Main mode; another source ShadowCPU family is required.")
        };
        if (actualFields.RectangleOverrideA84 != 0)
            throw new NotSupportedException("D09550 A84 rectangle overrides need actual A44..A80 and the current atlas writer.");
        float far = actualFields.Far9EC;
        float first = actualFields.Split9F4 >= far ? far : Maxss(0, actualFields.Split9F4);
        float second = actualFields.Split9F8 >= far ? far : Maxss(first, actualFields.Split9F8);
        float third = actualFields.Split9FC >= far ? far : Maxss(second, actualFields.Split9FC);
        float[] supplied = [far, far, far, far];
        if (count >= 2) supplied[0] = first;
        if (count >= 3) supplied[1] = second;
        if (count == 4) supplied[2] = third;
        float[] ends = new float[4];
        float previous = supplied[0];
        for (int i = 0; i < count; i++) ends[i] = previous = Maxss(supplied[i], previous);
        for (int i = count; i < 4; i++) ends[i] = previous;
        return new(count, ends, actualFields.FarOverrideA2C > 0 ? actualFields.FarOverrideA2C : actualMainFar144);
    }

    // SSE MAXSS copies the second operand on equal/unordered inputs.
    private static float Maxss(float first, float second)
        => first > second ? first : second;
}

public static class DeferredEarlyShadowCascadeSource
{
    public static LoadedDeferredEarlyShadowCascadeSource Read(LoadedDefaultView0Source actualLoadedView0)
    {
        ArgumentNullException.ThrowIfNull(actualLoadedView0);
        var main = actualLoadedView0.CopyCameraDefinitions().Document.Objects.Single(value =>
            value.ObjectId == actualLoadedView0.MainCameraObjectId);
        float F(int ordinal, uint hash) => BinaryPrimitives.ReadSingleLittleEndian(Field(main, ordinal, hash, 8).RawValue);
        uint rectangle = BinaryPrimitives.ReadUInt32LittleEndian(Field(main, 578, 0x7D733C4E, 4).RawValue);
        var values = new DeferredEarlyShadowCascadeMainFields(F(21, 0xEACC4096), F(23, 0xDE2CE52E),
            F(238, 0x7E026E9C), F(239, 0xD7DDD8DD), F(743, 0x9932C840), rectangle);
        return new(values, actualLoadedView0.SourceIdentity +
            ";TC:C4F6F0-Main-p21/23/238/239/743/578->raw760/768/76C/770/7A0/7F8->V418-prepared9EC/9F4/9F8/9FC/A2C/A84;" +
            "D09550-pool6B0->D0C7F0-014D31D0->ABDC10->1050200/10502D0;D6F230->CPU1390/13A0");
    }
    private static DokProperty Field(DokObject owner, int ordinal, uint hash, uint type)
    {
        var field = owner.Properties[ordinal];
        if (field.Index != ordinal || field.NameHash != hash || field.TypeCode != type || field.ElementCount != 1 ||
            field.ElementSize != 4 || field.RawValue.Length != 4)
            throw new InvalidDataException("Shadow cascade declaration differs from its actual Main loader binding at" + ordinal);
        return field;
    }
}
