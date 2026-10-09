using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace San14.Data;

/// <summary>Finite original9B4FF0/9C0560/996DD4 writers with explicit prepared
/// camera and plugin inputs. Source offsets are labels, not current lens/default
/// discovery; absent texture or disabled plugin preparation retains cache.</summary>
public static class DeferredRefractionMath
{
    /// <summary>Independent LocalAO module9967E0: uint157C=2 and scale1580=1.
    /// These fields are not similarly numbered Camera or Main fields.</summary>
    public static DeferredLocalAmbientOcclusionInputs PrepareFreshLocalAmbientOcclusion(
        int currentColorWidth,int currentColorHeight)
        => PrepareLocalAmbientOcclusion(2,currentColorWidth,currentColorHeight);
    public static float FreshLocalAmbientOcclusionStepScale => 1;
    /// <summary>Original F103 temporary named override. Null means no setter;
    /// scratchA/B semantic size roles are not inferred. Installed overrides
    /// must be followed by the original hardcoded postdraw restore.</summary>
    public static DeferredTranslucencyViewportInputs? PrepareTemporaryTranslucencyViewport(float scratchA,
        float scratchB,uint sourceCpu594,byte callerStatus)
    {
        if (!float.IsFinite(scratchA) || !float.IsFinite(scratchB))
            throw new InvalidDataException("Temporary translucency viewport requires finite explicit scratch values.");
        if ((scratchA == 1 && scratchB == 1) || ((sourceCpu594 & 0x100u) == 0 && callerStatus == 0)) return null;
        return new(new Vector4(0,0,scratchA,scratchB));
    }

    /// <summary>Only after a temporary setter was installed. This is the source
    /// literal, not an initial default or restoration of arbitrary prior data.</summary>
    public static DeferredTranslucencyViewportInputs AfterInstalledViewportDraw() => new(new Vector4(0,0,1,1));

    public static DeferredRefractionInputs Prepare(DeferredCameraInputs camera, Vector3 preparedTarget,
        float camera130, float camera134, bool camera148Bit1, float viewportDepthMin, float viewportDepthMax,
        bool explicitRefractionTexturePresent)
    {
        ArgumentNullException.ThrowIfNull(camera);
        Vector3 position = new(camera.SourceEye.X, camera.SourceEye.Y, camera.SourceEye.Z);
        if (!Finite(position) || !Finite(preparedTarget)) throw new InvalidDataException("Refraction requires a finite explicit prepared Eye and Target.");
        float x = Subtract(preparedTarget.X, position.X), y = Subtract(preparedTarget.Y, position.Y), z = Subtract(preparedTarget.Z, position.Z);
        float lengthSquared = Add(Add(Multiply(x, x), Multiply(y, y)), Multiply(z, z));
        if (!Sse.IsSupported) throw new PlatformNotSupportedException("Original refraction normalization requires x64 SSE.");
        if (!float.IsFinite(lengthSquared) || lengthSquared <= 0) throw new InvalidDataException("Refraction direction has a zero or nonfinite length.");
        float inverseLength = Divide(1, Sse.SqrtScalar(Vector128.CreateScalar(lengthSquared)).GetElement(0));
        var direction = new Vector3(Multiply(x, inverseLength), Multiply(y, inverseLength), Multiply(z, inverseLength));
        Matrix4x4 view = camera.SourceWorldToView;
        var viewY = new Vector3(view.M12, view.M22, view.M32);
        if (!Finite(viewY)) throw new InvalidDataException("Refraction view basis is nonfinite.");
        Vector2 depth = PrepareDepthToZ(camera130, camera134, camera148Bit1, viewportDepthMin, viewportDepthMax);
        IReadOnlyList<Vector4>? texture = explicitRefractionTexturePresent ? PrepareWorldToTexture(camera.SourceWorldToProjection) : null;
        return new(position, direction, viewY, depth, texture);
    }

    public static Vector2 PrepareDepthToZ(float camera130, float camera134, bool camera148Bit1,
        float viewportDepthMin, float viewportDepthMax)
    {
        if (new[] { camera130, camera134, viewportDepthMin, viewportDepthMax }.Any(v => !float.IsFinite(v)))
            throw new InvalidDataException("Depth conversion requires explicit finite camera and viewport fields.");
        float u = camera148Bit1 ? camera130 : camera134, v = camera148Bit1 ? camera134 : camera130;
        float q = Divide(1, Subtract(viewportDepthMax, viewportDepthMin));
        float second = Multiply(Divide(Subtract(v, u), Multiply(v, u)), q);
        float first = Multiply(viewportDepthMin, q);
        first = Multiply(first, Subtract(u, v)); first = Divide(first, u); first = Add(first, 1); first = Multiply(first, Divide(1, v));
        if (!float.IsFinite(first) || !float.IsFinite(second)) throw new InvalidDataException("Depth conversion produced a nonfinite result.");
        return new(first, second);
    }

    /// <summary>9A8410 overwrites the caller's texture-size arguments with
    /// original half constants; no size-dependent correction is introduced.</summary>
    public static IReadOnlyList<Vector4> PrepareWorldToTexture(Matrix4x4 worldToProjection)
    {
        float[] p = [worldToProjection.M11,worldToProjection.M12,worldToProjection.M13,worldToProjection.M14,
            worldToProjection.M21,worldToProjection.M22,worldToProjection.M23,worldToProjection.M24,
            worldToProjection.M31,worldToProjection.M32,worldToProjection.M33,worldToProjection.M34,
            worldToProjection.M41,worldToProjection.M42,worldToProjection.M43,worldToProjection.M44];
        if (p.Any(v => !float.IsFinite(v))) throw new InvalidDataException("Refraction projection input is nonfinite.");
        float[] sx = new float[4], sy = new float[4], sw = new float[4];
        for (int i = 0; i < 4; i++)
        {
            sx[i] = Add(Multiply(p[i*4+3], .5f), Multiply(p[i*4], .5f));
            sy[i] = Add(Multiply(p[i*4+3], .5f), Multiply(p[i*4+1], -.5f)); sw[i] = p[i*4+3];
        }
        return Array.AsReadOnly(new[] { new Vector4(sx[0],sx[1],sx[2],sx[3]), new Vector4(sy[0],sy[1],sy[2],sy[3]), new Vector4(sw[0],sw[1],sw[2],sw[3]) });
    }

    public static DeferredProjectionInputs? PrepareProjectionToView(Matrix4x4 cameraMatrixB0, bool explicitPluginA8Bit0)
        => explicitPluginA8Bit0 ? new(NativeMatrixMath.Invert(cameraMatrixB0, out _)) : null;

    public static DeferredLocalAmbientOcclusionInputs PrepareLocalAmbientOcclusion(uint source157C,
        int currentColorWidth, int currentColorHeight)
    {
        if (currentColorWidth <= 0 || currentColorHeight <= 0)
            throw new InvalidDataException("Local ambient occlusion requires an existing positive-size currentColor.");
        float k = source157C, reciprocal = Divide(1, currentColorHeight);
        return new(new Vector2(Multiply(Add(k, k), reciprocal), Multiply(currentColorWidth, reciprocal)));
    }

    private static bool Finite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Multiply(float left, float right) => left * right;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Divide(float left, float right) => left / right;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Add(float left, float right) => left + right;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Subtract(float left, float right) => left - right;
}
