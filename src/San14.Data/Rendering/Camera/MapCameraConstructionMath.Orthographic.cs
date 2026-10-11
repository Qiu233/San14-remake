using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace San14.Data;

public static partial class MapCameraConstructionMath
{
    /// <summary>TC AB5ED0. Near/far arguments have already been swapped for reverse depth.
    /// The source scale changes only M44; the non-bit2 depth branch includes far in its denominator.</summary>
    public static Matrix4x4 OrthographicProjectionAB5ED0(float width, float height,
        float nearArgument, float farArgument, uint nativeClipProfile, float projectionScale)
    {
        if (!float.IsFinite(width) || width <= 0 || !float.IsFinite(height) || height <= 0 ||
            !float.IsFinite(nearArgument) || nearArgument <= 0 ||
            !float.IsFinite(farArgument) || farArgument <= 0 || nearArgument == farArgument ||
            !float.IsFinite(projectionScale) || projectionScale == 0)
            throw new InvalidDataException("Original camera orthographic inputs require finite nonzero dimensions and scale.");
        float xScale = Divide(2, width), yScale = Divide(2, height);
        float range = NativeFloat.Subtract(nearArgument, farArgument);
        float depthScale, depthOffset;
        if ((nativeClipProfile & 2) != 0)
        {
            depthScale = Divide(1, range);
            depthOffset = NativeFloat.Multiply(depthScale, nearArgument);
        }
        else
        {
            float inverse = Divide(1, NativeFloat.Multiply(range, farArgument));
            float twiceNear = NativeFloat.Add(nearArgument, nearArgument);
            float sum = NativeFloat.Add(nearArgument, farArgument);
            depthScale = NativeFloat.Multiply(sum, inverse);
            depthOffset = NativeFloat.Multiply(NativeFloat.Multiply(twiceNear, farArgument), inverse);
        }
        float w = projectionScale == 1 ? 1 : Divide(1, projectionScale);
        return new(xScale,0,0,0,0,yScale,0,0,0,0,depthScale,0,0,0,depthOffset,w);
    }

    /// <summary>Normalize Target−Eye in the native scalar order, retaining the target W lane.</summary>
    public static Vector4 NormalizedLookDirection(Vector4 eye, Vector4 target)
    {
        if (!Finite(eye) || !Finite(target))
            throw new InvalidDataException("Original camera look direction requires finite eye and target.");
        return NormalizeDirectionC11350(new(NativeFloat.Subtract(target.X,eye.X),
            NativeFloat.Subtract(target.Y,eye.Y),NativeFloat.Subtract(target.Z,eye.Z),target.W));
    }

    private static Matrix4x4 OrthographicFromTangent(Vector4 eye, Vector4 target,
        CameraLens lens, uint nativeClipProfile, float nativeTangentOfHalfFov)
    {
        if (!float.IsFinite(lens.VerticalFovRadians) || lens.VerticalFovRadians <= 0 ||
            lens.VerticalFovRadians >= MathF.PI || !float.IsFinite(lens.Aspect) || lens.Aspect <= 0 ||
            !float.IsFinite(nativeTangentOfHalfFov) || nativeTangentOfHalfFov <= 0)
            throw new InvalidDataException("Original camera orthographic field of view is outside the finite source contract.");
        if (!Sse.IsSupported) throw new PlatformNotSupportedException("Original camera projection requires SSE.");
        float dx = NativeFloat.Subtract(target.X,eye.X), dy = NativeFloat.Subtract(target.Y,eye.Y),
            dz = NativeFloat.Subtract(target.Z,eye.Z);
        float squared = NativeFloat.Add(NativeFloat.Add(NativeFloat.Multiply(dx,dx),
            NativeFloat.Multiply(dy,dy)),NativeFloat.Multiply(dz,dz));
        float distance = Sse.SqrtScalar(Vector128.CreateScalar(squared)).GetElement(0);
        float product = NativeFloat.Multiply(distance,nativeTangentOfHalfFov);
        float height = NativeFloat.Add(product,product);
        float width = NativeFloat.Multiply(height,lens.Aspect);
        return OrthographicProjectionAB5ED0(width,height,
            lens.ReverseDepth ? lens.Far : lens.Near, lens.ReverseDepth ? lens.Near : lens.Far,
            nativeClipProfile,lens.OrthographicScale);
    }
}
