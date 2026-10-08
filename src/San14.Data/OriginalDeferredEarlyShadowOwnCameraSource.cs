using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

/// <summary>The finite CF4C90/AB5BD0/D12470 camera input to the own shadow
/// controller. The stripped Camera prefix is not a final shadow projection.</summary>
public sealed class OriginalDeferredEarlyShadowOwnCameraNumericFields
{
    private readonly byte[] prefix;
    internal OriginalDeferredEarlyShadowOwnCameraNumericFields(byte[] prefix, Matrix4x4 projection,
        Matrix4x4 combined, bool updatedView)
        => (this.prefix, PreparedProjectionB0, PreparedWorldToProjectionF0, UpdatedView) = (prefix, projection, combined, updatedView);
    public ReadOnlyMemory<byte> OwnCameraPrefix150 => prefix;
    public Matrix4x4 PreparedProjectionB0 { get; }
    public Matrix4x4 PreparedWorldToProjectionF0 { get; }
    public bool UpdatedView { get; }

    /// <summary>Only D12470's own Camera240..38F copy and two W=1 stores.
    /// Direction/type, work130, effective-pass mask and final5D0 are separate.</summary>
    public void WriteOwnedCpuCameraFields(Span<byte> actualOwnedCpu)
    {
        if (actualOwnedCpu.Length < 0x390)
            throw new ArgumentException("Shadow own Camera copy requires its actual CPU through38F.", nameof(actualOwnedCpu));
        prefix.CopyTo(actualOwnedCpu[0x240..]);
    }
}

public sealed class OriginalDeferredEarlyShadowCurrentOwnCameraFields
{
    internal OriginalDeferredEarlyShadowCurrentOwnCameraFields(OriginalDeferredEarlyShadowCurrentMainFields main,
        OriginalDeferredEarlyShadowCurrentDirectionFields direction, OriginalDeferredEarlyShadowOwnCameraNumericFields numeric)
        => (Main, Direction, Numeric) = (main, direction, numeric);
    public OriginalDeferredEarlyShadowCurrentMainFields Main { get; }
    public OriginalDeferredEarlyShadowCurrentDirectionFields Direction { get; }
    public OriginalDeferredEarlyShadowOwnCameraNumericFields Numeric { get; }
    public string SourceEvidence => Direction.SourceEvidence + ";TC:CF4D81..CF4E3D-actual-Main-copy;" +
        "AB64B0-near/far-and-projection-dirty;CF4E49-clear-reverse;AB5BD0-retained-or-dirty-view/own-projection;" +
        "D124A0..D12532-header-stripped-own-Camera240[150]/W1;not-final-shadow-matrix5D0";
}

/// <summary>Builds the actual own-camera input used by both source shadow
/// families. CF4 changes far and reverse depth, always dirties projection,
/// and respects the accepted Main's view dirty flag. Final light-space
/// cascades still require the selected own controller's numeric producer.</summary>
public static class OriginalDeferredEarlyShadowOwnCameraSource
{
    public static OriginalDeferredEarlyShadowCurrentOwnCameraFields PrepareCurrent(
        OriginalDeferredEarlyShadowCurrentMainFields actualMain,
        OriginalDeferredEarlyShadowCurrentDirectionFields actualDirection,
        uint nativeClipProfile, OriginalNativeCameraTangentDispatch tangentDispatch)
    {
        ArgumentNullException.ThrowIfNull(actualMain); ArgumentNullException.ThrowIfNull(actualDirection);
        if (!ReferenceEquals(actualDirection.Main, actualMain))
            throw new InvalidDataException("Own shadow Camera requires the selected direction's same actual Main preparation.");
        OriginalDeferredEarlyShadowCurrentMainFields.RequireActualMain(actualMain.ActualMainInputs, actualMain.PreparedMain);
        var numeric = PrepareNumericFields(actualMain.PreparedMain.Prefix.Span, actualMain.Values.Cascades.Far9EC,
            nativeClipProfile, tangentDispatch);
        return new(actualMain, actualDirection, numeric);
    }

