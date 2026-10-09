using System.Buffers.Binary;

namespace San14.Data;

/// <summary>Finite numeric stores from the actual pool6C8 compact caster
/// constructor. The controller binds BD0 to its own Camera240.</summary>
public static class DeferredEarlyShadowCompactConstructor
{
    public const int NativeCpuByteLength = 0xD30;
    public const int ControllerOffset = 0x8F0;
    public static int PassBoundsOffset(int index) => (uint)index < 4u
        ? ControllerOffset + index * 0xB0 : throw new ArgumentOutOfRangeException(nameof(index));

    /// <summary>10571B0/1057230 numeric constructor stores only. Unwritten
    /// own Camera240,5D0,BE0 matrices, effective maskBC4 and pointers retain
    /// their real producers. No default whole ShadowCPU is claimed.</summary>
    public static void ApplyOwnedNumericConstructorFragment(Span<byte> actualAllocatedCpu)
    {
        RequireStorage(actualAllocatedCpu.Length);
        W(actualAllocatedCpu, 0x18, 2); W(actualAllocatedCpu, 0x22C, 0x80000003);
        W(actualAllocatedCpu, 0x230, 0); W(actualAllocatedCpu, 0xBC0, 2);
        BinaryPrimitives.WriteUInt16LittleEndian(actualAllocatedCpu[0xD20..], 0x0100);
        BinaryPrimitives.WriteUInt16LittleEndian(actualAllocatedCpu[0xD22..], 0xFFFF);
        BinaryPrimitives.WriteUInt16LittleEndian(actualAllocatedCpu[0xD24..], 0);
        for (int i = 0; i < 4; i++)
        {
            int unit = PassBoundsOffset(i);
            for (int bounds = 0; bounds <= 0x20; bounds += 0x20)
            {
                for (int component = 0; component < 3; component++)
                {
                    W(actualAllocatedCpu, unit + bounds + component * 4, 0x7722B3DB);
                    W(actualAllocatedCpu, unit + bounds + 0x10 + component * 4, 0xF722B3DB);
                }
                W(actualAllocatedCpu, unit + bounds + 0xC, 0x3F800000);
                W(actualAllocatedCpu, unit + bounds + 0x1C, 0x3F800000);
            }
            actualAllocatedCpu.Slice(unit + 0x40, 0x14).Clear();
            W(actualAllocatedCpu, unit + 0x54, 0x7F7FFFFF);
            W(actualAllocatedCpu, unit + 0xA0, 0);
        }
    }
    internal static void RequireStorage(int length)
    {
        if (length < NativeCpuByteLength)
            throw new ArgumentException("Original pool6C8 numeric stores require its actualD30-byte allocation.");
    }
    private static void W(Span<byte> destination, int at, uint value)
        => BinaryPrimitives.WriteUInt32LittleEndian(destination[at..], value);
}

public sealed class DeferredEarlyShadowCompactNumericFields
{
    private readonly float[] ends;
    internal DeferredEarlyShadowCompactNumericFields(int count, float[] ends, float far)
        => (PassCount, this.ends, FarEC) = (count, ends, far);
    public int PassCount { get; }
    public IReadOnlyList<float> PassEndsBB0ToBBC => Array.AsReadOnly(ends);
    public uint Mask22C => 0x80000000u | ((1u << PassCount) - 1u);
    public float FarEC { get; }

    /// <summary>D097E0/D6F130 finite stores. AtlasCE0, effective maskBC4,
    /// work130, matricesBE0/5D0 and caster inventory are not supplied here.</summary>
    public void WriteOwnedCpuNumericFields(Span<byte> actualOwnedCpu)
    {
        DeferredEarlyShadowCompactConstructor.RequireStorage(actualOwnedCpu.Length);
        BinaryPrimitives.WriteInt32LittleEndian(actualOwnedCpu[0x18..], PassCount);
        BinaryPrimitives.WriteInt32LittleEndian(actualOwnedCpu[0xBC0..], PassCount);
        BinaryPrimitives.WriteUInt32LittleEndian(actualOwnedCpu[0x22C..], Mask22C);
        BinaryPrimitives.WriteUInt32LittleEndian(actualOwnedCpu[0x230..], 0);
        BinaryPrimitives.WriteSingleLittleEndian(actualOwnedCpu[0xEC..], FarEC);
        for (int i = 0; i < 4; i++)
            BinaryPrimitives.WriteSingleLittleEndian(actualOwnedCpu[(0xBB0 + i * 4)..], ends[i]);
        actualOwnedCpu[0xD25] = 0;
    }
}

/// <summary>A finite compact controller preparation attached to the actual
/// own Camera's Main and selected direction owners.</summary>
public sealed class DeferredEarlyShadowCurrentCompactFields
{
    internal DeferredEarlyShadowCurrentCompactFields(DeferredEarlyShadowCurrentOwnCameraFields ownCamera,
        DeferredEarlyShadowCompactNumericFields numeric)
        => (OwnCamera, Numeric) = (ownCamera, numeric);
    public DeferredEarlyShadowCurrentOwnCameraFields OwnCamera { get; }
    public DeferredEarlyShadowCompactNumericFields Numeric { get; }
    public string SourceEvidence => OwnCamera.SourceEvidence + ";TC:D11190-current-mode0/9/10/11->D097E0;" +
        "pool6C8/D0C8D0/B92870->10571B0/1057230-own-controller8F0/BD0->Camera240;" +
        "D6F130-endpointsBB0/countBC0;finite-controller-not-effective-matrix/caster-publication";
}

public static class DeferredEarlyShadowCompactSource
{
    public static DeferredEarlyShadowCurrentCompactFields PrepareCurrent(
        DeferredEarlyShadowCurrentOwnCameraFields actualOwnCamera)
    {
        ArgumentNullException.ThrowIfNull(actualOwnCamera);
        var main = actualOwnCamera.Main;
        DeferredEarlyShadowCurrentMainFields.RequireActualMain(main.ActualMainInputs, main.PreparedMain);
        float mainFar = BinaryPrimitives.ReadSingleLittleEndian(main.PreparedMain.Prefix.Span[0x144..]);
        var numeric = PrepareNumericFields(main.Values.ModeA3C, main.Values.Cascades, mainFar);
        return new(actualOwnCamera, numeric);
    }

    /// <summary>The actual D097E0 normal A84=0 count/split/far branch.
    /// This uses compact offsets and never relabels a pool6B0 CPU.</summary>
    public static DeferredEarlyShadowCompactNumericFields PrepareNumericFields(uint actualModeA3C,
        DeferredEarlyShadowCascadeMainFields actualFields, float actualMainFar144)
    {
        int count = actualModeA3C switch
        {
            0 => 1, 9 => 2, 10 => 3, 11 => 4,
            _ => throw new NotSupportedException("D097E0 returns null for this actual Main mode; its own selected shadow family is required.")
        };
        if (actualFields.RectangleOverrideA84 != 0)
            throw new NotSupportedException("Compact A84 rectangle override requires actualA44..A80 and its current atlas writer.");
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
    private static float Maxss(float first, float second) => first > second ? first : second;
}
