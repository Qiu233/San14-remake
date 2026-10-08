using System.Numerics;
using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>The original selected ClusterLightList's named scene values.
/// Null optional fields preserve an existing shader parameter; they never
/// supply a disabled-effect value.</summary>
public sealed record OriginalDeferredClusterInputs(Vector4 DepthParameters, int HorizontalPatchCount,
    int PatchCount, float? Scaling, Vector2? FadeDistance);

/// <summary>Results of the original float logarithm calls on the supplied
/// global endpoints and literal 2. Their provider is deliberately explicit:
/// this class does not substitute a platform logarithm implementation.</summary>
public sealed record OriginalDeferredClusterSourceLogarithms(float LogTwo, float LogStart, float LogEnd);

/// <summary>Original TC9AA280 / SC equivalent ClusterLightList arithmetic.
/// Consumes the caller's current viewport, prepared Camera fields, selected
/// module properties and global controls; it does not establish a live module
/// or current original frame.</summary>
public static class OriginalDeferredClusterMath
{
    public static OriginalDeferredClusterInputs Prepare(int viewportWidth, int viewportHeight,
        int clusterSize, int clusterZCount, float sourceScalingDivisor,
        OriginalDeferredClusterSourceLogarithms sourceLogarithms, float camera130, float camera134,
        bool camera148Bit1, bool scalingEnabled, bool fadeEnabled, float sourceFadeOut, float sourceFadeWidth)
    {
        ArgumentNullException.ThrowIfNull(sourceLogarithms);
        if (viewportWidth <= 0 || viewportHeight <= 0 || clusterSize <= 0)
            throw new InvalidDataException("Cluster preparation requires a positive explicit viewport and ClusterSize.");
        Finite(sourceScalingDivisor, sourceLogarithms.LogTwo, sourceLogarithms.LogStart,
            sourceLogarithms.LogEnd, camera130, camera134);
        float inverseScale = Divide(1, sourceScalingDivisor);
        float sizeMinusOne = unchecked(clusterSize - 1);
        int x = Truncate(Divide(Add(Multiply(viewportWidth, inverseScale), sizeMinusOne), clusterSize));
        int y = Truncate(Divide(Add(Multiply(viewportHeight, inverseScale), sizeMinusOne), clusterSize));
        int count = unchecked(x * y); // Original IMUL retains the low int32 word.
        float a = camera148Bit1 ? camera130 : camera134;
        float b = camera148Bit1 ? camera134 : camera130;
        float depthX = Divide(Subtract(b, a), Multiply(a, b));
        float depthY = Divide(1, b);
        float start = Divide(sourceLogarithms.LogStart, sourceLogarithms.LogTwo);
        float end = Divide(sourceLogarithms.LogEnd, sourceLogarithms.LogTwo);
        float depthZ = Divide(Subtract(clusterZCount, 1), Subtract(start, end));
        float depthW = Add(Multiply(depthZ, start), 1);
        Vector2? fade = null;
        if (fadeEnabled)
        {
            Finite(sourceFadeOut, sourceFadeWidth);
            float delta = Subtract(sourceFadeOut, sourceFadeWidth);
            // MAXSS(sourceDelta,+0) selects the second operand on equality,
            // preserving the native +0 -> sign-flipped -0 endpoint.
            float clamped = delta > 0 ? delta : 0f;
            fade = new Vector2(SignFlip(clamped), SignFlip(sourceFadeOut));
        }
        var depth = new Vector4(depthX, depthY, depthZ, depthW);
        Finite(depth.X, depth.Y, depth.Z, depth.W);
        if (scalingEnabled) Finite(inverseScale);
        if (fade is { } f) Finite(f.X, f.Y);
        return new(depth, x, count, scalingEnabled ? inverseScale : null, fade);
    }

    private static int Truncate(float value)
    {
        // This public finite client API rejects the original indefinite
        // CVTTSS2SI result instead of treating INT_MIN as a valid patch count.
        if (!float.IsFinite(value) || value < int.MinValue || (double)value > int.MaxValue)
            throw new InvalidDataException("Cluster patch count is outside the finite int32 range.");
        return (int)value;
    }
    private static void Finite(params float[] values)
    {
        if (values.Any(v => !float.IsFinite(v)))
            throw new InvalidDataException("Cluster writer requires finite consumed source values and results.");
    }
    private static float SignFlip(float value) => BitConverter.UInt32BitsToSingle(BitConverter.SingleToUInt32Bits(value) ^ 0x80000000);
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Multiply(float a, float b) => a * b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Divide(float a, float b) => a / b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Add(float a, float b) => a + b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Subtract(float a, float b) => a - b;
}