    public static OriginalDeferredEarlyShadowOwnCameraNumericFields PrepareNumericFields(
        ReadOnlySpan<byte> actualMainPrefix, float actualFar9EC, uint nativeClipProfile,
        OriginalNativeCameraTangentDispatch tangentDispatch)
    {
        RequirePrefix(actualMainPrefix);
        float half = OriginalNativeFloat.Multiply(BinaryPrimitives.ReadSingleLittleEndian(actualMainPrefix[0x148..]), .5f);
        float tangent = OriginalNativeCameraTangent.Tangent1020A90(half, tangentDispatch);
        return PrepareNumericFieldsFromTangent(actualMainPrefix, actualFar9EC, nativeClipProfile, tangent);
    }

    /// <summary>The explicit native tanf boundary used by source CPU oracles.
    /// This is a numeric kernel; it does not attest a current Main owner.</summary>
    public static OriginalDeferredEarlyShadowOwnCameraNumericFields PrepareNumericFieldsFromTangent(
        ReadOnlySpan<byte> actualMainPrefix, float actualFar9EC, uint nativeClipProfile, float nativeTangentOfHalfFov)
    {
        RequirePrefix(actualMainPrefix);
        byte[] local = actualMainPrefix[..0x164].ToArray();
        // AB64B0 always writes both lens values and marks projection dirty.
        BinaryPrimitives.WriteSingleLittleEndian(local.AsSpan(0x144), actualFar9EC);
        uint flags = BinaryPrimitives.ReadUInt32LittleEndian(local.AsSpan(0x158)) & ~2u;
        BinaryPrimitives.WriteUInt32LittleEndian(local.AsSpan(0x158), flags);
        local[0x161] = 1;
        var lens = OriginalCameraLens.DecodePrefix(local.AsSpan(0, 0x160));
        if (lens.IsOrthographic)
            throw new NotSupportedException("Shadow own-camera input currently requires the original perspective branch.");
        bool updateView = local[0x160] != 0;
        var raw = ReadMatrix(local, 0x40);
        var view = ReadMatrix(local, 0x80);
        if (updateView)
        {
            var rebuilt = OriginalMapCameraConstructionMath.ViewAB6BC0(
                ReadVector(local, 0x10), ReadVector(local, 0x20), ReadVector(local, 0x30), lens.ViewOffset);
            raw = rebuilt.RawWorldToView40; view = rebuilt.WorldToView80;
        }
        var projection = OriginalMapCameraConstructionMath.ProjectionFromTangentAB7040(
            lens.VerticalFovRadians, lens.Aspect, lens.Near, lens.Far, nativeClipProfile, 1, nativeTangentOfHalfFov);
        var combined = OriginalUnitWorldTransformMath.ComposeAffine17ADD0(view, projection);
        new OriginalMapCameraConstructionMatrices(raw, view, projection, combined).WriteEarlyMatrices(local);
        local[0x160] = local[0x161] = 0;
        // D12470's R9 points at local Camera+10: the source header is omitted.
        byte[] own = local.AsSpan(0x10, 0x150).ToArray();
        BinaryPrimitives.WriteSingleLittleEndian(own.AsSpan(0xC), 1);
        BinaryPrimitives.WriteSingleLittleEndian(own.AsSpan(0x1C), 1);
        return new(own, projection, combined, updateView);
    }

    private static void RequirePrefix(ReadOnlySpan<byte> prefix)
    {
        if (prefix.Length != OriginalMapCameraConstruction.PrefixByteLength)
            throw new ArgumentException("Shadow own-camera input requires the actual accepted Main3C0 prefix.", nameof(prefix));
    }
    private static Vector4 ReadVector(ReadOnlySpan<byte> bytes, int at) => new(
        BinaryPrimitives.ReadSingleLittleEndian(bytes[at..]), BinaryPrimitives.ReadSingleLittleEndian(bytes[(at + 4)..]),
        BinaryPrimitives.ReadSingleLittleEndian(bytes[(at + 8)..]), BinaryPrimitives.ReadSingleLittleEndian(bytes[(at + 12)..]));
    private static Matrix4x4 ReadMatrix(ReadOnlySpan<byte> bytes, int at)
    {
        Vector4 a = ReadVector(bytes, at), b = ReadVector(bytes, at + 16), c = ReadVector(bytes, at + 32), d = ReadVector(bytes, at + 48);
        return new(a.X,a.Y,a.Z,a.W,b.X,b.Y,b.Z,b.W,c.X,c.Y,c.Z,c.W,d.X,d.Y,d.Z,d.W);
    }
}
