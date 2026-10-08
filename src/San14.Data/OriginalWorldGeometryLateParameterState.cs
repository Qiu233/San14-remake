using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

/// <summary>The caller's retained Generic time cache. D724C0 decomposes the
/// requested time using the current FD8 period; restoring a saved time is
/// another setter call, rather than restoring the old FD0 scalar.</summary>
public sealed class OriginalWorldGeometryLateParameterTimeState
{
    public OriginalWorldGeometryLateParameterTimeState(Vector4 actualFd0, ushort actualF82,
        ulong actualDirtyMask)
        => (ValueFd0, ValidF82, DirtyMask) = (actualFd0, actualF82, actualDirtyMask);
    public Vector4 ValueFd0 { get; private set; }
    public ushort ValidF82 { get; private set; }
    public ulong DirtyMask { get; private set; }
    public float ReadCurrentTime()
    {
        float product = ValueFd0.Z * ValueFd0.Y;
        return product + ValueFd0.X;
    }
    public void SetCurrentTime(float requestedTime)
    {
        // Keep scalar source order: DIVSS -> floorf -> MULSS -> SUBSS.
        float quotient = requestedTime / ValueFd0.Z;
        float whole = MathF.Floor(quotient);
        float product = whole * ValueFd0.Z;
        var next = new Vector4(requestedTime - product, whole, ValueFd0.Z, 0);
        if (ValidF82 == 1 && Equal(ValueFd0, next)) return;
        ValueFd0 = next; ValidF82 = 1; DirtyMask |= 0x40000000UL;
    }
    // UCOMISS regards a NaN as unequal, and regards +0/-0 as equal.
    private static bool Equal(Vector4 a, Vector4 b) =>
        a.X == b.X && a.Y == b.Y && a.Z == b.Z && a.W == b.W;
}

/// <summary>Parameter-loan entry cleared by D1BA00. These are borrowed resource
/// owner references, not texture allocations; clearing them never frees the
/// resource owner or changes the attached framebuffer.</summary>
public readonly record struct OriginalWorldGeometryGenericResourceSlot(
    int GenericOffset, int FrontendOffset, uint NativeCommandCode);

public static class OriginalWorldGeometryLateGenericResources
{
    private static readonly OriginalWorldGeometryGenericResourceSlot[] slots =
    [
        new(0x28, 0xA8, 0x0D), // TC984DD0 / SC985F60
        new(0x38, 0xC8, 0x10), // TC984260 / SC9853F0
        new(0x40, 0xD0, 0x11), // TC983DC0 / SC984F50
        new(0x48, 0xD8, 0x0F), // TC984010 / SC9851A0
        new(0x50, 0xE0, 0x12), // TC983B70 / SC984D00
        new(0x30, 0xB8, 0x0E), // TC9844B0 / SC985640
    ];
    public static IReadOnlyList<OriginalWorldGeometryGenericResourceSlot> ClearOrder =>
        Array.AsReadOnly(slots);

    /// <summary>Invoke the actual owning Generic setters in source order.
    /// Each setter also updates its current resource parameters and frontend
    /// handle cache. No device object is disposed by this recipe.</summary>
    public static void ClearActualLoans(Action<OriginalWorldGeometryGenericResourceSlot, object?> actualSetter)
    {
        ArgumentNullException.ThrowIfNull(actualSetter);
        foreach (var slot in slots) actualSetter(slot, null);
    }
}

/// <summary>Current node58 caller gate, independent of the selected primitive
/// shader. The input is the actual record LightData obtained through D59210;
/// a final state2-null table is not a substitute for these fields.</summary>
public static class OriginalWorldGeometryLateRecordParameterGates
{
    /// <summary>D59210 returns the embedded modifier Param, not the base
    /// LightData. Its two classifier reads select null or offset1B0.</summary>
    public static int ResolveModifierOffset(ReadOnlySpan<byte> actualRecordLightData)
    {
        if (actualRecordLightData.Length < 0x1FC)
            throw new InvalidDataException("D59210 requires the actual record LightData classifier fields.");
        uint flags44 = BinaryPrimitives.ReadUInt32LittleEndian(actualRecordLightData.Slice(0x1F4, 4));
        uint flags48 = BinaryPrimitives.ReadUInt32LittleEndian(actualRecordLightData.Slice(0x1F8, 4));
        return (flags44 & 0xFFFFF0FFu) == 0xFFFFF0FFu && (flags48 & 0x1FFu) == 0x1FFu ? -1 : 0x1B0;
    }
    /// <summary>EA76E1/EA7810 supplies CPU728.bit21 as dispatcher8C30.
    /// When clear, the caller skips the deeper modifier reads as well as
    /// D76780. This gate is unrelated to the selected shader state.</summary>
    public static bool NeedsGenericPreparation(ulong actualCpu728, ReadOnlySpan<byte> actualModifierParam)
    {
        if ((actualCpu728 & (1UL << 21)) == 0 || actualModifierParam.IsEmpty) return false;
        if (actualModifierParam.Length <= 0x9BC)
            throw new InvalidDataException("The active modifier gate requires the actual Param+9BC/+96C fields.");
        if (actualModifierParam[0x9BC] != 0) return true;
        return BinaryPrimitives.ReadSingleLittleEndian(actualModifierParam.Slice(0x96C, 4)) > 0;
    }
}
